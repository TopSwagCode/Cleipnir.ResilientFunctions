using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Helpers;
using Cleipnir.ResilientFunctions.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cleipnir.ResilientFunctions.Tests.InMemoryTests.WatchDogsTests
{
    [TestClass]
    public class CrashedWatchdogTests : TestTemplates.WatchDogsTests.CrashedWatchdogTests
    {
        [TestMethod]
        public override Task CrashedFunctionInvocationIsCompletedByWatchDog()
            => CrashedFunctionInvocationIsCompletedByWatchDog(new InMemoryFunctionStore().CastTo<IFunctionStore>().ToTask());

        [TestMethod]
        public override Task CrashedFunctionWithScrapbookInvocationIsCompletedByWatchDog()
            => CrashedFunctionWithScrapbookInvocationIsCompletedByWatchDog(new InMemoryFunctionStore().CastTo<IFunctionStore>().ToTask());

        [TestMethod]
        public override Task CrashedActionInvocationIsCompletedByWatchDog()
            => CrashedActionInvocationIsCompletedByWatchDog(new InMemoryFunctionStore().CastTo<IFunctionStore>().ToTask());

        [TestMethod]
        public override Task CrashedActionWithScrapbookInvocationIsCompletedByWatchDog()
            => CrashedActionWithScrapbookInvocationIsCompletedByWatchDog(new InMemoryFunctionStore().CastTo<IFunctionStore>().ToTask());
    }
}