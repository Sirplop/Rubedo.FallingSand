using FallingSand.Game.World;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Gas, and I don't have a summary yet.
/// </summary>
public class Gas : Element
{
    public Gas(string name)
    {
        this.elementType = Type.GAS;
        this.name = name;
    }

    public override void Step(SandMatrix matrix, Cell cell)
    {
        CellBehaviour.TryRise(matrix, cell);
    }
}