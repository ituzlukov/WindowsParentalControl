using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ParentalControl.Core.Data;

namespace ParentalControl.Core.Telegram;

/// <summary>A Telegram chat (a parent) allowed to control the bot.</summary>
public sealed record PairedChat(long ChatId, string Name);

/// <summary>
/// Telegram bot settings stored in the settings table. The bot answers only paired chats;
/// a chat is paired by sending a one-time code, generated in the admin UI, to the bot.
/// </summary>
public static class TelegramSettings
{
    private const string KeyBotToken = "telegram.bot_token";
    private const string KeyPairedChats = "telegram.paired_chats";
    private const string KeyPairingCode = "telegram.pairing_code";
    private const string KeyPairingExpires = "telegram.pairing_expires_utc";
    private const string KeyUpdateOffset = "telegram.update_offset";

    public static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromMinutes(10);

    public static string? BotToken
    {
        get => SettingsRepository.Get(KeyBotToken);
        set => SettingsRepository.Set(KeyBotToken, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    /// <summary>Last processed Telegram update id + 1, so updates are not handled twice after a restart.</summary>
    public static long UpdateOffset
    {
        get => long.TryParse(SettingsRepository.Get(KeyUpdateOffset), out var v) ? v : 0;
        set => SettingsRepository.Set(KeyUpdateOffset, value.ToString(CultureInfo.InvariantCulture));
    }

    public static List<PairedChat> GetPairedChats()
    {
        var json = SettingsRepository.Get(KeyPairedChats);
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<PairedChat>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool IsPaired(long chatId) => GetPairedChats().Any(c => c.ChatId == chatId);

    public static void AddPairedChat(long chatId, string name)
    {
        var chats = GetPairedChats().Where(c => c.ChatId != chatId).ToList();
        chats.Add(new PairedChat(chatId, name));
        SettingsRepository.Set(KeyPairedChats, JsonSerializer.Serialize(chats));
    }

    public static void RemovePairedChat(long chatId)
    {
        var chats = GetPairedChats().Where(c => c.ChatId != chatId).ToList();
        SettingsRepository.Set(KeyPairedChats, JsonSerializer.Serialize(chats));
    }

    /// <summary>Creates a new one-time 6-digit pairing code, valid for <see cref="PairingCodeLifetime"/>.</summary>
    public static string GeneratePairingCode()
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        SettingsRepository.Set(KeyPairingCode, code);
        SettingsRepository.Set(KeyPairingExpires,
            DateTime.UtcNow.Add(PairingCodeLifetime).ToString("O", CultureInfo.InvariantCulture));
        return code;
    }

    /// <summary>True if the code matches the current, unexpired pairing code; the code can be used only once.</summary>
    public static bool TryConsumePairingCode(string code)
    {
        var expected = SettingsRepository.Get(KeyPairingCode);
        var expiresText = SettingsRepository.Get(KeyPairingExpires);
        if (expected is null || expiresText is null) return false;

        var expires = DateTime.Parse(expiresText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (!IsPairingCodeValid(code, expected, expires, DateTime.UtcNow)) return false;

        SettingsRepository.Set(KeyPairingCode, null);
        SettingsRepository.Set(KeyPairingExpires, null);
        return true;
    }

    /// <summary>Invalidates the current pairing code (e.g. after too many wrong attempts).</summary>
    public static void ClearPairingCode()
    {
        SettingsRepository.Set(KeyPairingCode, null);
        SettingsRepository.Set(KeyPairingExpires, null);
    }

    public static bool IsPairingCodeValid(string code, string expected, DateTime expiresUtc, DateTime nowUtc) =>
        nowUtc < expiresUtc &&
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(code.Trim()),
            System.Text.Encoding.ASCII.GetBytes(expected));
}
