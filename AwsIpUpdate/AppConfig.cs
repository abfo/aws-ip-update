using System.Text.Json;
using System.Text.Json.Serialization;

namespace AwsIpUpdate;

class AppConfig
{
    public string AwsAccessKeyId     { get; set; } = "";
    public string AwsSecretAccessKey { get; set; } = "";
    public string AwsRegion          { get; set; } = "us-east-1";
    public string SecurityGroupId    { get; set; } = "";

    /// <summary>
    /// Written into every IpRange.Description we create so we can
    /// identify and update only the rules we own.
    /// </summary>
    public string RuleOwnerTag { get; set; } = "managed-by-AwsIpUpdate";

    public List<RuleConfig> Rules { get; set; } = [];

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AwsAccessKeyId)     &&
        !string.IsNullOrWhiteSpace(AwsSecretAccessKey) &&
        !string.IsNullOrWhiteSpace(SecurityGroupId)    &&
        Rules.Count > 0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Loads config from disk. Returns null if the file is absent or malformed.
    /// </summary>
    public static AppConfig? TryLoad()
    {
        if (!File.Exists(AppPaths.ConfigFile))
            return null;

        try
        {
            string json = File.ReadAllText(AppPaths.ConfigFile);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a starter config file so the user has something to edit.
    /// </summary>
    public static void WriteTemplate()
    {
        AppPaths.EnsureDirectoryExists();
        if (File.Exists(AppPaths.ConfigFile)) return;

        var template = new AppConfig
        {
            AwsAccessKeyId     = "AKIAIOSFODNN7EXAMPLE",
            AwsSecretAccessKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
            AwsRegion          = "us-east-1",
            SecurityGroupId    = "sg-0123456789abcdef0",
            RuleOwnerTag       = "managed-by-AwsIpUpdate",
            Rules =
            [
                new RuleConfig { Protocol = "tcp", FromPort = 3389, ToPort = 3389 },
            ],
        };

        File.WriteAllText(AppPaths.ConfigFile,
            JsonSerializer.Serialize(template, JsonOptions));
    }
}

class RuleConfig
{
    public string Protocol { get; set; } = "tcp";
    public int    FromPort { get; set; }
    public int    ToPort   { get; set; }
}
