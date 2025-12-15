using Microsoft.Data.SqlClient;
using System.Data;

namespace Shared;

public sealed class MsSqlEmailLogStore
{
    private readonly string _cs;

    public MsSqlEmailLogStore(string connectionString)
    {
        _cs = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<object?> GetAsync(Guid messageId)
    {
        using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT MessageId, Status, ToList, Subject, ErrorDetail, RequestedAtUtc, UpdatedAtUtc
FROM dbo.EmailSendLog
WHERE MessageId = @MessageId";
        cmd.Parameters.AddWithValue("@MessageId", messageId);

        using var rdr = await cmd.ExecuteReaderAsync();
        if (!await rdr.ReadAsync()) return null;

        return new
        {
            MessageId = rdr.GetGuid(0),
            Status = rdr.GetString(1),
            ToList = rdr.IsDBNull(2) ? null : rdr.GetString(2),
            Subject = rdr.IsDBNull(3) ? null : rdr.GetString(3),
            ErrorDetail = rdr.IsDBNull(4) ? null : rdr.GetString(4),
            RequestedAtUtc = rdr.IsDBNull(5) ? (DateTime?)null : rdr.GetDateTime(5),
            UpdatedAtUtc = rdr.GetDateTime(6),
        };
    }

    public async Task<List<object>> GetRecentAsync(int take)
    {
        var list = new List<object>();

        using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
SELECT TOP ({take})
       MessageId, Status, ToList, Subject, ErrorDetail, RequestedAtUtc, UpdatedAtUtc
FROM dbo.EmailSendLog
ORDER BY UpdatedAtUtc DESC";

        using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            list.Add(new
            {
                MessageId = rdr.GetGuid(0),
                Status = rdr.GetString(1),
                ToList = rdr.IsDBNull(2) ? null : rdr.GetString(2),
                Subject = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                ErrorDetail = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                RequestedAtUtc = rdr.IsDBNull(5) ? (DateTime?)null : rdr.GetDateTime(5),
                UpdatedAtUtc = rdr.GetDateTime(6),
            });
        }

        return list;
    }



    public async Task UpsertAsync(
        Guid messageId,
        string status,
        string? toList = null,
        string? subject = null,
        DateTime? requestedAtUtc = null,
        string? errorDetail = null)
    {
        if (messageId == Guid.Empty) throw new ArgumentException("messageId cannot be empty.", nameof(messageId));
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("status required.", nameof(status));

        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
MERGE dbo.EmailSendLog AS T
USING (SELECT @MessageId AS MessageId) AS S
ON T.MessageId = S.MessageId
WHEN MATCHED THEN
    UPDATE SET
        Status = @Status,
        ToList = COALESCE(@ToList, T.ToList),
        Subject = COALESCE(@Subject, T.Subject),
        ErrorDetail = @ErrorDetail,
        RequestedAtUtc = COALESCE(@RequestedAtUtc, T.RequestedAtUtc),
        UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (MessageId, Status, ToList, Subject, ErrorDetail, RequestedAtUtc, UpdatedAtUtc)
    VALUES (@MessageId, @Status, @ToList, @Subject, @ErrorDetail, COALESCE(@RequestedAtUtc, SYSUTCDATETIME()), SYSUTCDATETIME());
";

        cmd.Parameters.Add(new SqlParameter("@MessageId", SqlDbType.UniqueIdentifier) { Value = messageId });
        cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 32) { Value = status });

        cmd.Parameters.Add(new SqlParameter("@ToList", SqlDbType.NVarChar, -1) { Value = (object?)toList ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Subject", SqlDbType.NVarChar, 500) { Value = (object?)subject ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ErrorDetail", SqlDbType.NVarChar, -1) { Value = (object?)errorDetail ?? DBNull.Value });

        cmd.Parameters.Add(new SqlParameter("@RequestedAtUtc", SqlDbType.DateTime2)
        {
            Value = requestedAtUtc.HasValue ? requestedAtUtc.Value : DBNull.Value
        });

        await cmd.ExecuteNonQueryAsync();
    }
}
