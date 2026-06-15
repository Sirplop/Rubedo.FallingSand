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

    /*
    public override void Step(WorldChunk caller, Cell cell)
    {
        if (liquid_isStatic)
            return;

        if (caller.TryGetCell(cell.x, cell.y - 1, out Cell t) && t.IsEmpty)
        {
            float velUpdate = caller.parentMatrix.gravity * cell.element.liquid_gravity * (Time.FixedDeltaTime);
            cell.yVel = System.MathF.Min(cell.element.liquid_maxSpeed, cell.yVel + velUpdate);
        }

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
                else if (CellBehaviour.TryDiagonalDown(caller, cell))
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
            if (CellBehaviour.TryMoveSide(caller, cell))
                return;
        }

        cell.freeFallingCount++;
        if (cell.freeFallingCount >= Cell.FREE_FALLING_THRESHOLD)
            cell.freeFalling = false; //failed to move.
    }*/


    public override void Step(WorldChunk caller, Cell cell)
    {
        if (liquid_isStatic)
            return;


        if (cell.freeFalling)
        {
            float velUpdate = caller.parentMatrix.gravity * cell.element.liquid_gravity * Time.FixedDeltaTime * 2;
            cell.yVel = System.MathF.Min(cell.element.liquid_maxSpeed, cell.yVel - velUpdate);
            cell.xVel *= 0.8f;
        }

        if (liquid_isSand)
        {
            if (cell.freeFalling)
            {
                if (caller.ChunkRNG.Percent() < 25)
                {
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

                cell.freeFallingCount++;
                if (cell.freeFallingCount >= Cell.FREE_FALLING_THRESHOLD)
                    cell.freeFalling = false; //failed to move.
            }
            else
            {
                if (CellBehaviour.TryMoveDown(caller, cell))
                    return;
            }
        }
        else //this is a liquid like water
        {
            if (caller.ChunkRNG.Percent() < 25)
            {
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
            if (CellBehaviour.TryMoveSide(caller, cell))
                return;
        }

        cell.lastFrame = Time.RunningTime;
    }
}