namespace Circle.Configuration;

/// <summary>
/// Localization options bound from <c>wwwroot/appsettings.json</c>.
/// </summary>
public class LocalizationOptions
{
    public string DefaultCulture { get; set; } = "en";
    public List<string> SupportedCultures { get; set; } = ["en"];
}
