﻿using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.ParameterSerialization;
using Cleipnir.ResilientFunctions.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Cleipnir.ResilientFunctions.Tests.InMemoryTests.RegistrationTests;

[TestClass]
public class RActionWithScrapbookRegistrationTests
{
    private readonly FunctionTypeId _functionTypeId = new FunctionTypeId("FunctionTypeId");
    private const string FunctionInstanceId = "FunctionInstanceId";
    
    [TestMethod]
    public async Task ConstructedFuncInvokeCanBeCreatedAndInvoked()
    {
        using var rFunctions = CreateRFunctions();
        var rAction = rFunctions
            .RegisterAction<string, Scrapbook>(
                _functionTypeId,
                InnerAction
            )
            .Invoke;

        await rAction(FunctionInstanceId, "hello world");
    }
    
    [TestMethod]
    public async Task ConstructedFuncWithCustomSerializerCanBeCreatedAndInvoked()
    {
        using var rFunctions = CreateRFunctions();
        var serializer = new Serializer();
        var rAction = rFunctions
            .RegisterAction<string, Scrapbook>(
                _functionTypeId,
                InnerAction,
                new Settings(Serializer: serializer)
            )
            .Invoke;

        await rAction(FunctionInstanceId, "hello world");
        serializer.Invoked.ShouldBeTrue();
    }

    private async Task InnerAction(string param, Scrapbook scrapbook) => await Task.CompletedTask;
    private RFunctions CreateRFunctions() => new(new InMemoryFunctionStore());

    private class Serializer : ISerializer
    {
        public bool Invoked { get; set; }
        private ISerializer Default { get; } = DefaultSerializer.Instance;
        public string SerializeParameter(object parameter)
        {
            Invoked = true;
            return Default.SerializeParameter(parameter);
        }

        public object DeserializeParameter(string json, string type) => Default.DeserializeParameter(json, type);

        public string SerializeScrapbook(RScrapbook scrapbook) => Default.SerializeScrapbook(scrapbook);

        public RScrapbook DeserializeScrapbook(string? json, string type) => Default.DeserializeScrapbook(json, type);

        public string SerializeError(RError error) => Default.SerializeError(error);

        public RError DeserializeError(string json) => Default.DeserializeError(json);

        public string SerializeResult(object result) => Default.SerializeResult(result);

        public object DeserializeResult(string json, string type) => Default.DeserializeResult(json, type);
    }
    
    private class Scrapbook : RScrapbook {}
}