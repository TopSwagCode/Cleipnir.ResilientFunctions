﻿using System;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Helpers;
using Cleipnir.ResilientFunctions.Storage;
using Cleipnir.ResilientFunctions.Tests.Utils;
using Newtonsoft.Json;
using Shouldly;

namespace Cleipnir.ResilientFunctions.Tests.TestTemplates.WatchDogsTests;

public abstract class InitialInvocationFailedTests
{
    public abstract Task CreatedActionIsCompletedByWatchdog();
    protected async Task CreatedActionIsCompletedByWatchdog(Task<IFunctionStore> storeTask)
    {
        var store = await storeTask;
        var functionId = new FunctionId(
            nameof(CreatedActionIsCompletedByWatchdog),
            nameof(CreatedActionIsCompletedByWatchdog)
        );
        await store.CreateFunction(
            functionId,
            param: new StoredParameter("hello world".ToJson(), typeof(string).SimpleQualifiedName()),
            scrapbookType: null,
            initialStatus: Status.Executing,
            initialEpoch: 0,
            initialSignOfLife: 0
        );

        var flag = new SyncedFlag();
        using var rFunctions = new RFunctions(store, new Settings(CrashedCheckFrequency: TimeSpan.FromMilliseconds(5)));
        _ = rFunctions.RegisterAction(
            functionId.TypeId,
            void(string param) => flag.Raise()
        );

        await flag.WaitForRaised();

        await BusyWait.Until(
            () => store.GetFunction(functionId).Map(sf => sf?.Status == Status.Succeeded)
        );
    }

    public abstract Task CreatedActionWithScrapbookIsCompletedByWatchdog();
    protected async Task CreatedActionWithScrapbookIsCompletedByWatchdog(Task<IFunctionStore> storeTask)
    {
        var store = await storeTask;
        var functionId = new FunctionId(
            nameof(CreatedActionWithScrapbookIsCompletedByWatchdog),
            nameof(CreatedActionWithScrapbookIsCompletedByWatchdog)
        );
        await store.CreateFunction(
            functionId,
            param: new StoredParameter("hello world".ToJson(), typeof(string).SimpleQualifiedName()),
            scrapbookType: typeof(Scrapbook).SimpleQualifiedName(),
            initialStatus: Status.Executing,
            initialEpoch: 0,
            initialSignOfLife: 0
        );

        var flag = new SyncedFlag();
        using var rFunctions = new RFunctions(store, new Settings(CrashedCheckFrequency: TimeSpan.FromMilliseconds(5)));
        _ = rFunctions.RegisterAction<string, Scrapbook>(
            functionId.TypeId,
            void(string param, Scrapbook scrapbook) => flag.Raise()
        );

        await flag.WaitForRaised();

        await BusyWait.Until(
            () => store.GetFunction(functionId).Map(sf => sf?.Status == Status.Succeeded)
        );
        var scrapbook = await store.GetFunction(functionId).Map(sf => sf?.Scrapbook);
        scrapbook.ShouldNotBeNull();
        scrapbook.ScrapbookJson.ShouldNotBeNull();
    }

    public abstract Task CreatedFuncIsCompletedByWatchdog();
    public async Task CreatedFuncIsCompletedByWatchdog(Task<IFunctionStore> storeTask)
    {
        var store = await storeTask;
        var functionId = new FunctionId(
            nameof(CreatedFuncIsCompletedByWatchdog),
            nameof(CreatedFuncIsCompletedByWatchdog)
        );
        await store.CreateFunction(
            functionId,
            param: new StoredParameter("hello world".ToJson(), typeof(string).SimpleQualifiedName()),
            scrapbookType: null,
            initialStatus: Status.Executing,
            initialEpoch: 0,
            initialSignOfLife: 0
        );

        var flag = new SyncedFlag();
        using var rFunctions = new RFunctions(store, new Settings(CrashedCheckFrequency: TimeSpan.FromMilliseconds(5)));
        _ = rFunctions.RegisterFunc(
            functionId.TypeId,
            string (string param) =>
            {
                flag.Raise();
                return param.ToUpper();
            });

        await flag.WaitForRaised();

        await BusyWait.Until(
            () => store.GetFunction(functionId).Map(sf => sf?.Status == Status.Succeeded)
        );
        var resultJson = await store.GetFunction(functionId).Map(sf => sf?.Result?.ResultJson);
        resultJson.ShouldNotBeNull();
        JsonConvert.DeserializeObject<string>(resultJson).ShouldBe("HELLO WORLD");
    }

    public abstract Task CreatedFuncWithScrapbookIsCompletedByWatchdog();
    protected async Task CreatedFuncWithScrapbookIsCompletedByWatchdog(Task<IFunctionStore> storeTask)
    {
        var store = await storeTask;
        var functionId = new FunctionId(
            nameof(CreatedFuncWithScrapbookIsCompletedByWatchdog),
            nameof(CreatedFuncWithScrapbookIsCompletedByWatchdog)
        );
        await store.CreateFunction(
            functionId,
            param: new StoredParameter("hello world".ToJson(), typeof(string).SimpleQualifiedName()),
            scrapbookType: typeof(Scrapbook).SimpleQualifiedName(),
            initialStatus: Status.Executing,
            initialEpoch: 0,
            initialSignOfLife: 0
        );

        var flag = new SyncedFlag();
        using var rFunctions = new RFunctions(store, new Settings(CrashedCheckFrequency: TimeSpan.FromMilliseconds(5)));
        _ = rFunctions.RegisterFunc(
            functionId.TypeId,
            string (string param, Scrapbook scrapbook) =>
            {
                flag.Raise();
                return param.ToUpper();
            });

        await flag.WaitForRaised();

        await BusyWait.Until(
            () => store.GetFunction(functionId).Map(sf => sf?.Status == Status.Succeeded)
        );
        var scrapbook = await store.GetFunction(functionId).Map(sf => sf?.Scrapbook);
        scrapbook.ShouldNotBeNull();
        scrapbook.ScrapbookJson.ShouldNotBeNull();
        
        var resultJson = await store.GetFunction(functionId).Map(sf => sf?.Result?.ResultJson);
        resultJson.ShouldNotBeNull();
        JsonConvert.DeserializeObject<string>(resultJson).ShouldBe("HELLO WORLD");
    }
    
    private class Scrapbook : RScrapbook {}
}