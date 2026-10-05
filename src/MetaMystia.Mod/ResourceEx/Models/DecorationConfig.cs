using GameData.Core.Collections;

namespace MetaMystia.ResourceEx.Models;

public class DecorationConfig : ItemConfig
{
    public string implementation { get; set; }
    public Decoration.DecorationType decorationType { get; set; } = Decoration.DecorationType.Outdoor;
    public int[] conflictDecorationIds { get; set; } = [];
    public int? buffId { get; set; }
}
