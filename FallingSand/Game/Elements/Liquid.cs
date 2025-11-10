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

    public override void Step(SandMatrix matrix, Cell cell)
    {
        if (liquid_isStatic)
            return;

        if (liquid_isSand)
        {
            if (cell.freeFalling && Rubedo.Lib.Random.Percent < 15)
            { //try to move diagonally first
                if (CellBehaviour.TryDiagonalDown(matrix, cell))
                    return;
                else if (CellBehaviour.TryFall(matrix, cell))
                    return;
            }
            else
            {
                if (CellBehaviour.TryFall(matrix, cell))
                    return;
                else if (cell.freeFalling && CellBehaviour.TryDiagonalDown(matrix, cell))
                    return;
            }
        }
        else
        {
            if (cell.freeFalling && Rubedo.Lib.Random.Percent < 25)
            { //try to move diagonally first
                if (CellBehaviour.TryDiagonalDown(matrix, cell))
                    return;
                else if (CellBehaviour.TryFall(matrix, cell))
                    return;
            }
            else
            {
                if (CellBehaviour.TryFall(matrix, cell))
                    return;
                else if (CellBehaviour.TryDiagonalDown(matrix, cell))
                    return;
            }
            if (CellBehaviour.MoveSide(matrix, cell))
                return;
        }

        //cell.freeFallingCount++;
        //if (cell.freeFallingCount >= Cell.FREE_FALLING_THRESHOLD)
            cell.freeFalling = false; //failed to move.

        /*
        if (!isStatic)
        {
            if (isSand)
            {
                if (cell.xVel != 0 && cell.freeFalling)
                {
                    bool couldMoveDown = CellBehaviour.CouldMoveDown(matrix, cell);
                    if (CellBehaviour.MoveDownDiagonalFromVel(matrix, cell))
                    {
                        if (couldMoveDown)
                            cell.xVel = 0;
                        return;
                    }
                    if (couldMoveDown)
                        cell.xVel = 0;
                }
                if (CellBehaviour.MoveDown(matrix, cell))
                    return;
                else if (cell.freeFalling && CellBehaviour.MoveDownDiagonal(matrix, cell))
                    return;
            }
            else
            {
                if (CellBehaviour.MoveDown(matrix, cell))
                    return;
                else if (CellBehaviour.MoveDownDiagonal(matrix, cell))
                    return;
                if (CellBehaviour.MoveSide(matrix, cell, dispersion))
                    return;
            }
            cell.freeFallingCount++;
            if (cell.freeFallingCount >= Cell.FREE_FALLING_THRESHOLD)
                cell.freeFalling = false; //failed to move.
        }*/
    }
}