using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MapleHelper.App.Services;
using MapleHelper.Core.Features.Translation;
using MapleHelper.Core.History;
using Microsoft.Extensions.Logging;

namespace MapleHelper.App.Features.ChatTranslate;

public sealed record ChatLine(string Vietnamese, string English);

public sealed partial class ChatTranslateViewModel(
    ITranslationService translation,
    IHistoryRepository history,
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
                // Xoá ô nhập để gõ ngay câu tiếp theo; câu vừa dịch vẫn hiện ở trên và trong danh sách gần đây
                Input = "";
                Status = "Đã copy. Ctrl+V vào ô chat trong game, hoặc gõ tiếp câu khác.";
            }
            else
            {
                IsError = true;
                Status = "Không copy được vào clipboard, hãy bấm Copy để thử lại.";
            }
        }
        catch (MachineTranslationException ex)
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
