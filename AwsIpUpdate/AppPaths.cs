namespace AwsIpUpdate;

static class AppPaths
{
    private static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AwsIpUpdate");

    public static readonly string ConfigFile = Path.Combine(BaseDir, "config.json");
    public static readonly string LastIpFile  = Path.Combine(BaseDir, "last-ip.txt");

    public static void EnsureDirectoryExists() => Directory.CreateDirectory(BaseDir);
}
