using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Platform;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Service.Telegram;

/// <summary>
/// Telegram bot for parents: status, bonus time, lock / unlock. Receives updates by long polling
/// (outgoing HTTPS only, nothing listens on this computer) and answers only paired chats.
/// </summary>
public sealed partial class TelegramBotWorker : BackgroundService
{
    private const int LongPollSeconds = 50;
    private const int MaxFailedPairingAttempts = 5;
    private static readonly TimeSpan NotConfiguredDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(60);

    private readonly SessionTracker _sessionTracker;
    private readonly UsageMonitorWorker _monitor;
    private readonly Serilog.ILogger _logger;
    private int _failedPairingAttempts;

    public TelegramBotWorker(SessionTracker sessionTracker, UsageMonitorWorker monitor, Serilog.ILogger logger)
    {
        _sessionTracker = sessionTracker;
        _monitor = monitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var token = TelegramSettings.BotToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                await Task.Delay(NotConfiguredDelay, stoppingToken);
                continue;
            }

            try
            {
                var api = new TelegramApi(token);
                var updates = await api.GetUpdatesAsync(TelegramSettings.UpdateOffset, LongPollSeconds, stoppingToken);
                foreach (var update in updates)
                {
                    if (update is null) continue;
                    var updateId = update["update_id"]!.GetValue<long>();
                    try
                    {
                        await HandleUpdateAsync(api, update, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.Error(ex, "Error handling Telegram update {UpdateId}", updateId);
                    }
                    // Never handle the same update twice, even if handling failed
                    TelegramSettings.UpdateOffset = updateId + 1;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning("Telegram polling failed: {Error}", ex.Message);
                await Task.Delay(ErrorDelay, stoppingToken);
            }
        }
    }

    private async Task HandleUpdateAsync(TelegramApi api, JsonNode update, CancellationToken ct)
    {
        if (update["message"] is { } message)
        {
            await HandleMessageAsync(api, message, ct);
        }
        else if (update["callback_query"] is { } callback)
        {
            await HandleCallbackAsync(api, callback, ct);
        }
    }

    private async Task HandleMessageAsync(TelegramApi api, JsonNode message, CancellationToken ct)
    {
        var chatId = message["chat"]?["id"]?.GetValue<long>();
        var text = message["text"]?.GetValue<string>()?.Trim() ?? "";
        if (chatId is null) return;

        if (!TelegramSettings.IsPaired(chatId.Value))
        {
            await HandleUnpairedMessageAsync(api, chatId.Value, text, SenderName(message["from"]), ct);
            return;
        }

        var command = text.Split(' ', 2)[0].Split('@')[0].ToLowerInvariant();
        switch (command)
        {
            case "/status":
            case "/start":
                await SendStatusAsync(api, chatId.Value, ct);
                break;
            default:
                await api.SendMessageAsync(chatId.Value,
                    "/status - time used and left, with buttons to add time or lock now.", ct: ct);
                break;
        }
    }

    private async Task HandleUnpairedMessageAsync(TelegramApi api, long chatId, string text, string name, CancellationToken ct)
    {
        var code = PairingCodePattern().Match(text) is { Success: true } m ? m.Groups[1].Value : null;
        if (code is null)
        {
            await api.SendMessageAsync(chatId,
                "This is a private bot. To pair, generate a code in the Parental Control admin app and send /start <code>.",
                ct: ct);
            return;
        }

        if (TelegramSettings.TryConsumePairingCode(code))
        {
            _failedPairingAttempts = 0;
            TelegramSettings.AddPairedChat(chatId, name);
            _logger.Information("Telegram chat paired: {Name} ({ChatId})", name, chatId);
            await api.SendMessageAsync(chatId,
                $"Paired with Parental Control on {Environment.MachineName}. Send /status to see the time left.", ct: ct);
            return;
        }

        // Limit guessing: a few wrong codes invalidate the current one
        if (++_failedPairingAttempts >= MaxFailedPairingAttempts)
        {
            _failedPairingAttempts = 0;
            TelegramSettings.ClearPairingCode();
            _logger.Warning("Too many wrong Telegram pairing codes; the current code was invalidated");
        }
        await api.SendMessageAsync(chatId, "Invalid or expired code. Generate a new one in the admin app.", ct: ct);
    }

