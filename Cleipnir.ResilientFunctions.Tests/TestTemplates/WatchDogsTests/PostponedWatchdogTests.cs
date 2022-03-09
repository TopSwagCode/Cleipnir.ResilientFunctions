using System;
using System.Linq;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.ExceptionHandling;
using Cleipnir.ResilientFunctions.Helpers;
using Cleipnir.ResilientFunctions.ParameterSerialization;
using Cleipnir.ResilientFunctions.ShutdownCoordination;
using Cleipnir.ResilientFunctions.Storage;
using Cleipnir.ResilientFunctions.Tests.Utils;
using Cleipnir.ResilientFunctions.Watchdogs;
using Cleipnir.ResilientFunctions.Watchdogs.Invocation;
using Shouldly;

namespace Cleipnir.ResilientFunctions.Tests.TestTemplates.WatchDogsTests
{
    public abstract class PostponedWatchdogTests
    {
        private readonly FunctionTypeId _functionTypeId = "functionId".ToFunctionTypeId();
        private readonly FunctionInstanceId _instanceId = "instanceId".ToFunctionInstanceId();
        private FunctionId FunctionId => new FunctionId(_functionTypeId, _instanceId);

        public abstract Task PostponedFunctionInvocationIsCompletedByWatchDog();
        protected async Task PostponedFunctionInvocationIsCompletedByWatchDog(Task<IFunctionStore> storeTask)
        {
            var store = await storeTask;
            var unhandledExceptionCatcher = new UnhandledExceptionCatcher();
            var unhandledExceptionHandler = new UnhandledExceptionHandler(unhandledExceptionCatcher.Catch);
            var syncedScrapbook = new Synced<RScrapbook>();
            
            using var watchDog = new PostponedWatchdog(
                _functionTypeId,
                (param, scrapbook) =>
                {
                    syncedScrapbook.Value = scrapbook;
                    return new Return<object?>(param.ToString()!.ToUpper()).ToTask();
                },
                store,
                new WrapperInnerFuncInvoker(
                    store, 
                    DefaultSerializer.Instance,
                    new NeverExecutingSignOfLifeUpdaterFactory(),
                    unhandledExceptionHandler,
                    new ShutdownCoordinator()
                ),
                checkFrequency: TimeSpan.FromMilliseconds(1),
                unhandledExceptionHandler,
                new ShutdownCoordinator()
            );

            await store.CreateFunction(
                FunctionId,
                param: new StoredParameter("hello".ToJson(), typeof(string).SimpleQualifiedName()),
                scrapbookType: null,
                Status.Executing,
                initialEpoch: 0,
                initialSignOfLife: 0
            ).ShouldBeTrueAsync();

            await store.SetFunctionState(
                FunctionId,
                Status.Postponed,
                scrapbookJson: null,
                result: null,
                errorJson: null,
                postponedUntil: DateTime.UtcNow.AddMinutes(-1).Ticks,
                expectedEpoch: 0
            ).ShouldBeTrueAsync();
            
            _ = watchDog.Start();
            
            await BusyWait.Until(
                async () => await store.GetFunctionsWithStatus(_functionTypeId, Status.Succeeded).Any()
            );

            var storedFunction = await store.GetFunction(FunctionId);
            storedFunction!.Result!.DefaultDeserialize().ShouldBe("HELLO");
            
            unhandledExceptionCatcher.ThrownExceptions.ShouldBeEmpty();
            syncedScrapbook.Value.ShouldBeNull();
        }
        
