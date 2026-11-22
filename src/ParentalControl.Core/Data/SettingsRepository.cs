namespace ParentalControl.Core.Data;

/// <summary>
/// Key/value application settings. The database is readable only by SYSTEM and Administrators,
/// so secrets such as the Telegram bot token are not exposed to restricted users.
/// </summary>
public static class SettingsRepository
{
    public static string? Get(string key)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        return cmd.ExecuteScalar() as string;
    }

    public static void Set(string key, string? value)
    {
        using var connection = DatabaseManager.CreateConnection();
        using var cmd = connection.CreateCommand();
        if (value is null)
        {
            cmd.CommandText = "DELETE FROM settings WHERE key = @key";
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO settings (key, value) VALUES (@key, @value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value
                """;
            cmd.Parameters.AddWithValue("@value", value);
        }
        cmd.Parameters.AddWithValue("@key", key);
        cmd.ExecuteNonQuery();
    }
}
