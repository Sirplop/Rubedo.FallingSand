using FallingSand.Game.World;
using Loyc.Syntax;
using Rubedo;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Liquid, and I don't have a summary yet.
/// </summary>
public class Liquid : Element
{
    public Liquid(string name)
    {
        this.elementType = Type.LIQUID;
        this.internalName = name;
    }


    public override void Step(WorldChunk caller, Cell cell)
    {
        if (liquid_isStatic)
            return;


        if (cell.freeFalling)
        {
            cell.xVel *= 0.8f;
        }

        if (liquid_isSand)
        {
            if (cell.freeFalling)
            {
                if (CellBehaviour.TryMoveDownTriple(caller, cell))
                    return;

                cell.freeFallingCount++;
                if (cell.freeFallingCount >= Cell.FREE_FALLING_THRESHOLD)
                {
                    cell.freeFalling = false; //failed to move.
                    cell.yVel = 0;
                    cell.xVel = 0;
                }
            }
            else
            {
                if (CellBehaviour.TryMoveDown(caller, cell))
                    return;
            }
        }
        else //this is a liquid like water
        {
            if (CellBehaviour.TryMoveDownTriple(caller, cell))
                return;

            if (CellBehaviour.TryMoveSide(caller, cell))
            {
                cell.yVel *= 0.5f; //we should stop moving down so fast.
                return;
            }

            cell.freeFallingCount++;
            if (cell.freeFallingCount >= Cell.FREE_FALLING_THRESHOLD)
            {
                cell.freeFallingCount = 0;
                cell.yVel = 0;
                cell.xVel = 0;
            }

        }

        cell.lastFrame = Time.RunningTime;
    }
}