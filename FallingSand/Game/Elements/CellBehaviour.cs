using FallingSand.Game.World;
using Rubedo;
using Rubedo.Lib;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am CellBehaviour, and I don't have a summary yet.
/// </summary>
public static class CellBehaviour
{
    public static bool CanBeSwapped(Cell cell, Cell target)
    {
        return target.IsEmpty ||
            (target.element.density < cell.element.density && target.element.elementType == Element.Type.LIQUID && (!cell.element.liquid_isSand || !target.element.liquid_isSand)) ||
            (target.element.elementType == Element.Type.GAS && cell.element.elementType == Element.Type.LIQUID) ||
            (target.element.elementType == Element.Type.GAS && cell.element.elementType == Element.Type.GAS && target.element.density > cell.element.density);
    }
    private static bool GasCanSwap(WorldChunk caller, Cell cell, Cell target)
    {
        return target.IsEmpty ||
            (target.element.elementType == Element.Type.GAS && cell.element.elementType == Element.Type.GAS);
    }

    #region LIQUID
    public static bool MoveSide(WorldChunk caller, Cell cell)
    {
        if (cell.xVel > 0)
        {
            int right = CheckSide(caller, cell, cell.element.liquid_dispersion, 1, out Cell rightTarget);
            if (right < cell.element.liquid_dispersion)
                cell.xVel = -cell.xVel;
            if (right > 0)
            {
                SwapOrDisplace(caller, cell, rightTarget);
                return true;
            }
            return false;
        }
        else if (cell.xVel < 0)
        {
            int left = CheckSide(caller, cell, cell.element.liquid_dispersion, -1, out Cell leftTarget);
            if (left < cell.element.liquid_dispersion)
                cell.xVel = -cell.xVel;
            if (left > 0)
            {
                SwapOrDisplace(caller, cell, leftTarget);
                return true;
            }
            return false;
        }
        else
        {
            int right = CheckSide(caller, cell, cell.element.liquid_dispersion, 1, out Cell rightTarget);
            int left = CheckSide(caller, cell, cell.element.liquid_dispersion, -1, out Cell leftTarget);

            if (left == 0 && right == 0)
                return false; //no movement possible.

            bool leftFree = false;
            bool rightFree = false;

            if (left == right)
            {
                leftFree = caller.ChunkRNG.Flip();
                rightFree = !leftFree;
            }
            else
            {
                leftFree = left > right;
                rightFree = !leftFree;
            }

            if (leftFree)
            {
                cell.xVel = -1;
                SwapOrDisplace(caller, cell, leftTarget);
            }
            else if (rightFree)
            {
                cell.xVel = 1;
                SwapOrDisplace(caller, cell, rightTarget);
            }

            return leftFree || rightFree;
        }
    }

    /*private static int CheckSide(WorldChunk caller, Cell source, int dispersion, int direction, out Cell destination)
    {

    }*/

    
    private static int CheckSide(WorldChunk caller, Cell cell, int dispersion, int direction, out Cell destination)
    {
        destination = cell;
        for (int i = 1; i <= dispersion; i++)
        {
            bool free = caller.TryGetCell(cell.x + (direction * i), cell.y, out Cell target) && CanBeSwapped(cell, target);
            if (free)
            {
                destination = target;
                continue;
            }
            return i-1;
        }
        return dispersion;
    }

    public static bool TryFall(WorldChunk caller, Cell cell)
    {
        cell.yVel = System.MathF.Min(cell.element.liquid_maxSpeed, cell.yVel + (caller.parentMatrix.gravity * cell.element.liquid_gravity));
        bool success = false;
        Cell target = null;
        int cellX = cell.x;
        int cellY = cell.y;
        for (int y = 1; y <= cell.yVel; y++)
        {
            if (caller.TryGetCell(cellX, cellY - y, out target))
            { //we found a cell below us!
                if (target.IsEmpty)
                { //we can swap immediately.
                    cell.SwapPositions(caller, target);
                    success = true;
                }
                else
                {
                    if (CanBeSwapped(cell, target))
                    {
                        SwapOrDisplace(caller, cell, target);
                        success = true;
                    }
                    //we've hit something!
                    ConvertVerticalToHorizontalMotion(cell);
                    break;
                }
            }
            else
            {
                //edge of the map.
                SetNeighborsFreefalling(caller, cell.x, cell.y);
                ConvertVerticalToHorizontalMotion(cell);
                break;
            }
        }
        return success;
    }

    public static bool TryDiagonalDown(WorldChunk caller, Cell cell)
    {
        //if (caller.ChunkRNG.Percent() < cell.element.liquid_inertialResistance)
        //    return false;

        bool downLeftFree = caller.TryGetCell(cell.x - 1, cell.y - 1, out Cell downLeft) && CanBeSwapped(cell, downLeft);
        bool downRightFree = caller.TryGetCell(cell.x + 1, cell.y - 1, out Cell downRight) && CanBeSwapped(cell, downRight);

        if (downLeftFree && downRightFree)
        {
            downLeftFree = caller.ChunkRNG.Flip();
            downRightFree = !downLeftFree;
        }

        if (downLeftFree)
        {
            cell.xVel = -1;
            SwapOrDisplace(caller, cell, downLeft);
        }
        else if (downRightFree)
        {
            cell.xVel = 1;
            SwapOrDisplace(caller, cell, downRight);
        }

        return downLeftFree || downRightFree;
    }
    #endregion

    #region GAS

    public static bool TryDiagonalUp(WorldChunk caller, Cell cell)
    {
        bool leftFree = caller.TryGetCell(cell.x - 1, cell.y + 1, out Cell left) && CanBeSwapped(cell, left);
        bool rightFree = caller.TryGetCell(cell.x + 1, cell.y + 1, out Cell right) && CanBeSwapped(cell, right);

        if (leftFree && rightFree)
        {
            leftFree = caller.ChunkRNG.Flip();
            rightFree = !leftFree;
        }

        if (leftFree)
        {
            cell.xVel = -1;
            SwapOrDisplace(caller, cell, left);
        }
        else if (rightFree)
        {
            cell.xVel = 1;
            SwapOrDisplace(caller, cell, right);
        }

        return leftFree || rightFree;
    }

    public static bool TryRise(WorldChunk caller, Cell cell)
    {
        bool free = caller.TryGetCell(cell.x, cell.y + 1, out Cell target) && CanBeSwapped(cell, target);
        if (free)
            cell.SwapPositions(caller, target);
        return free;
    }
    #endregion

    public static void SwapOrDisplace(WorldChunk caller, Cell cell, Cell target)
    {
        if (!target.IsEmpty)
            cell.Displace(caller, target);
        else
            cell.SwapPositions(caller, target);
    }
    public static void SetNeighborsFreefalling(WorldChunk caller, int x, int y)
    {
        caller.SetFreeFalling(caller, x + 1, y);
        caller.SetFreeFalling(caller, x - 1, y);
    }

    public static void ConvertVerticalToHorizontalMotion(Cell cell)
    {
        float absY = System.Math.Abs(cell.yVel);
        cell.xVel = cell.xVel > 0 ? absY : -absY;
        cell.yVel = 0;
    }
}