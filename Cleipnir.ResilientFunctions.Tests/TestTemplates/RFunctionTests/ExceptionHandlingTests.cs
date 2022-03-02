﻿using System;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Storage;
using Cleipnir.ResilientFunctions.Tests.Utils;
using Cleipnir.ResilientFunctions.Utils.Scrapbooks;
using Shouldly;

namespace Cleipnir.ResilientFunctions.Tests.TestTemplates.RFunctionTests;

public abstract class ExceptionHandlingTests
{
    public abstract Task UnhandledExceptionIsRethrownWhenEnsuringSuccessOnFunc();
    protected async Task UnhandledExceptionIsRethrownWhenEnsuringSuccessOnFunc(Task<IFunctionStore> storeTask)
    {
        var store = await storeTask;
        var unhandledExceptionCatcher = new UnhandledExceptionCatcher();
        var rFunctions = new RFunctions(store, unhandledExceptionCatcher.Catch);

        var rFunc = rFunctions.Register<string, string>(
            "typeId".ToFunctionTypeId(),
            param => throw new ArithmeticException("Division by zero")
        ).Invoke;

        var result = await rFunc("instanceId", "hello");
        Should.Throw<ArithmeticException>(result.EnsureSuccess);

        result = await rFunc("instanceId", "hello");

        Should.Throw<PreviousFunctionInvocationException>(result.EnsureSuccess);
    }

    public abstract Task UnhandledExceptionIsRethrownWhenEnsuringSuccessOnFuncWithScrapbook();
    protected async Task UnhandledExceptionIsRethrownWhenEnsuringSuccessOnFuncWithScrapbook(Task<IFunctionStore> storeTask)
    {
        var store = new InMemoryFunctionStore();
        var unhandledExceptionCatcher = new UnhandledExceptionCatcher();
        var rFunctions = new RFunctions(store, unhandledExceptionCatcher.Catch);

        var rFunc = rFunctions.Register<string, ListScrapbook<string>, string>(
            "typeId".ToFunctionTypeId(),
            (param, scrapbook) => throw new ArithmeticException("Division by zero")
        ).Invoke;

        var result = await rFunc("instanceId", "hello");
        Should.Throw<ArithmeticException>(result.EnsureSuccess);

        result = await rFunc("instanceId", "hello");

        Should.Throw<PreviousFunctionInvocationException>(result.EnsureSuccess);
    }

    public abstract Task UnhandledExceptionIsRethrownWhenEnsuringSuccessOnAction();
    protected async Task UnhandledExceptionIsRethrownWhenEnsuringSuccessOnAction(Task<IFunctionStore> storeTask)
    {
        var store = new InMemoryFunctionStore();
        var unhandledExceptionCatcher = new UnhandledExceptionCatcher();
        var rFunctions = new RFunctions(store, unhandledExceptionCatcher.Catch);

        var rFunc = rFunctions.Register<string>(
            "typeId".ToFunctionTypeId(),
            param => throw new ArithmeticException("Division by zero")
        ).Invoke;

        var result = await rFunc("instanceId", "hello");
        Should.Throw<ArithmeticException>(result.EnsureSuccess);

        result = await rFunc("instanceId", "hello");

        Should.Throw<PreviousFunctionInvocationException>(result.EnsureSuccess);
    }

    public abstract Task UnhandledExceptionIsRethrownWhenEnsuringSuccessOnActionWithScrapbook();
    protected async Task UnhandledExceptionIsRethrownWhenEnsuringSuccessOnActionWithScrapbook(Task<IFunctionStore> storeTask)
    {
        var store = new InMemoryFunctionStore();
        var unhandledExceptionCatcher = new UnhandledExceptionCatcher();
        var rFunctions = new RFunctions(store, unhandledExceptionCatcher.Catch);

        var rFunc = rFunctions.Register<string>(
            "typeId".ToFunctionTypeId(),
            param => throw new ArithmeticException("Division by zero")
        ).Invoke;

        var result = await rFunc("instanceId", "hello");
        Should.Throw<ArithmeticException>(result.EnsureSuccess);

        result = await rFunc("instanceId", "hello");

        Should.Throw<PreviousFunctionInvocationException>(result.EnsureSuccess);
    }
}