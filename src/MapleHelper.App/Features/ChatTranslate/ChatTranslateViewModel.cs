using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.App.Services;
using MapleHelper.Core.Ai;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.History;
using MapleHelper.Core.Settings;
using Microsoft.Extensions.Logging;

namespace MapleHelper.App.Features.ChatTranslate;

public sealed record ChatLine(string Vietnamese, string English);

public sealed partial class ChatTranslateViewModel(
    ITranslationService translation,
    IHistoryRepository history,
    ISettingsService settings,
    ILogger<ChatTranslateViewModel> logger) : ObservableObject
{
    private const int MaxRecent = 5;

    public ObservableCollection<ChatLine> Recent { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TranslateCommand))]
    private string _input = "";

    [ObservableProperty]
    private string? _output;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TranslateCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _status;

    [ObservableProperty]
    private bool _isError;

    /// <summary>Phát khi đã copy xong và người dùng bật "tự đóng sau khi copy".</summary>
    public event EventHandler? CloseRequested;

    public void Reset()
    {
        Input = "";
        Output = null;
        Status = "Enter để dịch và copy · Shift+Enter xuống dòng · Esc để đóng";
        IsError = false;
    }

    [RelayCommand(CanExecute = nameof(CanTranslate))]
    private async Task TranslateAsync()
    {
        var text = Input.Trim();
        IsBusy = true;
        IsError = false;
        Status = "Đang dịch…";
        try
        {
            var english = await translation.TranslateChatAsync(text);
            Output = english;
            Recent.Insert(0, new ChatLine(text, english));
            while (Recent.Count > MaxRecent)
            {
                Recent.RemoveAt(Recent.Count - 1);
            }
            await history.AddAsync(new HistoryEntry { Kind = HistoryKind.ChatTranslate, Input = text, Result = english });

            if (ClipboardService.TrySetText(english))
            {
                Status = "Đã copy. Mở ô chat trong game rồi Ctrl+V.";
                if (settings.Current.Overlay.AutoCloseChatAfterCopy)
                {
                    await Task.Delay(900);
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                IsError = true;
                Status = "Không copy được vào clipboard, hãy bấm Copy để thử lại.";
            }
        }
        catch (AiException ex)
        {
            IsError = true;
            Status = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dịch chat thất bại");
            IsError = true;
            Status = "Lỗi không mong đợi: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanTranslate() => !IsBusy && !string.IsNullOrWhiteSpace(Input);

    [RelayCommand]
    private void Copy(string? text)
    {
        if (!string.IsNullOrEmpty(text) && ClipboardService.TrySetText(text))
        {
            IsError = false;
            Status = "Đã copy.";
        }
    }
}
