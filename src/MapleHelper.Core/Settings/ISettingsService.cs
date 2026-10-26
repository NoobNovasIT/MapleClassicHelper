using MapleHelper.Core.Ai;

namespace MapleHelper.Core.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    event EventHandler? Changed;
    Task SaveAsync(CancellationToken ct = default);
}

/// <summary>Kho bí mật (API key). Bản Windows mã hoá bằng DPAPI theo tài khoản người dùng.</summary>
public interface ISecretStore
{
    string? Get(string key);
    void Set(string key, string value);
    void Remove(string key);
}

public static class SecretKeys
{
    public static string ApiKey(AiProviderKind kind) => $"ai.{kind}.apiKey";
}
