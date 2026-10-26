namespace MapleHelper.App.Features.Placeholder;

/// <summary>Trang tạm cho các mục chưa làm, ghi rõ mốc sẽ hoàn thành.</summary>
public sealed record PlaceholderViewModel(string Title, string Description, string Milestone)
{
    public string MilestoneText => $"Sẽ hoàn thành ở mốc {Milestone}";
}
