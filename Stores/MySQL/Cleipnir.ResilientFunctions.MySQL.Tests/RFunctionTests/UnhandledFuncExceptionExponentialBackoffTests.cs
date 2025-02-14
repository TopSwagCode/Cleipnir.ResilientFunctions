﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cleipnir.ResilientFunctions.MySQL.Tests.RFunctionTests;

[TestClass]
public class UnhandledFuncExceptionExponentialBackoffTests : Cleipnir.ResilientFunctions.Tests.TestTemplates.RFunctionTests.UnhandledFuncExceptionExponentialBackoffTests
{
    [TestMethod]
    public override Task UnhandledExceptionResultsInPostponedFunc()
        => UnhandledExceptionResultsInPostponedFunc(Sql.AutoCreateAndInitializeStore());
}