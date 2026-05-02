namespace Circle.Models;

public class Page : ContentItem
{
    /// <summary>
    /// Optional content of this page. When <c>null</c>, the page is shown in
    /// the navigation but cannot be activated (treated like an empty package).
    /// </summary>
    public PageContent? Content { get; set; }
}
