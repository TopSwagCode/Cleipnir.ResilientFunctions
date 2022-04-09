﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Domain.Exceptions;
using Cleipnir.ResilientFunctions.ExceptionHandling;
using Cleipnir.ResilientFunctions.Helpers;
using Cleipnir.ResilientFunctions.InnerDecorators;
using Cleipnir.ResilientFunctions.ParameterSerialization;
using Cleipnir.ResilientFunctions.Storage;
using Cleipnir.ResilientFunctions.Tests.Utils;
using Cleipnir.ResilientFunctions.Utils.Scrapbooks;
using Shouldly;

namespace Cleipnir.ResilientFunctions.Tests.TestTemplates.RFunctionTests;

public abstract class UnhandledFuncExceptionTests
{
    public abstract Task UnhandledExceptionResultsInPostponedFunc();
    protected async Task UnhandledExceptionResultsInPostponedFunc(Task<IFunctionStore> storeTask)
    {
        var functionType = "SomeFunctionType".ToFunctionTypeId();
        var store = await storeTask;
        var rFunctions = new RFunctions(store);
        var syncedException = new Synced<Exception>();
        var rFunc = rFunctions.Register<string, string>(
            functionType,
            inner: _ => throw new Exception("oh no"),
            preInvoke: null,
            postInvoke: async (result, metadata) =>
            {
                await Task.CompletedTask;
                syncedException.Value = result.Fail!;
                return Postpone.Until(
                    new DateTime(3000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    inProcessWait: false
                );
            }
        );

        FunctionInvocationPostponedException? thrownException = null;
        try
        {
            await rFunc.Invoke("1", "1");
        }
        catch (FunctionInvocationPostponedException exception)
        {
            thrownException = exception;
        }

        thrownException.ShouldNotBeNull();
        thrownException.PostponedUntil.ShouldBe(new DateTime(3000,1,1, 0, 0, 0, DateTimeKind.Utc));
        
        var sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.PostponedUntil.ShouldNotBeNull();

        //schedule
        await rFunc.Schedule("2", "2");
        await BusyWait.Until(
            () => store
                .GetFunction(new FunctionId(functionType, "2"))
                .Map(f => f?.PostponedUntil != null)
        );
        
        //re-invoke
        thrownException = null;
        try
        {
            await rFunc.ReInvoke("1", new[] {Status.Postponed});
        }
        catch (FunctionInvocationPostponedException exception)
        {
            thrownException = exception;
        }

        thrownException.ShouldNotBeNull();
        thrownException.PostponedUntil.ShouldBe(new DateTime(3000,1,1, 0, 0, 0, DateTimeKind.Utc));
        sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.PostponedUntil.ShouldNotBeNull();
    }

    public abstract Task UnhandledExceptionResultsInPostponedFuncWithScrapbook();
    protected async Task UnhandledExceptionResultsInPostponedFuncWithScrapbook(Task<IFunctionStore> storeTask)
    {
        var functionType = "SomeFunctionType".ToFunctionTypeId();
        var store = await storeTask;
        var rFunctions = new RFunctions(store);
        var syncedException = new Synced<Exception>();
        var rFunc = rFunctions.Register<string, ListScrapbook<string>, string>(
            functionType,
            (_, _) => throw new Exception("oh no"),
            preInvoke: null,
            postInvoke: async (result, scrapbook, metadata) =>
            {
                await Task.CompletedTask;
                syncedException.Value = result.Fail!;
                scrapbook.List.Add("onException");
                return Postpone.Until(
                    new DateTime(3000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    inProcessWait: false
                );
            }
        );

        //invoke
        FunctionInvocationPostponedException? thrownException = null;
        try
        {
            await rFunc.Invoke("1", "1");
        }
        catch (FunctionInvocationPostponedException exception)
        {
            thrownException = exception;
        }

        thrownException.ShouldNotBeNull();
        thrownException.PostponedUntil.ShouldBe(new DateTime(3000,1,1, 0, 0, 0, DateTimeKind.Utc));
        var sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.PostponedUntil.ShouldNotBeNull();
        sf.Scrapbook!
            .Deserialize(DefaultSerializer.Instance)
            .CastTo<ListScrapbook<string>>()
            .List
            .Single()
            .ShouldBe("onException");

        //schedule
        await rFunc.Schedule("2", "2");
        await BusyWait.Until(
            () => store
                .GetFunction(new FunctionId(functionType, "2"))
                .Map(f => f?.PostponedUntil != null)
        );
        sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.Scrapbook!
            .Deserialize(DefaultSerializer.Instance)
            .CastTo<ListScrapbook<string>>()
            .List
            .Single()
            .ShouldBe("onException");
        
        //re-invoke
        thrownException = null;
        try
        {
            await rFunc.ReInvoke("1", new[] {Status.Postponed});
        }
        catch (FunctionInvocationPostponedException exception)
        {
            thrownException = exception;
        }

        thrownException.ShouldNotBeNull();
        thrownException.PostponedUntil.ShouldBe(new DateTime(3000,1,1, 0, 0, 0, DateTimeKind.Utc));
        sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.PostponedUntil.ShouldNotBeNull();
        sf.Scrapbook!
            .Deserialize(DefaultSerializer.Instance)
            .CastTo<ListScrapbook<string>>()
            .List
            .Count
            .ShouldBe(2);
    }

    public abstract Task UnhandledExceptionResultsInPostponedAction();
    protected async Task UnhandledExceptionResultsInPostponedAction(Task<IFunctionStore> storeTask)
    {
        var functionType = "SomeFunctionType".ToFunctionTypeId();
        var store = await storeTask;
        var rFunctions = new RFunctions(store);
        var syncedException = new Synced<Exception>();
        var rAction = rFunctions
            .RegisterAction(
                functionType,
                OnFailure.PostponeUntil(
                    void (string param) => throw new Exception("oh no"),
                    new DateTime(3000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    onException: exception => syncedException.Value = exception
                )
            );

        //invoke
        var thrown = false;
        try
        {
            await rAction.Invoke("1", "1");
        }
        catch (FunctionInvocationPostponedException e)
        {
            e.PostponedUntil.ShouldBe(new DateTime(3000,1,1, 0, 0, 0, DateTimeKind.Utc));
            thrown = true;
        } 
        thrown.ShouldBeTrue();

        var sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.PostponedUntil.ShouldNotBeNull();

        //schedule
        await rAction.Schedule("2", "2");
        await BusyWait.Until(
            () => store
                .GetFunction(new FunctionId(functionType, "2"))
                .Map(f => f?.PostponedUntil != null)
        );
        
        //re-invoke
        thrown = false;
        try
        {
            await rAction.ReInvoke("1", new[] {Status.Postponed});
        }
        catch (FunctionInvocationPostponedException e)
        {
            e.PostponedUntil.ShouldBe(new DateTime(3000,1,1, 0, 0, 0, DateTimeKind.Utc));
            thrown = true;
        } 
        thrown.ShouldBeTrue();
        
        sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.PostponedUntil.ShouldNotBeNull();
    }

    public abstract Task UnhandledExceptionResultsInPostponedActionWithScrapbook();
    protected async Task UnhandledExceptionResultsInPostponedActionWithScrapbook(Task<IFunctionStore> storeTask)
    {
        var functionType = "SomeFunctionType".ToFunctionTypeId();
        var store = await storeTask;
        var rFunctions = new RFunctions(store);
        var syncedException = new Synced<Exception>();
        var rFunc = rFunctions.Register<string, ListScrapbook<string>>(
            functionType,
            (_, _) => throw new Exception("oh no"),
            preInvoke: null,
            postInvoke: async (result, scrapbook, metadata) =>
            {
                await Task.CompletedTask;
                syncedException.Value = result.Fail!;
                scrapbook.List.Add("onException");
                return Postpone.Until(
                    new DateTime(3000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    inProcessWait: false
                );
            }
        );

        //invoke
        var thrown = false;
        try
        {
            await rFunc.Invoke("1", "1");
        }
        catch (FunctionInvocationPostponedException e)
        {
            e.PostponedUntil.ShouldBe(new DateTime(3000,1,1, 0, 0, 0, DateTimeKind.Utc));
            thrown = true;
        }
        thrown.ShouldBeTrue();
        
        var sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.PostponedUntil.ShouldNotBeNull();
        sf.Scrapbook!
            .Deserialize(DefaultSerializer.Instance)
            .CastTo<ListScrapbook<string>>()
            .List
            .Single()
            .ShouldBe("onException");

        //schedule
        await rFunc.Schedule("2", "2");
        await BusyWait.Until(
            () => store
                .GetFunction(new FunctionId(functionType, "2"))
                .Map(f => f?.PostponedUntil != null)
        );
        sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.Scrapbook!
            .Deserialize(DefaultSerializer.Instance)
            .CastTo<ListScrapbook<string>>()
            .List
            .Single()
            .ShouldBe("onException");
        
        //re-invoke
        thrown = false;
        try
        {
            await rFunc.ReInvoke("1", new[] {Status.Postponed});
        }
        catch (FunctionInvocationPostponedException e)
        {
            thrown = true;
            e.PostponedUntil.ShouldBe(new DateTime(3000,1,1, 0, 0, 0, DateTimeKind.Utc));
        }
        thrown.ShouldBeTrue();
        
        sf = await store.GetFunction(new FunctionId(functionType, "1")).ShouldNotBeNullAsync();
        sf.PostponedUntil.ShouldNotBeNull();
        sf.Scrapbook!
            .Deserialize(DefaultSerializer.Instance)
            .CastTo<ListScrapbook<string>>()
            .List
            .Count
            .ShouldBe(2);
    }
}