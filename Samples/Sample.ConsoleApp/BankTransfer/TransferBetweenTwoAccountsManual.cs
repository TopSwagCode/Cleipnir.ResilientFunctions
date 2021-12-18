﻿using System;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions;
using Cleipnir.ResilientFunctions.Domain;

namespace ConsoleApp.BankTransfer;

public sealed class TransferSaga 
{
    private IBankClient BankClient { get; }

    public TransferSaga(RFunctions rFunctions, IBankClient bankClient)
    {
        BankClient = bankClient;
        Perform = rFunctions.Register<Transfer>(
            "Transfers".ToFunctionTypeId(),
            _Perform,
            transfer => $"{transfer.FromAccount}¤{transfer.ToAccount}"
        );
    }
    
    public RAction<Transfer> Perform { get; }
    private async Task<RResult> _Perform(Transfer transfer)
    {
        try
        {
            await BankClient.PostTransaction(transfer.FromAccountTransactionId, transfer.FromAccount, -transfer.Amount);
            await BankClient.PostTransaction(transfer.ToAccountTransactionId, transfer.ToAccount, transfer.Amount);
            return RResult.Success;
        }
        catch (Exception exception)
        {
            return Fail.WithException(exception);
        }
    }
}