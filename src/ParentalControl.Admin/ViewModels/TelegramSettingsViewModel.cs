using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParentalControl.Core.Telegram;

namespace ParentalControl.Admin.ViewModels;

/// <summary>
/// Telegram bot settings: bot token and pairing parents' chats with a one-time code.
/// The bot itself runs in the service.
/// </summary>
public partial class TelegramSettingsViewModel : ObservableObject
{
    private readonly Action _navigateBack;

    [ObservableProperty]
    private string _botToken = string.Empty;

    [ObservableProperty]
    private string _botUsername = string.Empty;

    [ObservableProperty]
    private string _pairingInstructions = string.Empty;

    public ObservableCollection<PairedChat> PairedChats { get; } = [];

    public TelegramSettingsViewModel(Action navigateBack)
    {
        _navigateBack = navigateBack;
    }

    public void Load()
    {
        BotToken = TelegramSettings.BotToken ?? string.Empty;
        RefreshChats();
        _ = LoadBotUsernameAsync(showErrors: false);
    }

    [RelayCommand]
    private void GoBack() => _navigateBack();

    [RelayCommand]
    private async Task SaveTokenAsync()
    {
        TelegramSettings.BotToken = BotToken;
        BotUsername = string.Empty;
        if (string.IsNullOrWhiteSpace(BotToken)) return;

        if (await LoadBotUsernameAsync(showErrors: true))
        {
            MessageBox.Show($"Bot token saved: @{BotUsername}.\n\nThe service picks it up within a minute.",
                "Telegram", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    [RelayCommand]
    private void GeneratePairingCode()
    {
        if (string.IsNullOrWhiteSpace(TelegramSettings.BotToken))
        {
            MessageBox.Show("Save the bot token first.", "Telegram", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var code = TelegramSettings.GeneratePairingCode();
        var bot = string.IsNullOrEmpty(BotUsername) ? "your bot" : $"@{BotUsername}";
        PairingInstructions =
            $"Send this message to {bot} in Telegram within {TelegramSettings.PairingCodeLifetime.TotalMinutes:0} minutes:\n" +
            $"/start {code}\n\n" +
            "Then press Refresh below: your chat appears in the list. Repeat for each parent.";
    }

    [RelayCommand]
    private void RefreshChats()
    {
        PairedChats.Clear();
        foreach (var chat in TelegramSettings.GetPairedChats()) PairedChats.Add(chat);
    }

    [RelayCommand]
    private void RemoveChat(PairedChat chat)
    {
        var result = MessageBox.Show($"Unpair {chat.Name}? The bot will no longer answer this chat.",
            "Telegram", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        TelegramSettings.RemovePairedChat(chat.ChatId);
        RefreshChats();
    }

    [RelayCommand]
    private async Task SendTestMessageAsync()
    {
        var token = TelegramSettings.BotToken;
        var chats = TelegramSettings.GetPairedChats();
        if (string.IsNullOrWhiteSpace(token) || chats.Count == 0)
        {
            MessageBox.Show("Save the bot token and pair at least one chat first.", "Telegram",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var api = new TelegramApi(token);
            foreach (var chat in chats)
            {
                await api.SendMessageAsync(chat.ChatId, "Parental Control: test message from the admin app.");
            }
            MessageBox.Show($"Test message sent to {chats.Count} chat(s).", "Telegram",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not send the test message: {ex.Message}", "Telegram",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<bool> LoadBotUsernameAsync(bool showErrors)
    {
        var token = TelegramSettings.BotToken;
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            BotUsername = await new TelegramApi(token).GetBotUsernameAsync();
            return true;
        }
        catch (Exception ex)
        {
            if (showErrors)
            {
                MessageBox.Show($"The token was saved, but Telegram did not accept it: {ex.Message}", "Telegram",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }
    }
}
