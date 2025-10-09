using Yeek.Security.Model;

namespace Yeek.Configuration;

public class JavaScriptConfiguration
{
    public const string Name = "JavaScript";

    public bool Enable { get; set; } = true;
    public TrustLevel RequiredTrust { get; set; } = TrustLevel.Trusted;

    /// <summary>
    /// Allowed characters for a js script.
    /// </summary>
    public long AllowedCharacters { get; set; } = 4096;
}