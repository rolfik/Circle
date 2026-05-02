using System.Text.Json.Serialization;

namespace Circle.Models;

public abstract class ContentNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public DateTime Time { get; set; } = DateTime.UtcNow;
    public string? Author { get; set; }

    /// <summary>
    /// Globally unique slash-separated path to this node, composed from local
    /// <see cref="Id"/>s along the tree (e.g. <c>TruthAndLove/test/chapter-1/basics/p-basics-1</c>).
    /// Filled by <see cref="Services.ContentManager"/> at load time; never serialized.
    /// </summary>
    [JsonIgnore]
    public string Path { get; set; } = string.Empty;
}
