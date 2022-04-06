﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.ExceptionHandling;

namespace Cleipnir.ResilientFunctions.Invocation;

public class RActionInvoker<TParam> where TParam : notnull
{
    private readonly FunctionTypeId _functionTypeId;
    private readonly Func<TParam, Task<Return>> _inner;

    private readonly CommonInvoker _commonInvoker;
    private readonly UnhandledExceptionHandler _unhandledExceptionHandler;
    private readonly Func<Metadata<TParam>, Task> _preInvoke;
    private readonly Func<Return, Metadata<TParam>, Task<Return>> _postInvoke;

    internal RActionInvoker(
        FunctionTypeId functionTypeId,
        Func<TParam, Task<Return>> inner,
        CommonInvoker commonInvoker,
        UnhandledExceptionHandler unhandledExceptionHandler,
        Func<Metadata<TParam>, Task> preInvoke,
        Func<Return, Metadata<TParam>, Task<Return>> postInvoke)
    {
        _functionTypeId = functionTypeId;
        _inner = inner;
        _commonInvoker = commonInvoker;
        _unhandledExceptionHandler = unhandledExceptionHandler;
        _preInvoke = preInvoke;
        _postInvoke = postInvoke;
    }

    public async Task Invoke(string functionInstanceId, TParam param)
    {
        var functionId = new FunctionId(_functionTypeId, functionInstanceId);
        var created = await PersistNewFunctionInStore(functionId, param);
        if (!created) { await WaitForActionResult(functionId); return; }
        var metadata = new Metadata<TParam>(functionId, param);

        using var _ = CreateSignOfLifeAndRegisterRunningFunction(functionId);
        while (true)
        {
            Return returned;
            try
            {
                await _preInvoke(metadata);
                // *** USER FUNCTION INVOCATION *** 
                returned = await _inner(param);
                returned = await _postInvoke(returned, metadata);
            }
            catch (Exception exception)
            {
                returned = await _postInvoke(new Fail(exception), metadata);
                if (returned.Fail == exception)
                {
                    await PersistPostInvoked(functionId, returned);
                    throw;
                }
            }

            if (await PersistResultAndEnsureSuccess(functionId, returned) == InProcessWait.DoNotRetryInvocation)
                return;
        }
    }

    public async Task ScheduleInvocation(string functionInstanceId, TParam param)
    {
        var functionId = new FunctionId(_functionTypeId, functionInstanceId);
        var created = await PersistNewFunctionInStore(functionId, param);
        if (!created) return;
        var metadata = new Metadata<TParam>(functionId, param);

        _ = Task.Run(async () =>
        {
            using var _ = CreateSignOfLifeAndRegisterRunningFunction(functionId);
            try
            {
                while (true)
                {
                    Return returned;
                    try
                    {
                        await _preInvoke(metadata);
                        // *** USER FUNCTION INVOCATION *** 
                        returned = await _inner(param);
                        returned = await _postInvoke(returned, metadata);
                    }
                    catch (Exception exception)
                    {
                        returned = await _postInvoke(new Fail(exception), metadata);
                        if (returned.Fail == exception)
                        {
                            await PersistPostInvoked(functionId, returned);
                            throw;
                        }
                    }

                    if (await PersistResultAndEnsureSuccess(functionId, returned) == InProcessWait.DoNotRetryInvocation)
                        return;
                }
            }
            catch (Exception exception)
            {
                _unhandledExceptionHandler.Invoke(_functionTypeId, exception);
            }
        });
    }

