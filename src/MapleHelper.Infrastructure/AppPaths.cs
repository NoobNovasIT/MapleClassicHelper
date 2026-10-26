namespace MapleHelper.Infrastructure;

/// <summary>Toàn bộ dữ liệu người dùng nằm trong %AppData%\MapleClassicHelper. Test truyền thư mục tạm vào.</summary>
public sealed class AppPaths
{
    public AppPaths(string root)
    {
        Root = root;
        Directory.CreateDirectory(Root);
    }

    public static AppPaths Default() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MapleClassicHelper"));

    public string Root { get; }
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string SecretsFile => Path.Combine(Root, "secrets.dat");
    public string DatabaseFile => Path.Combine(Root, "data.db");
    public string LogsDirectory => Path.Combine(Root, "logs");
}
