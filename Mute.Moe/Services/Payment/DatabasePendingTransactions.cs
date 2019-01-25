﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mute.Moe.Extensions;
using Mute.Moe.Services.Database;

namespace Mute.Moe.Services.Payment
{
    public class DatabasePendingTransactions
        : IPendingTransactions
    {
        #region SQL transactions
        private const string InsertPendingSql = "INSERT INTO `IOU2_PendingTransactions` (`FromId`, `ToId`, `Amount`, `Unit`, `Note`, `InstantUnix`, `Pending`) VALUES (@FromId, @ToId, @Amount, @Unit, @Note, @InstantUnix, @Pending); SELECT last_insert_rowid();";

        private const string GetFilteredTransactionsSql = "SELECT rowid as rowid, * FROM IOU2_PendingTransactions " + 
                                                          "WHERE (FromId = @FromId or @FromId IS null) " + 
                                                          "AND (ToId = @ToId or @ToId IS null) " + 
                                                          "AND (Unit = @Unit or @Unit IS null) " + 
                                                          "AND (InstantUnix < @UpperBoundInstant or @UpperBoundInstant IS null) " + 
                                                          "AND (InstantUnix > @LowerBoundInstant or @LowerBoundInstant IS NULL) " + 
                                                          "AND (Pending = @Pending or @Pending IS null) " + 
                                                          "AND (rowid = @DebtId or @DebtId IS null) " + 
                                                          "ORDER BY InstantUnix;";

        private const string ConfirmPendingTransaction = "BEGIN TRANSACTION; " +
                                                         "  INSERT INTO IOU2_Transactions (FromId, ToId, Amount, Unit, Note, InstantUnix) " +
                                                         "  SELECT FromId, ToId, Amount, Unit, Note, InstantUnix " +
                                                         "  FROM IOU2_PendingTransactions " +
                                                         "  WHERE rowid = @DebtId " +
                                                         "  AND Pending = 'Pending'; " +
                                                         "" +
                                                         "  SELECT Pending FROM IOU2_PendingTransactions " +
                                                         "  WHERE rowid = @DebtId; " +
                                                         "" +
                                                         "  UPDATE IOU2_PendingTransactions " +
                                                         "  SET Pending = 'Confirmed' " +
                                                         "  WHERE rowid = @DebtId " +
                                                         "  AND Pending = 'Pending';" +
                                                         "COMMIT;";

        private const string DenyPendingTransaction = "BEGIN TRANSACTION; " +
                                                      "  SELECT Pending FROM IOU2_PendingTransactions " +
                                                      "  WHERE rowid = @DebtId; " +
                                                      "" +
                                                      "  UPDATE IOU2_PendingTransactions " +
                                                      "  SET Pending = 'Denied' " +
                                                      "  WHERE rowid = @DebtId " +
                                                      "  AND Pending = 'Pending';" +
                                                      "COMMIT;";
        #endregion

        private readonly IDatabaseService _database;

        public DatabasePendingTransactions([NotNull] IDatabaseService database, [NotNull] ITransactions dbTransactions)
        {
            if (!(dbTransactions is DatabaseTransactions))
                throw new ArgumentException("Transactions service paired with `DatabasePendingTransactions` must be `DatabaseTransactions`");

            _database = database;

            _database.Exec("CREATE TABLE IF NOT EXISTS `IOU2_PendingTransactions` (`FromId` TEXT NOT NULL, `ToId` TEXT NOT NULL, `Amount` TEXT NOT NULL, `Unit` TEXT NOT NULL, `Note` TEXT, `InstantUnix` TEXT NOT NULL, `Pending` TEXT NOT NULL);");
        }

        public async Task<uint> CreatePending(ulong fromId, ulong toId, decimal amount, string unit, string note, DateTime instant)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Cannot transact a negative amount");
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (fromId == toId)
                throw new InvalidOperationException("Cannot transact from self to self");

            using (var cmd = _database.CreateCommand())
            {
                cmd.CommandText = InsertPendingSql;
                cmd.Parameters.Add(new SQLiteParameter("@FromId", System.Data.DbType.String) { Value = fromId.ToString() });
                cmd.Parameters.Add(new SQLiteParameter("@ToId", System.Data.DbType.String) { Value = toId.ToString() });
                cmd.Parameters.Add(new SQLiteParameter("@Amount", System.Data.DbType.String) { Value = amount.ToString(CultureInfo.InvariantCulture) });
                cmd.Parameters.Add(new SQLiteParameter("@Unit", System.Data.DbType.String) { Value = unit.ToLowerInvariant() });
                cmd.Parameters.Add(new SQLiteParameter("@Note", System.Data.DbType.String) { Value = note ?? "" });
                cmd.Parameters.Add(new SQLiteParameter("@InstantUnix", System.Data.DbType.String) { Value = instant.UnixTimestamp() });
                cmd.Parameters.Add(new SQLiteParameter("@Pending", System.Data.DbType.String) { Value = PendingState.Pending.ToString() });

                return (uint)(long)await cmd.ExecuteScalarAsync();
            }
        }