    public async Task ReInvoke(string instanceId, IEnumerable<Status> expectedStatuses, int? expectedEpoch = null)
    {
        var functionId = new FunctionId(_functionTypeId, instanceId);
        var (param, epoch) = await PrepareForReInvocation(functionId, expectedStatuses, expectedEpoch);
        var metadata = new Metadata<TParam>(functionId, param);

        using var _ = CreateSignOfLifeAndRegisterRunningFunction(functionId, epoch);
        while (true)
        {
            Return returned;
            try
            {
                await _preInvoke(metadata);
                // *** USER FUNCTION INVOCATION *** 
                returned = await _inner(param);
                returned = await _postInvoke(returned, metadata);
            }
            catch (Exception exception)
            {
                returned = await _postInvoke(new Fail(exception), metadata);
                if (returned.Fail == exception)
                {
                    await PersistPostInvoked(functionId, returned, epoch);
                    throw;
                }
            }

            if (await PersistResultAndEnsureSuccess(functionId, returned, epoch) == InProcessWait.DoNotRetryInvocation) 
                return;
        }
    }

    public async Task ScheduleReInvoke(string instanceId, IEnumerable<Status> expectedStatuses, int? expectedEpoch = null)
    {
        var functionId = new FunctionId(_functionTypeId, instanceId);
        var (param, epoch) = await PrepareForReInvocation(functionId, expectedStatuses, expectedEpoch);
        var metadata = new Metadata<TParam>(functionId, param);
            
        _ = Task.Run(async () =>
        {
            using var _ = CreateSignOfLifeAndRegisterRunningFunction(functionId, epoch);
            try
            {
                while (true)
                {
                    Return returned;
                    try
                    {
                        await _preInvoke(metadata);
                        // *** USER FUNCTION INVOCATION *** 
                        returned = await _inner(param);
                        returned = await _postInvoke(returned, metadata);
                    }
                    catch (Exception exception)
                    {
                        returned = await _postInvoke(new Fail(exception), metadata);
                        if (returned.Fail == exception)
                        {
                            await PersistPostInvoked(functionId, returned, epoch);
                            throw;
                        }
                    }

                    if (await PersistResultAndEnsureSuccess(functionId, returned, epoch) == InProcessWait.DoNotRetryInvocation)
                        return;
                }
            }
            catch (Exception exception)
            {
                _unhandledExceptionHandler.Invoke(_functionTypeId, exception);
            }
        });
    }

    private async Task<bool> PersistNewFunctionInStore(FunctionId functionId, TParam param)
        => await _commonInvoker.PersistFunctionInStore(functionId, param, scrapbookType: null);

    private async Task WaitForActionResult(FunctionId functionId)
        => await _commonInvoker.WaitForActionCompletion(functionId);

    private async Task<Tuple<TParam, int>> PrepareForReInvocation(
        FunctionId functionId,
        IEnumerable<Status> expectedStatuses,
        int? expectedEpoch)
        => await _commonInvoker.PrepareForReInvocation<TParam>(functionId, expectedStatuses, expectedEpoch);

    private async Task PersistPostInvoked(FunctionId functionId, Return returned, int expectedEpoch = 0)
        => await _commonInvoker.PersistPostInvoked(functionId, returned, scrapbook: null, expectedEpoch);

    private async Task<InProcessWait> PersistResultAndEnsureSuccess(FunctionId functionId, Return returned,
        int expectedEpoch = 0)
        => await _commonInvoker.PersistResultAndEnsureSuccess(functionId, returned, scrapbook: null, expectedEpoch);

    private IDisposable CreateSignOfLifeAndRegisterRunningFunction(FunctionId functionId, int expectedEpoch = 0)
        => _commonInvoker.CreateSignOfLifeAndRegisterRunningFunction(functionId, expectedEpoch);
}

public class RActionInvoker<TParam, TScrapbook> where TParam : notnull where TScrapbook : RScrapbook, new()
{
    private readonly FunctionTypeId _functionTypeId;
    private readonly InnerAction<TParam, TScrapbook> _inner;
    
    private readonly CommonInvoker _commonInvoker;
    private readonly UnhandledExceptionHandler _unhandledExceptionHandler;
    private readonly RAction.PreInvoke<TParam, TScrapbook> _preInvoke;
    private readonly RAction.PostInvoke<TParam, TScrapbook> _postInvoke;

