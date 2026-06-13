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
        this.internalName = name;
    }

    public override void Step(WorldChunk caller, Cell cell)
    {
        if (caller.ChunkRNG.Percent() < 25)
        { //try to move diagonally first
            if (CellBehaviour.TryDiagonalUp(caller, cell))
                return;
            else if (CellBehaviour.TryRise(caller, cell))
                return;
        }
        else
        {
            if (CellBehaviour.TryRise(caller, cell))
                return;
            else if (CellBehaviour.TryDiagonalUp(caller, cell))
                return;
        }
        if (CellBehaviour.MoveSide(caller, cell))
            return;
    }
}