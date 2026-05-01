namespace CircleOfTruthAndLove.Models;

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
    /// Absolute resolved URL to the content file. Filled by ContentManager.
    /// </summary>
    public string? ResolvedPath { get; set; }
}
