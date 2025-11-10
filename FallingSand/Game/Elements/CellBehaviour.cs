using FallingSand.Game.World;
using Rubedo.Lib;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am CellBehaviour, and I don't have a summary yet.
/// </summary>
public static class CellBehaviour
{
    private static bool CanBeSwapped(SandMatrix matrix, Cell cell, Cell target)
    {
        return target.IsEmpty ||
            (target.element.density < cell.element.density && target.element.elementType == Element.Type.LIQUID && cell.element.liquid_isSand) ||
            (target.element.elementType == Element.Type.GAS && cell.element.elementType == Element.Type.LIQUID) ||
            (target.element.elementType == Element.Type.GAS && cell.element.elementType == Element.Type.GAS && target.element.density > cell.element.density);
    }
    private static bool GasCanSwap(SandMatrix matrix, Cell cell, Cell target)
    {
        return target.IsEmpty ||
            (target.element.elementType == Element.Type.GAS && cell.element.elementType == Element.Type.GAS && Random.Percent == 0);
    }

    #region LIQUID
    public static bool MoveSide(SandMatrix matrix, Cell cell)
    {
        if (cell.xVel > 0)
        {
            int right = CheckSide(matrix, cell, cell.element.liquid_dispersion, 1, out Cell rightTarget);
            if (right < cell.element.liquid_dispersion)
                cell.xVel = -cell.xVel;
            if (right > 0)
            {
                SwapOrDisplace(matrix, cell, rightTarget);
                return true;
            }
            return false;
        }
        else if (cell.xVel < 0)
        {
            int left = CheckSide(matrix, cell, cell.element.liquid_dispersion, -1, out Cell leftTarget);
            if (left < cell.element.liquid_dispersion)
                cell.xVel = -cell.xVel;
            if (left > 0)
            {
                SwapOrDisplace(matrix, cell, leftTarget);
                return true;
            }
            return false;
        }
        else
        {
            int right = CheckSide(matrix, cell, cell.element.liquid_dispersion, 1, out Cell rightTarget);
            int left = CheckSide(matrix, cell, cell.element.liquid_dispersion, -1, out Cell leftTarget);

            if (left == 0 && right == 0)
                return false; //no movement possible.

            bool leftFree = false;
            bool rightFree = false;

            if (left == right)
            {
                leftFree = Rubedo.Lib.Random.Flip;
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
                SwapOrDisplace(matrix, cell, leftTarget);
            }
            else if (rightFree)
            {
                cell.xVel = 1;
                SwapOrDisplace(matrix, cell, rightTarget);
            }

            return leftFree || rightFree;
        }
    }

    private static int CheckSide(SandMatrix matrix, Cell cell, int dispersion, int direction, out Cell destination)
    {
        destination = cell;
        for (int i = 1; i <= dispersion; i++)
        {
            bool free = matrix.TryGetCell(cell.x + (direction * i), cell.y, out Cell target) && CanBeSwapped(matrix, cell, target);
            if (free)
            {
                destination = target;
                continue;
            }
            return i-1;
        }
        return dispersion;
    }

    public static bool TryFall(SandMatrix matrix, Cell cell)
    {
        cell.yVel = System.MathF.Min(cell.element.liquid_maxSpeed, cell.yVel + (matrix.gravity * cell.element.liquid_gravity));
        bool step = false;
        Cell lastValid = cell;
        Cell target = null;
        for (int y = 1; y <= cell.yVel; y++)
        {
            if (!(matrix.TryGetCell(cell.x, cell.y - y, out target) && target.IsEmpty))
            {
                if (target != null && CanBeSwapped(matrix, cell, target))
                { //something we can swap with, so swap the last valid position with this
                    lastValid.Displace(matrix, target);
                    lastValid = target;
                }
                //we hit something!
                cell.yVel = 0;
                break;
            }
            step = true;
            lastValid = target;
            SetNeighborsFreefalling(matrix, cell.x, cell.y - y);
        }
        if (step && lastValid != null)
        { //we can move
            SwapOrDisplace(matrix, cell, lastValid);
        }
        return step && lastValid != null;
    }

    public static bool TryDiagonalDown(SandMatrix matrix, Cell cell)
    {
        bool downLeftFree = matrix.TryGetCell(cell.x - 1, cell.y - 1, out Cell downLeft) && CanBeSwapped(matrix, cell, downLeft);
        bool downRightFree = matrix.TryGetCell(cell.x + 1, cell.y - 1, out Cell downRight) && CanBeSwapped(matrix, cell, downRight);

        if (downLeftFree && downRightFree)
        {
            downLeftFree = Rubedo.Lib.Random.Flip;
            downRightFree = !downLeftFree;
        }

        if (downLeftFree)
        {
            cell.xVel = -1;
            SwapOrDisplace(matrix, cell, downLeft);
        }
        else if (downRightFree)
        {
            cell.xVel = 1;
            SwapOrDisplace(matrix, cell, downRight);
        }

        return downLeftFree || downRightFree;
    }
    #endregion

    #region GAS


    public static bool TryRise(SandMatrix matrix, Cell cell)
    {
        bool Check(int x, int y)
        {
            bool free = matrix.TryGetCell(cell.x + x, cell.y + y, out Cell target) && (GasCanSwap(matrix, cell, target) || CanBeSwapped(matrix, cell, target));
            if (free)
                cell.SwapPositions(matrix, target);
            return free;
        }

        int chance = Random.Range(0, 12);
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

    private static void SwapOrDisplace(SandMatrix matrix, Cell cell, Cell target)
    {
        if (!target.IsEmpty)
            cell.Displace(matrix, target);
        else
            cell.SwapPositions(matrix, target);
    }
    private static void SetNeighborsFreefalling(SandMatrix matrix, int x, int y)
    {
        matrix.SetFreeFalling(x + 1, y);
        matrix.SetFreeFalling(x - 1, y);
    }
}