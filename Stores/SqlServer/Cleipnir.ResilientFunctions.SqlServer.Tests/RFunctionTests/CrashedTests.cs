﻿using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cleipnir.ResilientFunctions.SqlServer.Tests.RFunctionTests;

[TestClass]
public class CrashedTests : ResilientFunctions.Tests.TestTemplates.RFunctionTests.CrashedTests
{
    [TestMethod]
    public override Task NonCompletedFuncIsCompletedByWatchDog()
        => NonCompletedFuncIsCompletedByWatchDog(Sql.AutoCreateAndInitializeStore());

    [TestMethod]
    public override Task NonCompletedFuncWithScrapbookIsCompletedByWatchDog()
        => NonCompletedFuncWithScrapbookIsCompletedByWatchDog(Sql.AutoCreateAndInitializeStore());

    [TestMethod]
    public override Task NonCompletedActionIsCompletedByWatchDog()
        => NonCompletedActionIsCompletedByWatchDog(Sql.AutoCreateAndInitializeStore());

    [TestMethod]
    public override Task NonCompletedActionWithScrapbookIsCompletedByWatchDog()
        => NonCompletedActionWithScrapbookIsCompletedByWatchDog(Sql.AutoCreateAndInitializeStore());
}