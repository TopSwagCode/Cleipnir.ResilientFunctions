﻿using Cleipnir.ResilientFunctions.Utils.Monitor;
using MySql.Data.MySqlClient;

namespace Cleipnir.ResilientFunctions.MySQL.Utils;

public class Monitor : IMonitor
{
    private readonly string _connectionString;
    private readonly string _tablePrefix;

    public Monitor(string connectionString, string tablePrefix = "")
    {
        _connectionString = connectionString;
        _tablePrefix = tablePrefix.ToLower();
    }

    public async Task Initialize()
    {
        await using var conn = await DatabaseHelper.CreateOpenConnection(_connectionString);
        var sql = @$"
            CREATE TABLE IF NOT EXISTS {_tablePrefix}monitor (
                lockid VARCHAR(255) PRIMARY KEY NOT NULL,                
                keyid VARCHAR(255) NOT NULL
            );";

        await using var command = new MySqlCommand(sql, conn);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DropUnderlyingTable()
    {
        await using var conn = await DatabaseHelper.CreateOpenConnection(_connectionString);
        var sql = @$"DROP TABLE IF EXISTS {_tablePrefix}monitor";
        
        await using var command = new MySqlCommand(sql, conn);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IMonitor.ILock?> Acquire(string lockId, string keyId)
    {
        await using var conn = await DatabaseHelper.CreateOpenConnection(_connectionString);
        {
            var sql = $"INSERT IGNORE INTO {_tablePrefix}monitor (lockid, keyid) VALUES (?, ?)";
            await using var command = new MySqlCommand(sql, conn)
            {
                Parameters =
                {
                    new() {Value = lockId},
                    new() {Value = keyId}
                }
            };

            var affectedRows = await command.ExecuteNonQueryAsync();
            if (affectedRows == 1) return new Lock(this, lockId, keyId);
        }
        {
            var sql = @$"
                SELECT COUNT(*) 
                FROM {_tablePrefix}monitor
                WHERE lockid = ? AND keyid = ?;";
            await using var command = new MySqlCommand(sql, conn)
            {
                Parameters =
                {
                    new() {Value = lockId},
                    new() {Value = keyId}
                }
            };
            var count = (long) (await command.ExecuteScalarAsync() ?? 0);
            return count == 1
                ? new Lock(this, lockId, keyId)
                : null;
        }
    }

    public async Task Release(string lockId, string keyId)
    {
        await using var conn = await DatabaseHelper.CreateOpenConnection(_connectionString);
        var sql = @$"DELETE FROM {_tablePrefix}monitor WHERE lockid = ? AND keyid = ?";
        await using var command = new MySqlCommand(sql, conn)
        {
            Parameters =
            {
                new() {Value = lockId},
                new() {Value = keyId}
            }
        };
        await command.ExecuteNonQueryAsync();
    }

    private class Lock : IMonitor.ILock
    {
        private readonly IMonitor _monitor;
        private readonly string _lockId;
        private readonly string _keyId;

        public Lock(IMonitor monitor, string lockId, string keyId)
        {
            _monitor = monitor;
            _lockId = lockId;
            _keyId = keyId;
        }

        public async ValueTask DisposeAsync() => await _monitor.Release(_lockId, _keyId);
    }
}