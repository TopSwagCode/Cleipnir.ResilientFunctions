﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cleipnir.ResilientFunctions.MongoDB.Tests.RFunctionTests;

[TestClass]
public class ReInvocationTests : Cleipnir.ResilientFunctions.Tests.TestTemplates.RFunctionTests.ReInvocationTests
{
    [TestMethod]
    public override Task ActionReInvocationSunshineScenario()
        => ActionReInvocationSunshineScenario(NoSql.AutoCreateAndInitializeStore());
    [TestMethod]
    public override Task ActionWithScrapbookReInvocationSunshineScenario()
        => ActionWithScrapbookReInvocationSunshineScenario(NoSql.AutoCreateAndInitializeStore());
    [TestMethod]
    public override Task ScrapbookUpdaterIsCalledBeforeReInvokeOnAction()
        => ScrapbookUpdaterIsCalledBeforeReInvokeOnAction(NoSql.AutoCreateAndInitializeStore());
    [TestMethod]
    public override Task ScrapbookUpdaterIsCalledBeforeReInvokeOnFunc()
        => ScrapbookUpdaterIsCalledBeforeReInvokeOnFunc(NoSql.AutoCreateAndInitializeStore());
    [TestMethod]
    public override Task FuncReInvocationSunshineScenario()
        => FuncReInvocationSunshineScenario(NoSql.AutoCreateAndInitializeStore());
    [TestMethod]
    public override Task FuncWithScrapbookReInvocationSunshineScenario()
        => FuncWithScrapbookReInvocationSunshineScenario(NoSql.AutoCreateAndInitializeStore());
    [TestMethod]
    public override Task ReInvocationFailsWhenItHasUnexpectedStatus()
        => ReInvocationFailsWhenItHasUnexpectedStatus(NoSql.AutoCreateAndInitializeStore());
    [TestMethod]
    public override Task ReInvocationFailsWhenTheFunctionDoesNotExist()
        => ReInvocationFailsWhenTheFunctionDoesNotExist(NoSql.AutoCreateAndInitializeStore());
}