        public async Task<IAsyncEnumerable<IPendingTransaction>> Get(uint? debtId = null, PendingState? state = null, ulong? fromId = null, ulong? toId = null, string unit = null, DateTime? after = null, DateTime? before = null)
        {
            IPendingTransaction ParsePendingTransaction(DbDataReader reader)
            {
                return new PendingTransaction(
                    ulong.Parse((string)reader["FromId"]),
                    ulong.Parse((string)reader["ToId"]),
                    decimal.Parse(reader["Amount"].ToString()),
                    (string)reader["Unit"],
                    (string)reader["Note"],
                    ulong.Parse((string)reader["InstantUnix"]).FromUnixTimestamp(),
                    Enum.Parse<PendingState>(reader["Pending"].ToString()),
                    uint.Parse(reader["rowid"].ToString())
                );
            }

            DbCommand PrepareQuery(IDatabaseService db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = GetFilteredTransactionsSql;
                cmd.Parameters.Add(new SQLiteParameter("@DebtId", System.Data.DbType.String) { Value = debtId?.ToString() });
                cmd.Parameters.Add(new SQLiteParameter("@FromId", System.Data.DbType.String) { Value = fromId?.ToString() });
                cmd.Parameters.Add(new SQLiteParameter("@ToId", System.Data.DbType.String) { Value = toId?.ToString() });
                cmd.Parameters.Add(new SQLiteParameter("@Unit", System.Data.DbType.String) { Value = unit?.ToLowerInvariant() });
                cmd.Parameters.Add(new SQLiteParameter("@UpperBoundInstant", System.Data.DbType.String) { Value = before?.UnixTimestamp().ToString() });
                cmd.Parameters.Add(new SQLiteParameter("@LowerBoundInstant", System.Data.DbType.String) { Value = after?.UnixTimestamp().ToString() });
                cmd.Parameters.Add(new SQLiteParameter("@Pending", System.Data.DbType.String) { Value = state?.ToString() });
                return cmd;
            }

            return new SqlAsyncResult<IPendingTransaction>(_database, PrepareQuery, ParsePendingTransaction);
        }

        private async Task<PendingState?> UpdatePending(uint id, string sql)
        {
            PendingState ParseResult(DbDataReader reader)
            {
                return Enum.Parse<PendingState>(reader["Pending"].ToString());
            }

            DbCommand PrepareQuery(IDatabaseService db)
            {
                var cmd = db.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.Add(new SQLiteParameter("@DebtId", System.Data.DbType.String) { Value = id.ToString() });
                return cmd;
            }

            var results = await new SqlAsyncResult<PendingState>(_database, PrepareQuery, ParseResult).ToArray();

            if (results.Length > 1)
                throw new InvalidOperationException($"Modified more than 1 payment at once! ID:{id}");
            if (results.Length == 0)
                return null;

            return results[0];
        }

        public async Task<ConfirmResult> ConfirmPending(uint debtId)
        {
            var result = await UpdatePending(debtId, ConfirmPendingTransaction);
            if (!result.HasValue)
                return ConfirmResult.IdNotFound;

            switch (result)
            {
                case PendingState.Confirmed:
                    return ConfirmResult.AlreadyConfirmed;
                case PendingState.Denied:
                    return ConfirmResult.AlreadyDenied;
                case PendingState.Pending:
                    return ConfirmResult.Confirmed;

                default:
                    throw new InvalidOperationException($"Unknown debt state! ID: {debtId} State:{result}");
            }
        }

        public async Task<DenyResult> DenyPending(uint debtId)
        {
            var result = await UpdatePending(debtId, DenyPendingTransaction);
            if (!result.HasValue)
                return DenyResult.IdNotFound;

            switch (result)
            {
                case PendingState.Confirmed:
                    return DenyResult.AlreadyConfirmed;
                case PendingState.Denied:
                    return DenyResult.AlreadyDenied;
                case PendingState.Pending:
                    return DenyResult.Denied;

                default:
                    throw new InvalidOperationException($"Unknown debt state! ID: {debtId} State:{result}");
            }
        }
    }
}