    internal RActionInvoker(
        FunctionTypeId functionTypeId,
        InnerAction<TParam, TScrapbook> inner,
        CommonInvoker commonInvoker,
        UnhandledExceptionHandler unhandledExceptionHandler,
        RAction.PreInvoke<TParam, TScrapbook>? preInvoke,
        RAction.PostInvoke<TParam, TScrapbook>? postInvoke)
    {
        _functionTypeId = functionTypeId;
        _inner = inner;
        _commonInvoker = commonInvoker;
        _unhandledExceptionHandler = unhandledExceptionHandler;
        _preInvoke = preInvoke ?? CommonInvoker.PreInvokeNoOp;
        _postInvoke = postInvoke ?? CommonInvoker.ActionPostInvokeNoOp;
    }

    public async Task Invoke(string functionInstanceId, TParam param)
    {
        var functionId = new FunctionId(_functionTypeId, functionInstanceId);
        var created = await PersistNewFunctionInStore(functionId, param, typeof(TScrapbook));
        if (!created) { await WaitForActionCompletion(functionId); return; }
        var metadata = new Metadata<TParam>(functionId, param);

        using var _ = CreateSignOfLifeAndRegisterRunningFunction(functionId);
        var scrapbook = CreateScrapbook(functionId);
        while (true)
        {
            Return postInvoked;
            try
            {
                await _preInvoke(scrapbook, metadata);
                // *** USER FUNCTION INVOCATION *** 
                await _inner(param, scrapbook);
                postInvoked = await _postInvoke(Return.Succeed, scrapbook, metadata);
            }
            catch (Exception exception)
            {
                postInvoked = await _postInvoke(new Fail(exception), scrapbook, metadata);
                if (postInvoked.Fail == exception)
                {
                    await PersistPostInvoked(functionId, postInvoked, scrapbook);
                    throw;
                }
            }

            if (await PersistResultAndEnsureSuccess(functionId, postInvoked, scrapbook) == InProcessWait.DoNotRetryInvocation)
                return;
        }
    }
    
    public async Task ScheduleInvocation(string functionInstanceId, TParam param)
    {
        var functionId = new FunctionId(_functionTypeId, functionInstanceId);
        var created = await PersistNewFunctionInStore(functionId, param, typeof(TScrapbook));
        if (!created) return;
        var metadata = new Metadata<TParam>(functionId, param);

        _ = Task.Run(async () =>
        {
            using var _ = CreateSignOfLifeAndRegisterRunningFunction(functionId);
            var scrapbook = CreateScrapbook(functionId);
            try
            {
                while (true)
                {
                    Return postInvoked;
                    try
                    {
                        await _preInvoke(scrapbook, metadata);
                        // *** USER FUNCTION INVOCATION *** 
                        await _inner(param, scrapbook);
                        postInvoked = await _postInvoke(Return.Succeed, scrapbook, metadata);
                    }
                    catch (Exception exception)
                    {
                        postInvoked = await _postInvoke(new Fail(exception), scrapbook, metadata);
                        if (postInvoked.Fail == exception)
                        {
                            await PersistPostInvoked(functionId, postInvoked, scrapbook);
                            throw;
                        }
                    }

                    if (await PersistResultAndEnsureSuccess(functionId, postInvoked, scrapbook) == InProcessWait.DoNotRetryInvocation)
                        return;
                }
            }
            catch (Exception exception)
            {
                _unhandledExceptionHandler.Invoke(_functionTypeId, exception);
            }
        });
    }
    
