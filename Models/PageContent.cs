namespace Circle.Models;

/// <summary>
/// Animated transition used when this page becomes the active page.
/// </summary>
public enum PageTransition
{
    None,
    Fade,
    Slide,
    IrisWipe,
    ScaleZoom
}

/// <summary>
/// Describes the renderable content of a page (file + type + transition + presentation).
/// FileName is optional in JSON; when omitted it is derived from the owner's Id.
/// </summary>
public class PageContent
{
    public PageType Type { get; set; } = PageType.Svg;
    public string? FileName { get; set; }
    public PageTransition Transition { get; set; } = PageTransition.Fade;

    /// <summary>
    /// When true the curtain retracts (fullscreen presentation) while this page is active.
    /// </summary>
    public bool FullScreen { get; set; }

    /// <summary>
    /// Minimum scale of the page content during a curtain pulse cycle.
    /// One pulse cycle matches one curtain revolution (uses RotationSeconds).
    /// 1.0 (default) disables pulsing. Active only when the curtain is visible (not FullScreen)
    /// and is rotating (RotationSeconds &gt; 0).
    /// Typical range: 0.1–1.0.
    /// </summary>
    public double PulseMinScale { get; set; } = 1.0;

    /// <summary>
    /// Absolute resolved URL to the content file. Filled by ContentManager.
    /// </summary>
    public string? ResolvedPath { get; set; }
}
