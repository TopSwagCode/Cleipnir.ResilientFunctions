using System.Reactive.Linq;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Messaging.Core;
using Shouldly;

namespace Cleipnir.ResilientFunctions.Messaging.PostgreSQL.Tests;

[TestClass]
public class EventSourcesTests
{
    [TestMethod]
    public async Task EventSourcesSunshineScenario()
    {
        var functionId = new FunctionId("TypeId", "InstanceId");
        using var eventStore = await Sql.CreateAndInitializeEventStore();
        var eventSources = new EventSources(eventStore);

        var eventSource = await eventSources.GetEventSource(functionId);

        async Task<object> FirstAsync() => await eventSource.All.FirstAsync();
        var task = FirstAsync();
        
        await Task.Delay(10);
        task.IsCompleted.ShouldBeFalse();

        await eventSource.Emit("hello world");

        (await task).ShouldBe("hello world");
    }
    
    [TestMethod]
    public async Task SecondEventWithExistingIdempotencyKeyIsIgnored()
    {
        var functionId = new FunctionId("TypeId", "InstanceId");
        using var eventStore = await Sql.CreateAndInitializeEventStore();
        var eventSources = new EventSources(eventStore);

        var eventSource = await eventSources.GetEventSource(functionId);

        async Task<IList<object>> TakeTwo() => await eventSource.All.Take(2).ToList();
        var task = TakeTwo();
        
        await Task.Delay(10);
        task.IsCompleted.ShouldBeFalse();

        await eventSource.Emit("hello world", "1");
        await eventSource.Emit("hello world", "1");
        await eventSource.Emit("hello universe");

        task.IsCompletedSuccessfully.ShouldBeTrue();
        task.Result.Count.ShouldBe(2);
        task.Result[0].ShouldBe("hello world");
        task.Result[1].ShouldBe("hello universe");
        
        (await eventStore.GetEvents(functionId, 0)).Count().ShouldBe(3);
    }
}