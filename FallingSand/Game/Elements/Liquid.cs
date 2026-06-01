using FallingSand.Game.World;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Liquid, and I don't have a summary yet.
/// </summary>
public class Liquid : Element
{
    public Liquid(string name)
    {
        this.elementType = Type.LIQUID;
        this.name = name;
    }

    public override void Step(WorldChunk caller, Cell cell)
    {
        if (liquid_isStatic)
            return;

        if (liquid_isSand)
        {
            if (cell.freeFalling && caller.ChunkRNG.Percent() < 15)
            { //try to move diagonally first
                if (CellBehaviour.TryDiagonalDown(caller, cell))
                    return;
                else if (CellBehaviour.TryFall(caller, cell))
                    return;
            }
            else
            {
                if (CellBehaviour.TryFall(caller, cell))
                    return;
                else if (cell.freeFalling && CellBehaviour.TryDiagonalDown(caller, cell))
                    return;
            }
        }
        else
        {
            if (cell.freeFalling && caller.ChunkRNG.Percent() < 25)
            { //try to move diagonally first
                if (CellBehaviour.TryDiagonalDown(caller, cell))
                    return;
                else if (CellBehaviour.TryFall(caller, cell))
                    return;
            }
            else
            {
                if (CellBehaviour.TryFall(caller, cell))
                    return;
                else if (CellBehaviour.TryDiagonalDown(caller, cell))
                    return;
            }
            if (CellBehaviour.MoveSide(caller, cell))
                return;
        }

        cell.freeFallingCount++;
        if (cell.freeFallingCount >= Cell.FREE_FALLING_THRESHOLD)
            cell.freeFalling = false; //failed to move.
    }
}