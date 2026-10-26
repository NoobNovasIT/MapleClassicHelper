using System.Text;
using MapleHelper.Core.Ai;
using MapleHelper.Core.Settings;
using MapleHelper.Infrastructure.Settings;

namespace MapleHelper.Infrastructure.Tests;

public sealed class SettingsStorageTests : IDisposable
{
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "MapleHelperTests", Guid.NewGuid().ToString("N")));

    public void Dispose() => Directory.Delete(_paths.Root, recursive: true);

    [Fact]
    public async Task Settings_round_trip_through_json_file()
    {
        var first = new JsonSettingsService(_paths);
        first.Current.Ai.ActiveProvider = AiProviderKind.Claude;
        first.Current.Ai.Models[AiProviderKind.Claude] = "claude-sonnet-5-5";
        first.Current.MinimizeToTrayOnClose = false;
        await first.SaveAsync(TestContext.Current.CancellationToken);

        var second = new JsonSettingsService(_paths);

        Assert.Equal(AiProviderKind.Claude, second.Current.Ai.ActiveProvider);
        Assert.Equal("claude-sonnet-5-5", second.Current.Ai.GetModel(AiProviderKind.Claude));
        Assert.False(second.Current.MinimizeToTrayOnClose);
        Assert.Contains("\"Claude\"", File.ReadAllText(_paths.SettingsFile)); // enum lưu dạng chữ, dễ đọc
    }

    [Fact]
    public void Corrupt_settings_fall_back_to_defaults_and_keep_backup()
    {
        File.WriteAllText(_paths.SettingsFile, "{ not json");

        var service = new JsonSettingsService(_paths);

        Assert.Equal(AiProviderKind.Gemini, service.Current.Ai.ActiveProvider);
        Assert.True(File.Exists(_paths.SettingsFile + ".bak"));
    }

    [Fact]
    public void Missing_hotkey_actions_are_added_on_load()
    {
        File.WriteAllText(_paths.SettingsFile, """
            {"Hotkeys":[{"Action":"ToggleOverlay","Modifiers":"Alt","VirtualKey":77,"Enabled":true}]}
            """);

        var hotkeys = new JsonSettingsService(_paths).Current.Hotkeys;

        Assert.Equal(Enum.GetValues<HotkeyAction>().Length, hotkeys.Count);
        Assert.Equal(HotkeyModifiers.Alt, hotkeys.Single(h => h.Action == HotkeyAction.ToggleOverlay).Modifiers);
    }

    [Fact]
    public void Secrets_persist_encrypted()
    {
        new DpapiSecretStore(_paths).Set("ai.Gemini.apiKey", "AIza-secret-value");

        var reloaded = new DpapiSecretStore(_paths);

        Assert.Equal("AIza-secret-value", reloaded.Get("ai.Gemini.apiKey"));
        Assert.DoesNotContain("AIza-secret-value", Encoding.UTF8.GetString(File.ReadAllBytes(_paths.SecretsFile)));
    }

    [Fact]
    public void Unreadable_secrets_file_starts_empty()
    {
        File.WriteAllBytes(_paths.SecretsFile, [1, 2, 3, 4]);

        Assert.Null(new DpapiSecretStore(_paths).Get("anything"));
    }
}
