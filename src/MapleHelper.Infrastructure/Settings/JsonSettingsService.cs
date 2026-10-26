using System.Text.Json;
using System.Text.Json.Serialization;
using MapleHelper.Core.Settings;

namespace MapleHelper.Infrastructure.Settings;

public sealed class JsonSettingsService : ISettingsService
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public JsonSettingsService(AppPaths paths)
    {
        _filePath = paths.SettingsFile;
        Current = Load(_filePath);
    }

    public AppSettings Current { get; }

    public event EventHandler? Changed;

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _saveLock.WaitAsync(ct);
        try
        {
            // Ghi ra file tạm rồi move để không bao giờ để lại settings.json ghi dở
            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, Current, JsonOptions, ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            _saveLock.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static AppSettings Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new AppSettings();
        }

        AppSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), JsonOptions);
        }
        catch (JsonException)
        {
            // File hỏng: giữ lại bản .bak để debug, chạy tiếp với mặc định
            File.Copy(filePath, filePath + ".bak", overwrite: true);
            settings = null;
        }

        settings ??= new AppSettings();
        AddMissingHotkeys(settings);
        return settings;
    }

    /// <summary>Phiên bản mới thêm action mới thì người dùng cũ vẫn có hotkey mặc định cho action đó.</summary>
    private static void AddMissingHotkeys(AppSettings settings)
    {
        var existing = settings.Hotkeys.Select(h => h.Action).ToHashSet();
        settings.Hotkeys.AddRange(HotkeyBinding.CreateDefaults().Where(h => !existing.Contains(h.Action)));
    }
}
