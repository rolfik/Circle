using System.Globalization;
using CircleOfTruthAndLove.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace CircleOfTruthAndLove.Services;

/// <summary>
/// Persists UI/user preferences (theme, navigation header, language, last page)
/// in the browser's localStorage so they survive a reload (F5) or PWA restart.
/// </summary>
public class SettingsService
{
    private const string KeyDarkMode = "app.darkMode";
    private const string KeyShowBreadcrumb = "app.showBreadcrumb";
    private const string KeyCulture = "app.culture";
    private const string KeyLastPageId = "app.lastPageId";
    private const string KeyRotationSpeed = "app.rotationSpeed";
    private const string KeyRotationDirection = "app.rotationDirection";

    private readonly IJSRuntime js;
    private readonly LocalizationOptions localization;
    private bool loaded;

    public SettingsService(IJSRuntime js, IOptions<LocalizationOptions> localization)
    {
        this.js = js;
        this.localization = localization.Value;
        Culture = this.localization.DefaultCulture;
    }

    public bool IsDarkMode { get; private set; }
    public bool ShowBreadcrumb { get; private set; } = true;
    public string Culture { get; private set; }
    public string? LastPageId { get; private set; }

    /// <summary>
    /// Curtain rotation speed in seconds per full revolution. <c>0</c> = no rotation.
    /// Range typically 0..600.
    /// </summary>
    public double RotationSeconds { get; private set; } = 0;

    /// <summary>
    /// Curtain rotation direction. <c>+1</c> = clockwise, <c>-1</c> = counter-clockwise.
    /// </summary>
    public int RotationDirection { get; private set; } = 1;

    public IReadOnlyList<string> SupportedCultures => localization.SupportedCultures;

    public event Action? OnChanged;

    public async Task LoadAsync()
    {
        if (loaded) return;
        IsDarkMode = await GetBoolAsync(KeyDarkMode, false);
        ShowBreadcrumb = await GetBoolAsync(KeyShowBreadcrumb, true);

        var saved = await GetNullableStringAsync(KeyCulture);
        Culture = ResolveCulture(saved);

        LastPageId = await GetNullableStringAsync(KeyLastPageId);
        RotationSeconds = await GetDoubleAsync(KeyRotationSpeed, 0);
        RotationDirection = (await GetDoubleAsync(KeyRotationDirection, 1)) >= 0 ? 1 : -1;
        ApplyCulture(Culture);
        loaded = true;
        OnChanged?.Invoke();
    }

    public async Task SetDarkModeAsync(bool value)
    {
        IsDarkMode = value;
        await js.InvokeVoidAsync("localStorage.setItem", KeyDarkMode, value ? "1" : "0");
        OnChanged?.Invoke();
    }

    public async Task SetShowBreadcrumbAsync(bool value)
    {
        ShowBreadcrumb = value;
        await js.InvokeVoidAsync("localStorage.setItem", KeyShowBreadcrumb, value ? "1" : "0");
        OnChanged?.Invoke();
    }

    public async Task SetCultureAsync(string culture)
    {
        var resolved = ResolveCulture(culture);
        Culture = resolved;
        await js.InvokeVoidAsync("localStorage.setItem", KeyCulture, resolved);
        ApplyCulture(resolved);
        OnChanged?.Invoke();
    }

    public async Task SetLastPageIdAsync(string? pageId)
    {
        LastPageId = pageId;
        if (string.IsNullOrEmpty(pageId))
            await js.InvokeVoidAsync("localStorage.removeItem", KeyLastPageId);
        else
            await js.InvokeVoidAsync("localStorage.setItem", KeyLastPageId, pageId);
    }

    public async Task SetRotationSecondsAsync(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        RotationSeconds = seconds;
        await js.InvokeVoidAsync("localStorage.setItem", KeyRotationSpeed,
            seconds.ToString(CultureInfo.InvariantCulture));
        OnChanged?.Invoke();
    }

    public async Task SetRotationDirectionAsync(int direction)
    {
        RotationDirection = direction >= 0 ? 1 : -1;
        await js.InvokeVoidAsync("localStorage.setItem", KeyRotationDirection,
            RotationDirection.ToString(CultureInfo.InvariantCulture));
        OnChanged?.Invoke();
    }

    private string ResolveCulture(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return localization.DefaultCulture;

        var match = localization.SupportedCultures
            .FirstOrDefault(c => string.Equals(c, candidate, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        var dash = candidate.IndexOf('-');
        if (dash > 0)
        {
            var prefix = candidate[..dash];
            match = localization.SupportedCultures
                .FirstOrDefault(c => string.Equals(c, prefix, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return localization.DefaultCulture;
    }

    private static void ApplyCulture(string culture)
    {
        try
        {
            var ci = new CultureInfo(culture);
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;
        }
        catch
        {
            // ignore invalid culture strings
        }
    }

    private async Task<bool> GetBoolAsync(string key, bool defaultValue)
    {
        var v = await js.InvokeAsync<string?>("localStorage.getItem", key);
        return v switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            _ => defaultValue
        };
    }

    private async Task<string?> GetNullableStringAsync(string key)
    {
        var v = await js.InvokeAsync<string?>("localStorage.getItem", key);
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private async Task<double> GetDoubleAsync(string key, double defaultValue)
    {
        var v = await js.InvokeAsync<string?>("localStorage.getItem", key);
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : defaultValue;
    }
}
