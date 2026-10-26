using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MapleHelper.Core.Settings;

namespace MapleHelper.Infrastructure.Settings;

/// <summary>
/// Lưu API key trong secrets.dat, mã hoá bằng DPAPI (CurrentUser).
/// Copy file sang máy hoặc tài khoản Windows khác sẽ không giải mã được.
/// </summary>
public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MapleClassicHelper.v1");

    private readonly string _filePath;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, string> _secrets;

    public DpapiSecretStore(AppPaths paths)
    {
        _filePath = paths.SecretsFile;
        _secrets = Load(_filePath);
    }

    public string? Get(string key)
    {
        lock (_lock)
        {
            return _secrets.GetValueOrDefault(key);
        }
    }

    public void Set(string key, string value)
    {
        lock (_lock)
        {
            _secrets[key] = value;
            Save();
        }
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            if (_secrets.Remove(key))
            {
                Save();
            }
        }
    }

    private void Save()
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(_secrets);
        var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_filePath, encrypted);
    }

    private static Dictionary<string, string> Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        try
        {
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(filePath), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(plain) ?? [];
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            // File của tài khoản khác hoặc bị hỏng: coi như chưa có key, người dùng nhập lại
            return [];
        }
    }
}