    private async Task HandleCallbackAsync(TelegramApi api, JsonNode callback, CancellationToken ct)
    {
        var callbackId = callback["id"]!.GetValue<string>();
        var chatId = callback["message"]?["chat"]?["id"]?.GetValue<long>();
        var messageId = callback["message"]?["message_id"]?.GetValue<long>();
        var parent = SenderName(callback["from"]);

        if (chatId is null || messageId is null || !TelegramSettings.IsPaired(chatId.Value))
        {
            await api.AnswerCallbackQueryAsync(callbackId, "Not allowed", ct);
            return;
        }

        var action = BotUi.Parse(callback["data"]?.GetValue<string>());
        var user = action switch
        {
            GrantBonusAction a => UserRepository.GetById(a.UserId),
            LockNowAction a => UserRepository.GetById(a.UserId),
            UnlockAction a => UserRepository.GetById(a.UserId),
            _ => null
        };
        if (user is null)
        {
            await api.AnswerCallbackQueryAsync(callbackId, "This button is no longer valid", ct);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        string answer;
        switch (action)
        {
            case GrantBonusAction a:
                var grant = BonusGrant.Grant(user.Id, a.Minutes, DateTime.Now);
                EventRepository.LogEvent(user.Sid, EventType.BONUS_GRANTED,
                    $"+{a.Minutes} min via Telegram by {parent}" + GrantDetails(grant));
                _monitor.EnforceUser(user.Sid);
                TellUser(user.Sid, $"A parent added {a.Minutes} minutes.");
                answer = $"+{a.Minutes} min for {user.Username}" + ExtendedUntil(grant);
                break;

            case LockNowAction:
                UsageRepository.SetParentLock(user.Id, today, true);
                EventRepository.LogEvent(user.Sid, EventType.LOCKED_OUT, $"Locked by parent via Telegram ({parent})");
                _monitor.EnforceUser(user.Sid);
                answer = $"{user.Username} is locked for today";
                break;

            case UnlockAction:
                UsageRepository.SetParentLock(user.Id, today, false);
                EventRepository.LogEvent(user.Sid, EventType.LOCKED_OUT, $"Parent lock lifted via Telegram ({parent})");
                _monitor.EnforceUser(user.Sid);
                answer = $"{user.Username} is unlocked";
                break;

            default:
                await api.AnswerCallbackQueryAsync(callbackId, "This button is no longer valid", ct);
                return;
        }

        _logger.Information("Telegram: {Answer} (by {Parent})", answer, parent);
        await api.AnswerCallbackQueryAsync(callbackId, answer, ct);
        await EditToStatusAsync(api, chatId.Value, messageId.Value, user, ct);
    }

    private async Task SendStatusAsync(TelegramApi api, long chatId, CancellationToken ct)
    {
        var users = UserRepository.GetAll().Where(u => u.IsRestricted).ToList();
        var sent = false;
        foreach (var user in users)
        {
            var status = BuildStatus(user);
            if (status is null) continue;
            await api.SendMessageAsync(chatId, status.Value.Text, status.Value.Keyboard, ct);
            sent = true;
        }

        if (!sent) await api.SendMessageAsync(chatId, "No users with limits.", ct: ct);
    }

    private async Task EditToStatusAsync(TelegramApi api, long chatId, long messageId, User user, CancellationToken ct)
    {
        var status = BuildStatus(user);
        if (status is null) return;
        try
        {
            await api.EditMessageTextAsync(chatId, messageId, status.Value.Text, status.Value.Keyboard, ct);
        }
        catch (TelegramApiException)
        {
            // e.g. "message is not modified" - the status is already current
        }
    }

    private (string Text, IReadOnlyList<IReadOnlyList<InlineButton>> Keyboard)? BuildStatus(User user)
    {
        var now = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var limit = LimitRepository.GetEffective(user.Id, today);
        if (limit is null) return null;

        var usage = UsageRepository.GetUsage(user.Id, today);
        var session = _sessionTracker.ActiveSessions.Values.FirstOrDefault(s => s.UserSid == user.Sid);
        var state = session switch
        {
            null => SessionState.NotSignedIn,
            { LockedOutAt: not null } => SessionState.LockedOut,
            { IsDisconnected: true } => SessionState.SwitchedAway,
            { IsLocked: true } => SessionState.ScreenLocked,
            _ => SessionState.Active
        };

        var text = BotUi.FormatStatus(user.Username, limit, usage, state, session?.LockedOutAt, now);
        return (text, BotUi.UserKeyboard(user.Id, usage?.ParentLocked == true));
    }

    /// <summary>Shows a message in the user's active (not locked, connected) sessions.</summary>
    private void TellUser(string userSid, string message)
    {
        foreach (var session in _sessionTracker.ActiveSessions.Values)
        {
            if (session.UserSid == userSid && !session.IsPaused)
            {
                SessionManager.SendMessage(session.SessionId, "Parental Control", message);
            }
        }
    }

    private static string GrantDetails(BonusGrantResult grant) =>
        $" (bonus today: {grant.TotalBonusMinutes} min)" +
        (grant.ScheduleEndExtendedTo is { } end ? $"; allowed until {end:HH\\:mm} today" : "");

    private static string ExtendedUntil(BonusGrantResult grant) =>
        grant.ScheduleEndExtendedTo is { } end ? $", allowed until {end:HH\\:mm}" : "";

    private static string SenderName(JsonNode? from)
    {
        var first = from?["first_name"]?.GetValue<string>() ?? "";
        var last = from?["last_name"]?.GetValue<string>() ?? "";
        var name = $"{first} {last}".Trim();
        return name.Length > 0 ? name : from?["username"]?.GetValue<string>() ?? "Telegram user";
    }

    /// <summary>"/start 123456" (deep link or typed) or just "123456".</summary>
    [GeneratedRegex(@"^(?:/start\s+)?(\d{6})$")]
    private static partial Regex PairingCodePattern();
}
