﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Domain.Exceptions;
using Cleipnir.ResilientFunctions.ExceptionHandling;
using Cleipnir.ResilientFunctions.ShutdownCoordination;
using Cleipnir.ResilientFunctions.Storage;

namespace Cleipnir.ResilientFunctions.Watchdogs;

internal class PostponedJobWatchdog
{
    private readonly IFunctionStore _functionStore;
    private readonly UnhandledExceptionHandler _unhandledExceptionHandler;
    private readonly TimeSpan _checkFrequency;
    private readonly ShutdownCoordinator _shutdownCoordinator;

    private readonly Dictionary<string, WatchDogReInvokeFunc> _reInvokeFuncs = new();
    private readonly object _sync = new();

    public PostponedJobWatchdog(
        IFunctionStore functionStore,
        TimeSpan checkFrequency,
        UnhandledExceptionHandler unhandledExceptionHandler,
        ShutdownCoordinator shutdownCoordinator)
    {
        _functionStore = functionStore;
        _unhandledExceptionHandler = unhandledExceptionHandler;
        _shutdownCoordinator = shutdownCoordinator;
        _checkFrequency = checkFrequency;
    }
    
    public void AddJob(string jobId, WatchDogReInvokeFunc reInvokeFunc)
    {
        lock (_sync)
        {
            var start = _reInvokeFuncs.Count == 0;
            _reInvokeFuncs[jobId] = reInvokeFunc;
            if (start)
                _ = Start();
        }
    }

    private async Task Start()
    {
        if (_checkFrequency == TimeSpan.Zero) return;

        try
        {
            while (!_shutdownCoordinator.ShutdownInitiated)
            {
                await Task.Delay(_checkFrequency);
                if (_shutdownCoordinator.ShutdownInitiated) return;

                var expires = await _functionStore
                    .GetFunctionsWithStatus("Job", Status.Postponed, DateTime.UtcNow.Ticks);

                foreach (var expired in expires)
                    _ = ReInvokeJob(expired.InstanceId.ToString(), expired.Epoch);
            }
        }
        catch (Exception innerException)
        {
            _unhandledExceptionHandler.Invoke(
                new FrameworkException(
                    "Job",
                    $"{nameof(PostponedJobWatchdog)} failed while executing",
                    innerException
                )
            );
        }
    }

    private async Task ReInvokeJob(string jobId, int expectedEpoch)
    {
        var functionId = new FunctionId("Job", jobId);
        
        if (_shutdownCoordinator.ShutdownInitiated) return;
        WatchDogReInvokeFunc? reInvoke;
        lock (_sync)
            _reInvokeFuncs.TryGetValue(jobId, out reInvoke);
                            
        if (reInvoke == null) return;

        try
        {
            using var _ = _shutdownCoordinator.RegisterRunningRFuncDisposable();
            var success = await _functionStore.TryToBecomeLeader(
                functionId,
                Status.Executing,
                expectedEpoch: expectedEpoch,
                newEpoch: expectedEpoch + 1
            );
            if (!success) return;

            await reInvoke(
                jobId,
                expectedStatuses: new[] {Status.Executing},
                expectedEpoch: expectedEpoch + 1
            );
        }
        catch (ObjectDisposedException) { } //ignore when rfunctions has been disposed
        catch (UnexpectedFunctionState) { } //ignore when the functions state has changed since fetching it
        catch (Exception innerException)
        {
            _unhandledExceptionHandler.Invoke(
                new FrameworkException(
                    "Job",
                    $"{nameof(PostponedJobWatchdog)} failed while executing: '{functionId}'",
                    innerException
                )
            );
        }
    }
}