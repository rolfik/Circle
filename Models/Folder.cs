namespace Circle.Models;

public class Folder : ContentItem
{
    public List<ContentItem> Items { get; set; } = [];

    /// <summary>
    /// Optional folder name on disk. Defaults to <see cref="ContentNode.Id"/> when null.
    /// </summary>
    public string? FolderName { get; set; }

    /// <summary>
    /// Optional content shown when the folder itself is selected in the menu.
    /// Defaults to a new <see cref="PageContent"/> so the folder can be selected without explicit JSON.
    /// Set to <c>null</c> in JSON to opt out (the folder node then only expands the menu).
    /// </summary>
    public PageContent? Content { get; set; } = new();

    public string EffectiveFolderName => string.IsNullOrWhiteSpace(FolderName) ? Id : FolderName!;
}
