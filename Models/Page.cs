namespace Circle.Models;

public class Page : ContentItem
{
    /// <summary>
    /// Required content of this page.
    /// </summary>
    public PageContent Content { get; set; } = new();
}
