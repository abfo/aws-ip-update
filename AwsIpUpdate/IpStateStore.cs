namespace AwsIpUpdate;

static class IpStateStore
{
    public static string? ReadLastIp()
    {
        if (!File.Exists(AppPaths.LastIpFile)) return null;
        try { return File.ReadAllText(AppPaths.LastIpFile).Trim(); }
        catch { return null; }
    }

    public static void WriteLastIp(string ip)
    {
        AppPaths.EnsureDirectoryExists();
        File.WriteAllText(AppPaths.LastIpFile, ip);
    }
}
