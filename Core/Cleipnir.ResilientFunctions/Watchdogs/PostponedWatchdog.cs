﻿using System;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Domain.Exceptions;
using Cleipnir.ResilientFunctions.ExceptionHandling;
using Cleipnir.ResilientFunctions.Helpers;
using Cleipnir.ResilientFunctions.ShutdownCoordination;
using Cleipnir.ResilientFunctions.Storage;

namespace Cleipnir.ResilientFunctions.Watchdogs;

internal class PostponedWatchdog
{
    private readonly IFunctionStore _functionStore;
    private readonly UnhandledExceptionHandler _unhandledExceptionHandler;
    private readonly ShutdownCoordinator _shutdownCoordinator;
    private readonly WatchDogReInvokeFunc _reInvoke;
    private readonly WorkQueue _workQueue;
    private readonly TimeSpan _checkFrequency;
    private readonly TimeSpan _delayStartUp;
    private readonly FunctionTypeId _functionTypeId;

    public PostponedWatchdog(
        FunctionTypeId functionTypeId,
        IFunctionStore functionStore,
        WatchDogReInvokeFunc reInvoke,
        WorkQueue workQueue,
        TimeSpan checkFrequency,
        TimeSpan delayStartUp,
        UnhandledExceptionHandler unhandledExceptionHandler,
        ShutdownCoordinator shutdownCoordinator)
    {
        _functionTypeId = functionTypeId;
        _functionStore = functionStore;
        _unhandledExceptionHandler = unhandledExceptionHandler;
        _shutdownCoordinator = shutdownCoordinator;
        _reInvoke = reInvoke;
        _workQueue = workQueue;
        _checkFrequency = checkFrequency;
        _delayStartUp = delayStartUp;
    }

    public async Task Start()
    {
        if (_checkFrequency == TimeSpan.Zero) return;
        await Task.Delay(_delayStartUp);
        
        try
        {
            while (!_shutdownCoordinator.ShutdownInitiated)
            {
                await Task.Delay(_checkFrequency);
                if (_shutdownCoordinator.ShutdownInitiated) return;

                var now = DateTime.UtcNow;

                var expiresSoon = await _functionStore
                    .GetPostponedFunctions(
                        _functionTypeId,
                        now.Add(_checkFrequency).Ticks
                    );

                foreach (var expireSoon in expiresSoon)
                    _ = SleepAndThenReInvoke(expireSoon, now);
            }
        }
        catch (Exception innerException)
        {
            _unhandledExceptionHandler.Invoke(
                new FrameworkException(
                    _functionTypeId,
                    $"{nameof(PostponedWatchdog)} failed while executing: '{_functionTypeId}'",
                    innerException
                )
            );
        }
    }

    private async Task SleepAndThenReInvoke(StoredPostponedFunction spf, DateTime now)
    {
        var functionId = new FunctionId(_functionTypeId, spf.InstanceId);
        if (_shutdownCoordinator.ShutdownInitiated) return;
        
        var postponedUntil = new DateTime(spf.PostponedUntil, DateTimeKind.Utc);
        var delay = TimeSpanHelper.Max(postponedUntil - now, TimeSpan.Zero);
        await Task.Delay(delay);

        if (_shutdownCoordinator.ShutdownInitiated) return;

        _workQueue.Enqueue(
            functionId.InstanceId.ToString(),
            async () =>
            {
                try
                {
                    while (DateTime.UtcNow < postponedUntil) //clock resolution means that we might wake up early 
                        await Task.Yield();
                    
                    using var _ = _shutdownCoordinator.RegisterRunningRFunc();
                    var success = await _functionStore.TryToBecomeLeader(
                        functionId,
                        Status.Executing,
                        expectedEpoch: spf.Epoch,
                        newEpoch: spf.Epoch + 1
                    );
                    if (!success) return;
            
                    await _reInvoke(
                        spf.InstanceId,
                        expectedStatuses: new[] {Status.Executing},
                        expectedEpoch: spf.Epoch + 1
                    );
                }
                catch (ObjectDisposedException) {} //ignore when rfunctions has been disposed
                catch (UnexpectedFunctionState) {} //ignore when the functions state has changed since fetching it
                catch (FunctionInvocationPostponedException) {}
                catch (Exception innerException)
                {
                    _unhandledExceptionHandler.Invoke(
                        new FrameworkException(
                            _functionTypeId,
                            $"{nameof(PostponedWatchdog)} failed while executing: '{functionId}'",
                            innerException
                        )
                    );
                }
            });
    }
}