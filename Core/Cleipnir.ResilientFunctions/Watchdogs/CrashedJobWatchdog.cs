using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Domain.Exceptions;
using Cleipnir.ResilientFunctions.ExceptionHandling;
using Cleipnir.ResilientFunctions.Helpers;
using Cleipnir.ResilientFunctions.ShutdownCoordination;
using Cleipnir.ResilientFunctions.Storage;

namespace Cleipnir.ResilientFunctions.Watchdogs;

internal class CrashedJobWatchdog
{
    private readonly IFunctionStore _functionStore;
    private readonly TimeSpan _checkFrequency;
    private readonly UnhandledExceptionHandler _unhandledExceptionHandler;
    private readonly ShutdownCoordinator _shutdownCoordinator;

    private readonly Dictionary<string, WatchDogReInvokeFunc> _reInvokeFuncs = new();
    private readonly object _sync = new();

    public CrashedJobWatchdog(
        IFunctionStore functionStore,
        TimeSpan checkFrequency,
        UnhandledExceptionHandler unhandledExceptionHandler,
        ShutdownCoordinator shutdownCoordinator)
    {
        _functionStore = functionStore;
        _checkFrequency = checkFrequency;
        _unhandledExceptionHandler = unhandledExceptionHandler;
        _shutdownCoordinator = shutdownCoordinator;
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
            var prevExecutingFunctions = new Dictionary<FunctionInstanceId, StoredExecutingFunction>();

            while (!_shutdownCoordinator.ShutdownInitiated)
            {
                await Task.Delay(_checkFrequency);
                if (_shutdownCoordinator.ShutdownInitiated) return;

                var currExecutingFunctions = await _functionStore
                    .GetExecutingFunctions("Job")
                    .TaskSelect(l =>
                        l.ToDictionary(
                            s => s.InstanceId,
                            s => s
                        )
                    );

                var hangingFunctions =
                    from prev in prevExecutingFunctions
                    join curr in currExecutingFunctions
                        on (prev.Key, prev.Value.Epoch, prev.Value.SignOfLife) 
                        equals (curr.Key, curr.Value.Epoch, curr.Value.SignOfLife)
                    select prev.Value;

                foreach (var function in hangingFunctions)
                    _ = ReInvokeJob(jobId: function.InstanceId.Value, function.Epoch);

                prevExecutingFunctions = currExecutingFunctions;
            }
        }
        catch (Exception thrownException)
        {
            _unhandledExceptionHandler.Invoke(
                new FrameworkException(
                    "Job",
                    $"{nameof(CrashedWatchdog)} failed while executing",
                    innerException: thrownException
                )
            );
        }
    }
    
    private async Task ReInvokeJob(string jobId, int expectedEpoch)
    {
        if (_shutdownCoordinator.ShutdownInitiated) return;
        WatchDogReInvokeFunc? reInvoke;
        lock (_sync)
            _reInvokeFuncs.TryGetValue(jobId, out reInvoke);
                            
        if (reInvoke == null) return;
                    
        try
        {
            await reInvoke(
                jobId,
                expectedStatuses: new[] {Status.Executing},
                expectedEpoch: expectedEpoch
            );
        }
        catch (ObjectDisposedException) {} //ignore when rfunctions has been disposed
        catch (UnexpectedFunctionState) {} //ignore when the functions state has changed since fetching it
        catch (Exception innerException)
        {
            var functionId = new FunctionId("Job", jobId);
            _unhandledExceptionHandler.Invoke(
                new FrameworkException(
                    "Job",
                    $"{nameof(CrashedJobWatchdog)} failed while executing: '{functionId}'",
                    innerException
                )
            );
        }
    }
}