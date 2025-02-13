﻿namespace Cleipnir.ResilientFunctions.Messaging.SamplesConsoleApp.OrderProcessingFlow.Clients;

public interface IEmailClient
{
    Task SendOrderConfirmation(string email, IEnumerable<string> productIds);
}

public class EmailClientStub : IEmailClient
{
    public Task SendOrderConfirmation(string email, IEnumerable<string> productIds) => Task.CompletedTask;
}