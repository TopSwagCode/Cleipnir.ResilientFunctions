﻿using System;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Storage;
using Cleipnir.ResilientFunctions.Tests.Utils;

namespace ConsoleApp.Job;

public class Example
{
    public static async Task RegisterAndStart()
    {
        var store = new InMemoryFunctionStore();
        var rFunctions = new RFunctions(
            store,
            postponedCheckFrequency: TimeSpan.FromMilliseconds(1_000),
            unhandledExceptionHandler: Console.WriteLine
        );
        var registration = rFunctions
            .RegisterJob<Scrapbook>("SampleJob")
            .WithInner(Job)
            .Create();

        await registration.Start();

        Console.ReadLine();
    }

    private static Return Job(Scrapbook scrapbook)
    {
        Console.WriteLine("Executing Job with scrapbook: " + Environment.NewLine + scrapbook.ToJson());
        var now = DateTime.UtcNow;
        if (now < scrapbook.ExecuteNext)
        {
            Console.WriteLine("Not executing as next execution is in the future");
            return Postpone.Until(scrapbook.ExecuteNext!, inProcessWait: false);            
        }
        
        scrapbook.ExecuteNext = now.AddSeconds(3);
        Console.WriteLine("Job Executed");
        return Postpone.Until(scrapbook.ExecuteNext, inProcessWait: false);
    }

    private class Scrapbook : RScrapbook
    {
        public DateTime ExecuteNext { get; set; }
    }
}