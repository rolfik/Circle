using Circle;
using Circle.Configuration;
using Circle.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.AddScoped<ContentManager>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<AppUpdateService>();

builder.Services.Configure<KeyboardShortcuts>(builder.Configuration.GetSection("KeyboardShortcuts"));
builder.Services.Configure<LocalizationOptions>(builder.Configuration.GetSection("Localization"));

var locOptions = builder.Configuration.GetSection("Localization").Get<LocalizationOptions>() ?? new LocalizationOptions();

var host = builder.Build();

// Apply persisted/auto-detected culture before the first render so all resources
// resolve correctly. SettingsService.LoadAsync() owns the actual detection
// (localStorage -> navigator.language -> DefaultCulture); here we just prime
// CurrentCulture from whatever it persisted last time, falling back to the
// configured default if nothing is stored yet.
var js = host.Services.GetRequiredService<IJSRuntime>();
string? initial = null;
try
{
    initial = await js.InvokeAsync<string?>("localStorage.getItem", "app.culture");
    if (string.IsNullOrWhiteSpace(initial))
    {
        var browser = await js.InvokeAsync<string?>("eval",
            "navigator.language || (navigator.languages && navigator.languages[0]) || navigator.userLanguage || ''");
        initial = ResolveSupportedCulture(browser, locOptions);
    }
    else if (!IsSupported(initial, locOptions))
    {
        initial = locOptions.DefaultCulture;
    }
}
catch
{
    initial = locOptions.DefaultCulture;
}

try
{
    var ci = new CultureInfo(initial ?? locOptions.DefaultCulture);
    CultureInfo.DefaultThreadCurrentCulture = ci;
    CultureInfo.DefaultThreadCurrentUICulture = ci;
    CultureInfo.CurrentCulture = ci;
    CultureInfo.CurrentUICulture = ci;
}
catch
{
    // ignore invalid stored culture
}

await host.RunAsync();

static bool IsSupported(string? culture, LocalizationOptions options) =>
    !string.IsNullOrEmpty(culture) &&
    options.SupportedCultures.Any(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));

static string ResolveSupportedCulture(string? browserCulture, LocalizationOptions options)
{
    if (string.IsNullOrWhiteSpace(browserCulture))
        return options.DefaultCulture;

    // Try exact match, then two-letter prefix (e.g. "cs-CZ" -> "cs").
    if (IsSupported(browserCulture, options))
        return options.SupportedCultures.First(c => string.Equals(c, browserCulture, StringComparison.OrdinalIgnoreCase));

    var dash = browserCulture.IndexOf('-');
    var prefix = dash > 0 ? browserCulture[..dash] : browserCulture;
    if (IsSupported(prefix, options))
        return options.SupportedCultures.First(c => string.Equals(c, prefix, StringComparison.OrdinalIgnoreCase));

    return options.DefaultCulture;
}