        public abstract Task PostponedFunctionWithScrapbookInvocationIsCompletedByWatchDog();
        protected async Task PostponedFunctionWithScrapbookInvocationIsCompletedByWatchDog(Task<IFunctionStore> storeTask)
        {
            var store = await storeTask;
            var unhandledExceptionCatcher = new UnhandledExceptionCatcher();
            var unhandledExceptionHandler = new UnhandledExceptionHandler(unhandledExceptionCatcher.Catch);
            
            using var watchDog = new PostponedWatchdog(
                _functionTypeId,
                async (param, scrapbook) =>
                {
                    ((Scrapbook) scrapbook!).Value = 1;
                    await scrapbook.Save();
                    return new Return<object?>(((string) param).ToUpper());
                },
                store,
                new WrapperInnerFuncInvoker(
                    store, 
                    DefaultSerializer.Instance,
                    new NeverExecutingSignOfLifeUpdaterFactory(),
                    unhandledExceptionHandler,
                    new ShutdownCoordinator()
                ),
                checkFrequency: TimeSpan.FromMilliseconds(1),
                unhandledExceptionHandler,
                new ShutdownCoordinator()
            );

            await store.CreateFunction(
                FunctionId,
                param: new StoredParameter("hello".ToJson(), typeof(string).SimpleQualifiedName()),
                scrapbookType: typeof(Scrapbook).SimpleQualifiedName(),
                Status.Executing,
                initialEpoch: 0,
                initialSignOfLife: 0
            ).ShouldBeTrueAsync();

            await store.SetFunctionState(
                FunctionId,
                Status.Postponed,
                new Scrapbook().ToJson(),
                result: null,
                errorJson: null,
                postponedUntil: DateTime.UtcNow.AddMinutes(-1).Ticks,
                expectedEpoch: 0
            ).ShouldBeTrueAsync();
            
            _ = watchDog.Start();

            await BusyWait.Until(
                async () => await store.GetFunction(FunctionId).Map(sf => sf!.Status) == Status.Succeeded
            );

            var storedFunction = await store.GetFunction(FunctionId);
            storedFunction!.Result!.DefaultDeserialize().ShouldBe("HELLO");

            storedFunction.Scrapbook.ShouldNotBeNull();
            storedFunction.Scrapbook.DefaultDeserialize().CastTo<Scrapbook>().Value.ShouldBe(1);
            
            unhandledExceptionCatcher.ThrownExceptions.ShouldBeEmpty();
        }
        
        public abstract Task PostponedActionInvocationIsCompletedByWatchDog();
        protected async Task PostponedActionInvocationIsCompletedByWatchDog(Task<IFunctionStore> storeTask)
        {
            var store = await storeTask;
            var unhandledExceptionCatcher = new UnhandledExceptionCatcher();
            var unhandledExceptionHandler = new UnhandledExceptionHandler(unhandledExceptionCatcher.Catch);
            var syncedParam = new Synced<string>();
            var syncedScrapbook = new Synced<RScrapbook>();
            
            using var watchDog = new PostponedWatchdog(
                _functionTypeId,
                (param, scrapbook) =>
                {
                    syncedScrapbook.Value = scrapbook;
                    syncedParam.Value = (string) param;
                    return new Return<object?>(succeedWithValue: null).ToTask();
                },
                store,
                new WrapperInnerFuncInvoker(
                    store, 
                    DefaultSerializer.Instance,
                    new NeverExecutingSignOfLifeUpdaterFactory(),
                    unhandledExceptionHandler,
                    new ShutdownCoordinator()
                ),
                checkFrequency: TimeSpan.FromMilliseconds(1),
                unhandledExceptionHandler,
                new ShutdownCoordinator()
            );

            await store.CreateFunction(
                FunctionId,
                param: new StoredParameter("hello".ToJson(), typeof(string).SimpleQualifiedName()),
                scrapbookType: null,
                Status.Executing,
                initialEpoch: 0,
                initialSignOfLife: 0
            ).ShouldBeTrueAsync();

            await store.SetFunctionState(
                FunctionId,
                Status.Postponed,
                scrapbookJson: null,
                result: null,
                errorJson: null,
                postponedUntil: DateTime.UtcNow.AddMinutes(-1).Ticks,
                expectedEpoch: 0
            ).ShouldBeTrueAsync();
            
            _ = watchDog.Start();
            
            await BusyWait.Until(
                async () => await store.GetFunctionsWithStatus(_functionTypeId, Status.Succeeded).Any()
            );

            var storedFunction = await store.GetFunction(FunctionId);
            storedFunction.ShouldNotBeNull();
            storedFunction.Result.ShouldBeNull();
            storedFunction.Scrapbook.ShouldBeNull();
            
            unhandledExceptionCatcher.ThrownExceptions.ShouldBeEmpty();
            syncedParam.Value.ShouldBe("hello");
            syncedScrapbook.Value.ShouldBeNull();
        }
        
