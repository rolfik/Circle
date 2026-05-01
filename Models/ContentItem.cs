using System.Text.Json.Serialization;

namespace CircleOfTruthAndLove.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(Folder), "Folder")]
[JsonDerivedType(typeof(Page), "Page")]
public abstract class ContentItem : ContentNode
{
}
