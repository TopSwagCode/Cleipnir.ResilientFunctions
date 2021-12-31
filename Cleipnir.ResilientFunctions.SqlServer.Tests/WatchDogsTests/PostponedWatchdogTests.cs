using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cleipnir.ResilientFunctions.SqlServer.Tests.WatchDogsTests
{
    [TestClass]
    public class PostponedWatchdogTests : ResilientFunctions.Tests.TestTemplates.WatchDogsTests.PostponedWatchdogTests
    {
        [TestMethod]
        public override Task PostponedFunctionInvocationIsCompletedByWatchDog()
            => PostponedFunctionInvocationIsCompletedByWatchDog(Sql.AutoCreateAndInitializeStore());

        [TestMethod]
        public override Task PostponedFunctionWithScrapbookInvocationIsCompletedByWatchDog()
            => PostponedFunctionWithScrapbookInvocationIsCompletedByWatchDog(Sql.AutoCreateAndInitializeStore());

        [TestMethod]
        public override Task PostponedActionInvocationIsCompletedByWatchDog()
            => PostponedActionInvocationIsCompletedByWatchDog(Sql.AutoCreateAndInitializeStore());

        [TestMethod]
        public override Task PostponedActionWithScrapbookInvocationIsCompletedByWatchDog()
            => PostponedActionWithScrapbookInvocationIsCompletedByWatchDog(Sql.AutoCreateAndInitializeStore());
    }
}