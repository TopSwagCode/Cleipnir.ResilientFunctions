﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cleipnir.ResilientFunctions.MySQL.Tests.WatchDogsTests;

[TestClass]
public class InitialInvocationFailedTests : Cleipnir.ResilientFunctions.Tests.TestTemplates.WatchDogsTests.InitialInvocationFailedTests
{
    [TestMethod]
    public override Task CreatedActionIsCompletedByWatchdog()
        => CreatedActionIsCompletedByWatchdog(Sql.AutoCreateAndInitializeStore());

    [TestMethod]
    public override Task CreatedActionWithScrapbookIsCompletedByWatchdog()
        => CreatedActionWithScrapbookIsCompletedByWatchdog(Sql.AutoCreateAndInitializeStore());

    [TestMethod]
    public override Task CreatedFuncIsCompletedByWatchdog()
        => CreatedFuncIsCompletedByWatchdog(Sql.AutoCreateAndInitializeStore());

    [TestMethod]
    public override Task CreatedFuncWithScrapbookIsCompletedByWatchdog()
        => CreatedFuncWithScrapbookIsCompletedByWatchdog(Sql.AutoCreateAndInitializeStore());
}