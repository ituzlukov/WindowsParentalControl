using Microsoft.Data.Sqlite;
using ParentalControl.Core.Models;

namespace ParentalControl.Core.Data;

public static class UsageRepository
{
    public static UsageRecord? GetUsage(int userId, DateOnly date)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT user_id, date, minutes_used, bonus_minutes, parent_lock FROM usage WHERE user_id = @userId AND date = @date";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return ReadUsageRecord(reader);
    }

    public static void AddMinutes(int userId, DateOnly date, int minutes)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage (user_id, date, minutes_used)
            VALUES (@userId, @date, @minutes)
            ON CONFLICT(user_id, date) DO UPDATE SET
                minutes_used = minutes_used + @minutes
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@minutes", minutes);
        cmd.ExecuteNonQuery();
    }

    public static void SetUsage(int userId, DateOnly date, int minutes)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage (user_id, date, minutes_used)
            VALUES (@userId, @date, @minutes)
            ON CONFLICT(user_id, date) DO UPDATE SET
                minutes_used = @minutes
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@minutes", minutes);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Adds bonus minutes for the day (on top of the daily limit) and lifts a parent lock, since granting
    /// time means the user may continue. Returns the new bonus.
    /// </summary>
    public static int AddBonusMinutes(int userId, DateOnly date, int minutes)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage (user_id, date, minutes_used, bonus_minutes, parent_lock)
            VALUES (@userId, @date, 0, MAX(0, @minutes), 0)
            ON CONFLICT(user_id, date) DO UPDATE SET
                bonus_minutes = MAX(0, bonus_minutes + @minutes),
                parent_lock = 0
            RETURNING bonus_minutes
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@minutes", minutes);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>Locks the user out for the rest of the day ("Lock now"), or lifts that lock.</summary>
    public static void SetParentLock(int userId, DateOnly date, bool locked)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO usage (user_id, date, minutes_used, parent_lock)
            VALUES (@userId, @date, 0, @locked)
            ON CONFLICT(user_id, date) DO UPDATE SET
                parent_lock = @locked
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@locked", locked ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public static List<UsageRecord> GetUsageRange(int userId, DateOnly from, DateOnly to)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT user_id, date, minutes_used, bonus_minutes, parent_lock FROM usage
            WHERE user_id = @userId AND date >= @from AND date <= @to
            ORDER BY date
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd"));

        var records = new List<UsageRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadUsageRecord(reader));
        }
        return records;
    }

    public static int DeleteOlderThan(DateOnly cutoff)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM usage WHERE date < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("yyyy-MM-dd"));
        return cmd.ExecuteNonQuery();
    }

    private static UsageRecord ReadUsageRecord(SqliteDataReader reader)
    {
        return new UsageRecord
        {
            UserId = reader.GetInt32(0),
            Date = DateOnly.Parse(reader.GetString(1)),
            MinutesUsed = reader.GetInt32(2),
            BonusMinutes = reader.GetInt32(3),
            ParentLocked = reader.GetInt32(4) != 0
        };
    }
}
