using Microsoft.Data.Sqlite;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Data;

public static class LimitRepository
{
    /// <summary>
    /// The user's default limits (without per-weekday overrides).
    /// </summary>
    public static LimitConfig? GetByUserId(int userId)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT user_id, daily_minutes, schedule_start, schedule_end FROM limits WHERE user_id = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new LimitConfig
        {
            UserId = reader.GetInt32(0),
            DailyMinutes = reader.GetInt32(1),
            ScheduleStart = TimeOnly.Parse(reader.GetString(2)),
            ScheduleEnd = TimeOnly.Parse(reader.GetString(3))
        };
    }

    /// <summary>
    /// The limits in effect on the given date: the defaults with that weekday's override applied.
    /// Null if the user has no limits.
    /// </summary>
    public static LimitConfig? GetEffective(int userId, DateOnly date)
    {
        var defaults = GetByUserId(userId);
        if (defaults is null) return null;

        var dayOverride = GetOverrides(userId).FirstOrDefault(o => o.DayOfWeek == date.DayOfWeek);
        return defaults.WithOverride(dayOverride);
    }

    public static List<LimitOverride> GetOverrides(int userId)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT user_id, day_of_week, daily_minutes, schedule_start, schedule_end
            FROM limit_overrides WHERE user_id = @userId
            """;
        cmd.Parameters.AddWithValue("@userId", userId);

        var overrides = new List<LimitOverride>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            overrides.Add(new LimitOverride
            {
                UserId = reader.GetInt32(0),
                DayOfWeek = (DayOfWeek)reader.GetInt32(1),
                DailyMinutes = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                ScheduleStart = reader.IsDBNull(3) ? null : TimeOnly.Parse(reader.GetString(3)),
                ScheduleEnd = reader.IsDBNull(4) ? null : TimeOnly.Parse(reader.GetString(4))
            });
        }
        return overrides;
    }

    public static void Upsert(LimitConfig config)
    {
        using var connection = DatabaseManager.CreateConnection();
        Upsert(connection, null, config);
    }

    /// <summary>
    /// Saves the default limits and replaces all per-weekday overrides in one transaction.
    /// Empty overrides are not stored.
    /// </summary>
    public static void Save(LimitConfig config, IEnumerable<LimitOverride> overrides)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var transaction = connection.BeginTransaction();

        Upsert(connection, transaction, config);
        DeleteOverrides(connection, transaction, config.UserId);

        foreach (var dayOverride in overrides.Where(o => !o.IsEmpty))
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO limit_overrides (user_id, day_of_week, daily_minutes, schedule_start, schedule_end)
                VALUES (@userId, @dayOfWeek, @dailyMinutes, @start, @end)
                """;
            cmd.Parameters.AddWithValue("@userId", config.UserId);
            cmd.Parameters.AddWithValue("@dayOfWeek", (int)dayOverride.DayOfWeek);
            cmd.Parameters.AddWithValue("@dailyMinutes", (object?)dayOverride.DailyMinutes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@start", (object?)dayOverride.ScheduleStart?.ToString("HH:mm") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@end", (object?)dayOverride.ScheduleEnd?.ToString("HH:mm") ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public static void Delete(int userId)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var transaction = connection.BeginTransaction();

        DeleteOverrides(connection, transaction, userId);

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "DELETE FROM limits WHERE user_id = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.ExecuteNonQuery();

        transaction.Commit();
    }

    private static void Upsert(SqliteConnection connection, SqliteTransaction? transaction, LimitConfig config)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO limits (user_id, daily_minutes, schedule_start, schedule_end)
            VALUES (@userId, @dailyMinutes, @start, @end)
            ON CONFLICT(user_id) DO UPDATE SET
                daily_minutes = excluded.daily_minutes,
                schedule_start = excluded.schedule_start,
                schedule_end = excluded.schedule_end
            """;
        cmd.Parameters.AddWithValue("@userId", config.UserId);
        cmd.Parameters.AddWithValue("@dailyMinutes", config.DailyMinutes);
        cmd.Parameters.AddWithValue("@start", config.ScheduleStart.ToString("HH:mm"));
        cmd.Parameters.AddWithValue("@end", config.ScheduleEnd.ToString("HH:mm"));
        cmd.ExecuteNonQuery();
    }

    private static void DeleteOverrides(SqliteConnection connection, SqliteTransaction transaction, int userId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "DELETE FROM limit_overrides WHERE user_id = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.ExecuteNonQuery();
    }
}
