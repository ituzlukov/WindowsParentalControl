using System.Collections.Concurrent;
using ParentalControl.Core.Data;
using ParentalControl.Core.Models;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Service.Telegram;

/// <summary>A child's request for more time, waiting for a parent's answer in Telegram.</summary>
public sealed record TimeRequest(int Id, int UserId, string UserSid, string Username, string Text);

/// <summary>
/// Lets a restricted user ask the parents for more time, at most <see cref="TelegramSettings.MaxRequestsPerDay"/>
/// times a day and one request at a time. Requests are kept in memory: after a service restart the
/// buttons of old requests answer "expired".
/// </summary>
public sealed class TimeRequestService
{
    private readonly TelegramNotifier _notifier;
    private readonly Serilog.ILogger _logger;
    private readonly ConcurrentDictionary<int, TimeRequest> _pending = new();
    private int _nextId;

    public TimeRequestService(TelegramNotifier notifier, Serilog.ILogger logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// How many more requests the user can make today; 0 if requests are off, the bot is not set up
    /// or a request is already waiting for an answer.
    /// </summary>
    public int RequestsLeft(string userSid)
    {
        if (string.IsNullOrWhiteSpace(TelegramSettings.BotToken) || TelegramSettings.GetPairedChats().Count == 0) return 0;
        if (_pending.Values.Any(r => r.UserSid == userSid)) return 0;

        var usedToday = EventRepository.CountEvents(userSid, EventType.TIME_REQUESTED, DateTime.Today);
        return RequestsLeft(TelegramSettings.MaxRequestsPerDay, usedToday);
    }

    public static int RequestsLeft(int maxPerDay, int usedToday) => Math.Max(0, maxPerDay - usedToday);

    public static string Prompt(int requestsLeft) =>
        $"\n\nAsk a parent for more time? (requests left today: {requestsLeft})";

    /// <summary>Sends the request to the parents with +15 / +30 / +60 / No buttons.</summary>
    public void Submit(User user, string statusLine)
    {
        var id = Interlocked.Increment(ref _nextId);
        var text = $"{user.Username} asks for more time.\n{statusLine}";
        _pending[id] = new TimeRequest(id, user.Id, user.Sid, user.Username, text);

        EventRepository.LogEvent(user.Sid, EventType.TIME_REQUESTED, statusLine);
        _logger.Information("Time request {RequestId} from {Username}: {Status}", id, user.Username, statusLine);

        IReadOnlyList<IReadOnlyList<InlineButton>> keyboard =
        [
            BotUi.BonusOptions
                .Select(m => new InlineButton($"+{m} min", BotUi.Format(new AnswerRequestAction(id, m))))
                .ToList(),
            [new InlineButton("No", BotUi.Format(new AnswerRequestAction(id, 0)))]
        ];
        _notifier.Send(text, keyboard);
    }

    /// <summary>Takes a pending request for answering; null if it was already answered or has expired.</summary>
    public TimeRequest? TryTake(int requestId) => _pending.TryRemove(requestId, out var request) ? request : null;
}
