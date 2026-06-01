using FallingSand.Game.World;
using Rubedo;
using Rubedo.Lib;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am CellBehaviour, and I don't have a summary yet.
/// </summary>
public static class CellBehaviour
{
    private static bool CanBeSwapped(Cell cell, Cell target)
    {
        return target.IsEmpty ||
            (target.element.density < cell.element.density && target.element.elementType == Element.Type.LIQUID && cell.element.liquid_isSand) ||
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
        bool step = false;
        Cell lastValid = cell;
        Cell target = null;
        for (int y = 1; y <= cell.yVel; y++)
        {
            if (!(caller.TryGetCell(cell.x, cell.y - y, out target) && target.IsEmpty))
            {
                if (target != null && CanBeSwapped(cell, target))
                { //something we can swap with, so swap the last valid position with this
                    lastValid.Displace(caller, target);
                    lastValid = target;
                }
                //we hit something!
                cell.yVel = 0;
                break;
            }
            step = true;
            lastValid = target;
            SetNeighborsFreefalling(caller, cell.x, cell.y - y);
        }
        if (step && lastValid != null)
        { //we can move
            SwapOrDisplace(caller, cell, lastValid);
        }
        return step && lastValid != null;
    }

    public static bool TryDiagonalDown(WorldChunk caller, Cell cell)
    {
        if (caller.ChunkRNG.Percent() < cell.element.liquid_inertialResistance)
            return false;

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

    public static bool TryRise(WorldChunk caller, Cell cell)
    {
        bool Check(int x, int y)
        {
            bool free = caller.TryGetCell(cell.x + x, cell.y + y, out Cell target) && CanBeSwapped(cell, target);
            if (free)
                cell.SwapPositions(caller, target);
            return free;
        }

        int chance = caller.ChunkRNG.Range(0, 12);
        switch (chance)
        {
            case 0:
            {
                if (Check(-1, 0)) return true;
                if (Check(1, 0)) return true;
                if (Check(1, 1)) return true;
                if (Check(-1, 1)) return true;
                if (Check(0, 1)) return true;
                break;
            }
            case 1:
            {
                if (Check(1, 0)) return true;
                if (Check(1, 1)) return true;
                if (Check(-1, 1)) return true;
                if (Check(0, 1)) return true;
                if (Check(-1, 0)) return true;
                break;
            }
            case 2:
            {
                if (Check(1, 1)) return true;
                if (Check(-1, 1)) return true;
                if (Check(0, 1)) return true;
                if (Check(-1, 0)) return true;
                if (Check(1, 0)) return true;
                break;
            }
            case 3:
            {
                if (Check(-1, 1)) return true;
                if (Check(0, 1)) return true;
                if (Check(-1, 0)) return true;
                if (Check(1, 0)) return true;
                if (Check(1, 1)) return true;
                break;
            }
            case 4:
            case 5:
            case 6:
            case 7:
            case 8:
            case 9:
            case 10:
            case 11:
            {
                if (Check(0, 1)) return true;
                if (Check(-1, 0)) return true;
                if (Check(1, 0)) return true;
                if (Check(1, 1)) return true;
                if (Check(-1, 1)) return true;
                break;
            }
        }
        return false;
    }
    #endregion

    private static void SwapOrDisplace(WorldChunk caller, Cell cell, Cell target)
    {
        if (!target.IsEmpty)
            cell.Displace(caller, target);
        else
            cell.SwapPositions(caller, target);
    }
    private static void SetNeighborsFreefalling(WorldChunk caller, int x, int y)
    {
        caller.SetFreeFalling(caller, x + 1, y);
        caller.SetFreeFalling(caller, x - 1, y);
    }
}