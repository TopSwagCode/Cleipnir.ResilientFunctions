﻿using System;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions;

namespace ConsoleApp;

public class ApiCaller
{
    private readonly bool _shouldFail;
    private readonly int _service;

    public ApiCaller(bool shouldFail, int service)
    {
        _shouldFail = shouldFail;
        _service = service;
    }

    public async Task<RResult<string>> CallApi(string input)
    {
        Console.WriteLine($"[SERVICE{_service}] Executing CallApi");
        await Task.Delay(1_000);
        if (_shouldFail)
        {
            Console.WriteLine($"[SERVICE{_service}] Throwing Exception");
            throw new Exception("api call failed");
        }
                
        return "output";
    }
}