    public async Task ReInvoke(string instanceId, IEnumerable<Status> expectedStatuses, int? expectedEpoch = null)
    {
        var functionId = new FunctionId(_functionTypeId, instanceId);
        var (param, scrapbook, epoch) = await PrepareForReInvocation(functionId, expectedStatuses, expectedEpoch);
        var metadata = new Metadata<TParam>(functionId, param);

        using var _ = CreateSignOfLifeAndRegisterRunningFunction(functionId, epoch);
        while (true)
        {
            Return postInvoked;
            try
            {
                await _preInvoke(scrapbook, metadata);
                // *** USER FUNCTION INVOCATION *** 
                await _inner(param, scrapbook);
                postInvoked = await _postInvoke(Return.Succeed, scrapbook, metadata);
            }
            catch (Exception exception)
            {
                postInvoked = await _postInvoke(new Fail(exception), scrapbook, metadata);
                if (postInvoked.Fail == exception)
                {
                    await PersistPostInvoked(functionId, postInvoked, scrapbook, epoch);
                    throw;
                }
            }

            if (await PersistResultAndEnsureSuccess(functionId, postInvoked, scrapbook, epoch) == InProcessWait.DoNotRetryInvocation)
                return;
        }
    }
    
    public async Task ScheduleReInvoke(string instanceId, IEnumerable<Status> expectedStatuses, int? expectedEpoch = null)
    {
        var functionId = new FunctionId(_functionTypeId, instanceId);
        var (param, scrapbook, epoch) = await PrepareForReInvocation(functionId, expectedStatuses, expectedEpoch);
        var metadata = new Metadata<TParam>(functionId, param);
            
        _ = Task.Run(async () =>
        {
            using var _ = CreateSignOfLifeAndRegisterRunningFunction(functionId, epoch);
            try
            {
                while (true)
                {
                    Return postInvoked;
                    try
                    {
                        await _preInvoke(scrapbook, metadata);
                        // *** USER FUNCTION INVOCATION *** 
                        await _inner(param, scrapbook);
                        postInvoked = await _postInvoke(Return.Succeed, scrapbook, metadata);
                    }
                    catch (Exception exception)
                    {
                        postInvoked = await _postInvoke(new Fail(exception), scrapbook, metadata);
                        if (postInvoked.Fail == exception)
                        {
                            await PersistPostInvoked(functionId, postInvoked, scrapbook, epoch);
                            throw;
                        }
                    }

                    if (await PersistResultAndEnsureSuccess(functionId, postInvoked, scrapbook, epoch) == InProcessWait.DoNotRetryInvocation)
                        return;
                }
            }
            catch (Exception exception)
            {
                _unhandledExceptionHandler.Invoke(_functionTypeId, exception);
            }
        });
    }

    private TScrapbook CreateScrapbook(FunctionId functionId, int epoch = 0)
        => _commonInvoker.CreateScrapbook<TScrapbook>(functionId, epoch);

    private async Task<bool> PersistNewFunctionInStore(FunctionId functionId, TParam param, Type scrapbookType)
        => await _commonInvoker.PersistFunctionInStore(functionId, param, scrapbookType);

    private async Task WaitForActionCompletion(FunctionId functionId)
        => await _commonInvoker.WaitForActionCompletion(functionId);

    private async Task<Tuple<TParam, TScrapbook, int>> PrepareForReInvocation(FunctionId functionId, IEnumerable<Status> expectedStatuses, int? expectedEpoch)
        => await _commonInvoker.PrepareForReInvocation<TParam, TScrapbook>(functionId, expectedStatuses, expectedEpoch ?? 0);

    private async Task PersistPostInvoked(FunctionId functionId, Return returned, RScrapbook scrapbook, int expectedEpoch = 0)
        => await _commonInvoker.PersistPostInvoked(functionId, returned, scrapbook, expectedEpoch);

    private async Task<InProcessWait> PersistResultAndEnsureSuccess(FunctionId functionId, Return returned, RScrapbook scrapbook, int expectedEpoch = 0)
        => await _commonInvoker.PersistResultAndEnsureSuccess(functionId, returned, scrapbook, expectedEpoch);

    private IDisposable CreateSignOfLifeAndRegisterRunningFunction(FunctionId functionId, int expectedEpoch = 0)
        => _commonInvoker.CreateSignOfLifeAndRegisterRunningFunction(functionId, expectedEpoch);
}

