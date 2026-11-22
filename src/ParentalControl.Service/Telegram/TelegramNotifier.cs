using ParentalControl.Core.Models;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Service.Telegram;

/// <summary>
/// Sends notifications to all paired parents. Fire-and-forget: never blocks or fails the caller;
/// does nothing if the bot is not configured.
/// </summary>
public sealed class TelegramNotifier
{
    private readonly Serilog.ILogger _logger;

    public TelegramNotifier(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public void NotifyText(string text) => Send(text, null);

    public void NotifyLockedOut(User user, string reason, DateTime logoffAt)
    {
        Send($"{user.Username}: {reason}.\nThe session is locked; it will be logged off at {logoffAt:HH\\:mm} unless you add time.",
            BotUi.UserKeyboard(user.Id, parentLocked: reason == "Locked by parent"));
    }

    public void Send(string text, IReadOnlyList<IReadOnlyList<InlineButton>>? keyboard)
    {
        var token = TelegramSettings.BotToken;
        var chats = TelegramSettings.GetPairedChats();
        if (string.IsNullOrWhiteSpace(token) || chats.Count == 0) return;

        Task.Run(async () =>
        {
            var api = new TelegramApi(token);
            foreach (var chat in chats)
            {
                try
                {
                    await api.SendMessageAsync(chat.ChatId, text, keyboard);
                }
                catch (Exception ex)
                {
                    _logger.Warning("Telegram notification to {Chat} failed: {Error}", chat.Name, ex.Message);
                }
            }
        });
    }
}
