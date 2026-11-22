using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ParentalControl.Core.Telegram;

/// <summary>One inline keyboard button: label and callback data (at most 64 bytes).</summary>
public sealed record InlineButton(string Text, string CallbackData);

/// <summary>
/// Minimal Telegram Bot API client. Only outgoing HTTPS requests to api.telegram.org are made
/// (updates are received by long polling), so nothing listens on the computer.
/// Messages are sent as plain text, no markup.
/// </summary>
public sealed class TelegramApi
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };
    private readonly string _token;

    public TelegramApi(string token)
    {
        _token = token;
    }

    /// <summary>The bot's username (without @), or throws if the token is invalid.</summary>
    public async Task<string> GetBotUsernameAsync(CancellationToken ct = default)
    {
        var result = await CallAsync("getMe", new JsonObject(), ct);
        return result?["username"]?.GetValue<string>() ?? "";
    }

    public async Task<long?> SendMessageAsync(
        long chatId, string text, IReadOnlyList<IReadOnlyList<InlineButton>>? keyboard = null, CancellationToken ct = default)
    {
        var body = new JsonObject { ["chat_id"] = chatId, ["text"] = text };
        if (keyboard is not null) body["reply_markup"] = BuildKeyboard(keyboard);
        var result = await CallAsync("sendMessage", body, ct);
        return result?["message_id"]?.GetValue<long>();
    }

    public async Task EditMessageTextAsync(
        long chatId, long messageId, string text, IReadOnlyList<IReadOnlyList<InlineButton>>? keyboard = null,
        CancellationToken ct = default)
    {
        var body = new JsonObject { ["chat_id"] = chatId, ["message_id"] = messageId, ["text"] = text };
        if (keyboard is not null) body["reply_markup"] = BuildKeyboard(keyboard);
        await CallAsync("editMessageText", body, ct);
    }

    public async Task AnswerCallbackQueryAsync(string callbackQueryId, string? text = null, CancellationToken ct = default)
    {
        var body = new JsonObject { ["callback_query_id"] = callbackQueryId };
        if (text is not null) body["text"] = text;
        await CallAsync("answerCallbackQuery", body, ct);
    }

    /// <summary>Long polling: waits up to <paramref name="timeoutSeconds"/> for new updates.</summary>
    public async Task<JsonArray> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["offset"] = offset,
            ["timeout"] = timeoutSeconds,
            ["allowed_updates"] = new JsonArray("message", "callback_query")
        };
        var result = await CallAsync("getUpdates", body, ct);
        return result as JsonArray ?? [];
    }

    private static JsonObject BuildKeyboard(IReadOnlyList<IReadOnlyList<InlineButton>> keyboard)
    {
        var rows = new JsonArray();
        foreach (var row in keyboard)
        {
            var buttons = new JsonArray();
            foreach (var button in row)
            {
                buttons.Add(new JsonObject { ["text"] = button.Text, ["callback_data"] = button.CallbackData });
            }
            rows.Add(buttons);
        }
        return new JsonObject { ["inline_keyboard"] = rows };
    }

    private async Task<JsonNode?> CallAsync(string method, JsonObject body, CancellationToken ct)
    {
        using var response = await Http.PostAsJsonAsync($"https://api.telegram.org/bot{_token}/{method}", body, ct);
        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);

        if (json?["ok"]?.GetValue<bool>() != true)
        {
            // Never include the URL in errors: it contains the bot token
            var description = json?["description"]?.GetValue<string>() ?? response.StatusCode.ToString();
            throw new TelegramApiException($"Telegram {method} failed: {description}");
        }
        return json["result"];
    }
}

public sealed class TelegramApiException(string message) : Exception(message);
