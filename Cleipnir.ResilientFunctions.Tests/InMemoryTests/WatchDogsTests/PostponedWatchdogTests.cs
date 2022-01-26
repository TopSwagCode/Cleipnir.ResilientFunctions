using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cleipnir.ResilientFunctions.Tests.InMemoryTests.WatchDogsTests
{
    [TestClass]
    public class PostponedWatchdogTests : TestTemplates.WatchDogsTests.PostponedWatchdogTests
    {
        [TestMethod]
        public override Task PostponedFunctionInvocationIsCompletedByWatchDog()
            => PostponedFunctionInvocationIsCompletedByWatchDog(new InMemoryFunctionStore());

        [TestMethod]
        public override Task PostponedFunctionWithScrapbookInvocationIsCompletedByWatchDog()
            => PostponedFunctionWithScrapbookInvocationIsCompletedByWatchDog(new InMemoryFunctionStore());

        [TestMethod]
        public override Task PostponedActionInvocationIsCompletedByWatchDog()
            => PostponedActionInvocationIsCompletedByWatchDog(new InMemoryFunctionStore());

        [TestMethod]
        public override Task PostponedActionWithScrapbookInvocationIsCompletedByWatchDog()
            => PostponedActionWithScrapbookInvocationIsCompletedByWatchDog(new InMemoryFunctionStore());

        [TestMethod]
        public override Task MultiplePostponedFunctionsAreInvokedOrderedByTheirDueTime()
            => MultiplePostponedFunctionsAreInvokedOrderedByTheirDueTime(new InMemoryFunctionStore());
    }
}