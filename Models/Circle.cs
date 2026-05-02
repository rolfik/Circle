namespace Circle.Models;

/// <summary>
/// Top-level content node representing a thematic Circle (Kruh).
/// A Circle owns its visual curtain SVG, an optional default page shown when the
/// circle itself is selected in navigation, and a list of packages.
/// </summary>
public class Circle : ContentNode
{
    /// <summary>
    /// Packages belonging to this circle. Populated by <see cref="Services.ContentManager"/>
    /// from the per-circle <c>packages.json</c>.
    /// </summary>
    public List<Package> Packages { get; set; } = [];

    /// <summary>
    /// Optional folder name on disk. Defaults to <see cref="ContentNode.Id"/> when null.
    /// </summary>
    public string? FolderName { get; set; }

    /// <summary>
    /// Optional default content shown when the circle itself is selected/hovered in the menu.
    /// Set to <c>null</c> in JSON to opt out (the circle node then only expands the menu).
    /// </summary>
    public PageContent? Content { get; set; } = new();

    /// <summary>
    /// File name of the curtain SVG inside the circle folder. Defaults to <c>curtain.svg</c>.
    /// </summary>
    public string? CurtainFileName { get; set; }

    /// <summary>
    /// Absolute resolved URL to the curtain SVG. Filled by <see cref="Services.ContentManager"/>.
    /// </summary>
    public string? CurtainPath { get; set; }

    public string EffectiveFolderName => string.IsNullOrWhiteSpace(FolderName) ? Id : FolderName!;
}