        public abstract Task PostponedActionWithScrapbookInvocationIsCompletedByWatchDog();
        protected async Task PostponedActionWithScrapbookInvocationIsCompletedByWatchDog(Task<IFunctionStore> storeTask)
        {
            var store = await storeTask;
            var unhandledExceptionCatcher = new UnhandledExceptionCatcher();
            var unhandledExceptionHandler = new UnhandledExceptionHandler(unhandledExceptionCatcher.Catch);
            var syncedParam = new Synced<object>();
            
            using var watchDog = new PostponedWatchdog(
                _functionTypeId,
                async (param, scrapbook) =>
                {
                    syncedParam.Value = param;
                    ((Scrapbook) scrapbook!).Value = 1;
                    await scrapbook.Save();
                    return new Return<object?>(succeedWithValue: null);
                },
                store,
                new WrapperInnerFuncInvoker(
                    store, 
                    DefaultSerializer.Instance,
                    new NeverExecutingSignOfLifeUpdaterFactory(),
                    unhandledExceptionHandler,
                    new ShutdownCoordinator()
                ),
                checkFrequency: TimeSpan.FromMilliseconds(1),
                unhandledExceptionHandler,
                new ShutdownCoordinator()
            );

            await store.CreateFunction(
                FunctionId,
                param: new StoredParameter("hello".ToJson(), typeof(string).SimpleQualifiedName()),
                scrapbookType: typeof(Scrapbook).SimpleQualifiedName(),
                Status.Executing,
                initialEpoch: 0,
                initialSignOfLife: 0
            ).ShouldBeTrueAsync();

            await store.SetFunctionState(
                FunctionId,
                Status.Postponed,
                new Scrapbook().ToJson(),
                result: null,
                errorJson: null,
                postponedUntil: DateTime.UtcNow.AddMinutes(-1).Ticks,
                expectedEpoch: 0
            ).ShouldBeTrueAsync();
            
            _ = watchDog.Start();

            await BusyWait.Until(
                async () => await store.GetFunction(FunctionId).Map(sf => sf!.Status) == Status.Succeeded
            );

            var storedFunction = await store.GetFunction(FunctionId);
            storedFunction!.Result.ShouldBeNull();

            storedFunction.Scrapbook.ShouldNotBeNull();
            storedFunction.Scrapbook.DefaultDeserialize().CastTo<Scrapbook>().Value.ShouldBe(1);
            
            unhandledExceptionCatcher.ThrownExceptions.ShouldBeEmpty();
            (syncedParam.Value is string).ShouldBeTrue();
        }

        public abstract Task MultiplePostponedFunctionsAreInvokedOrderedByTheirDueTime();
        protected async Task MultiplePostponedFunctionsAreInvokedOrderedByTheirDueTime(Task<IFunctionStore> storeTask)
        {
            var store = await storeTask;
            var functionType = nameof(MultiplePostponedFunctionsAreInvokedOrderedByTheirDueTime).ToFunctionTypeId();
            var unhandledExceptionsCatcher = new UnhandledExceptionCatcher();
            using var rFunctions = new RFunctions(
                store,
                unhandledExceptionsCatcher.Catch,
                postponedCheckFrequency: TimeSpan.FromMilliseconds(1)
            );

            var syncedList = new SyncedList<int>();

            var rAction = rFunctions.Register(
                functionType,
                async (int delay, Scrapbook scrapbook) =>
                {
                    if (scrapbook.Value == 1)
                    {
                        syncedList.Add(delay);
                        return Succeed.WithoutValue;
                    }

                    scrapbook.Value = 1;
                    await scrapbook.Save();

                    return Postpone.For(delay);
                }
            ).Invoke;

            _ = rAction(10.ToString(), 10);
            _ = rAction(1000.ToString(), 1000);
            _ = rAction(2000.ToString(), 2000);

            await BusyWait.UntilAsync(
                () => syncedList.Count == 3, 
                checkInterval: TimeSpan.FromMilliseconds(10), 
                maxWait: TimeSpan.FromSeconds(5)
            );

            syncedList
                .SequenceEqual(new[] { 10, 1000, 2000 })
                .ShouldBeTrue($"But was: {string.Join(", ", syncedList)}");

            await BusyWait.Until(async () =>
                await store
                    .GetFunctionsWithStatus(functionType, Status.Succeeded)
                    .Map(e => e.Count()) != 0
            );

            await store
                .GetFunctionsWithStatus(functionType, Status.Succeeded)
                .Map(fs => fs.Select(s => int.Parse(s.InstanceId.Value)))
                .Map(s => s.SequenceEqual(new[] { 10, 1000, 2000 }))
                .ShouldBeTrueAsync();
        }

        private class Scrapbook : RScrapbook
        {
            public int Value { get; set; }
        }
    }
}