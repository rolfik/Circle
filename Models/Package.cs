using System.Text.Json.Serialization;

namespace Circle.Models;

public class Package : ContentNode
{
    public List<ContentItem> Items { get; set; } = [];
    public bool IsDownloaded { get; set; }

    /// <summary>
    /// Parent circle reference. Set by <see cref="Services.ContentManager"/> after loading;
    /// not serialized to/from JSON.
    /// </summary>
    [JsonIgnore]
    public Circle? Circle { get; set; }

    /// <summary>
    /// Optional folder name on disk. Defaults to <see cref="ContentNode.Id"/> when null.
    /// </summary>
    public string? FolderName { get; set; }

    /// <summary>
    /// Optional content shown when the package itself is selected in the menu.
    /// Defaults to a new <see cref="PageContent"/> so the package can be selected without explicit JSON.
    /// Set to <c>null</c> in JSON to opt out (the package node then only expands the menu).
    /// </summary>
    public PageContent? Content { get; set; } = new();

    public string EffectiveFolderName => string.IsNullOrWhiteSpace(FolderName) ? Id : FolderName!;
}
