namespace CircleOfTruthAndLove.Models;

public abstract class ContentNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public DateTime Time { get; set; } = DateTime.UtcNow;
    public string? Author { get; set; }
}
