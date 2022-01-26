﻿using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Storage;

namespace Cleipnir.ResilientFunctions.Tests.TestTemplates.WatchDogsTests;

public class CrashableFunctionStore : IFunctionStore
{
    private readonly IFunctionStore _inner;
    private volatile bool _crashed;

    private readonly object _sync = new();
    private readonly Subject<SetFunctionStateParams> _subject = new();

    public IObservable<SetFunctionStateParams> AfterSetFunctionStateStream
    {
        get
        {
            lock (_sync)
                return _subject;
        }
    }

    public CrashableFunctionStore(IFunctionStore inner) => _inner = inner;

    public void Crash() => _crashed = true;
    
    public Task<bool> CreateFunction(
        FunctionId functionId, 
        StoredParameter param, 
        string? scrapbookType, 
        Status initialStatus,
        int initialEpoch, 
        int initialSignOfLife
    ) => _crashed 
        ? Task.FromException<bool>(new TimeoutException()) 
        : _inner.CreateFunction(functionId, param, scrapbookType, initialStatus, initialEpoch, initialSignOfLife);

    public Task<bool> TryToBecomeLeader(FunctionId functionId, Status newStatus, int expectedEpoch, int newEpoch)
        => _crashed
            ? Task.FromException<bool>(new TimeoutException())
            : _inner.TryToBecomeLeader(functionId, newStatus, expectedEpoch, newEpoch);

    public Task<bool> UpdateSignOfLife(FunctionId functionId, int expectedEpoch, int newSignOfLife)
        => _crashed
            ? Task.FromException<bool>(new TimeoutException())
            : _inner.UpdateSignOfLife(functionId, expectedEpoch, newSignOfLife);

    public Task<IEnumerable<StoredFunctionStatus>> GetFunctionsWithStatus(
        FunctionTypeId functionTypeId,
        Status status,
        long? expiresBefore = null
    ) => _crashed
        ? Task.FromException<IEnumerable<StoredFunctionStatus>>(new TimeoutException())
        : _inner.GetFunctionsWithStatus(functionTypeId, status, expiresBefore);

    public record SetFunctionStateParams(
        FunctionId FunctionId,
        Status Status,
        string? ScrapbookJson,
        StoredResult? Result,
        StoredFailure? Failed,
        long? PostponedUntil,
        int ExpectedEpoch
    );
    
    public async Task<bool> SetFunctionState(
        FunctionId functionId,
        Status status,
        string? scrapbookJson,
        StoredResult? result,
        StoredFailure? failed,
        long? postponedUntil,
        int expectedEpoch
    )
    {
        if (_crashed)
            throw new TimeoutException();

        var success = await _inner
            .SetFunctionState(
                functionId,
                status,
                scrapbookJson,
                result,
                failed,
                postponedUntil,
                expectedEpoch
            );

        lock (_sync)
            _subject.OnNext(new SetFunctionStateParams(
                functionId, status, scrapbookJson, result, failed, postponedUntil, expectedEpoch
            ));

        return success;
    }

    public Task<bool> Barricade(FunctionId functionId)
        => _crashed
            ? Task.FromException<bool>(new TimeoutException())
            : _inner.Barricade(functionId);

    public Task<StoredFunction?> GetFunction(FunctionId functionId)
        => _crashed
            ? Task.FromException<StoredFunction?>(new TimeoutException())
            : _inner.GetFunction(functionId);
}

public static class CrashableFunctionStoreExtensions
{
    public static CrashableFunctionStore ToCrashableFunctionStore(this IFunctionStore store) => new(store);
}