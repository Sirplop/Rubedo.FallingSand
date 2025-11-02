using FallingSand.Game.World;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Rubedo;
using Rubedo.Lib;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am CellBehaviour, and I don't have a summary yet.
/// </summary>
public static class CellBehaviour
{
    private static bool CanBeSwapped(SandMatrix matrix, Cell cell, Cell target)
    {
        return target.IsEmpty || target.element.density < cell.element.density;
    }

    public static bool MoveDown(SandMatrix matrix, Cell cell)
    {
        if (matrix.TryGetCell(cell.x, cell.y - 1, out Cell target) && CanBeSwapped(matrix, cell, target))
        {
            cell.SwapPositions(matrix, target);
            return true;
        }
        return false;
    }

    public static bool MoveDownDiagonal(SandMatrix matrix, Cell cell)
    {
        bool downLeftFree = matrix.TryGetCell(cell.x - 1, cell.y - 1, out Cell downLeft) && CanBeSwapped(matrix, cell, downLeft);
        bool downRightFree = matrix.TryGetCell(cell.x + 1, cell.y - 1, out Cell downRight) && CanBeSwapped(matrix, cell, downRight);

        if (downLeftFree && downRightFree)
        {
            downLeftFree = Rubedo.Lib.Random.Flip;
            downRightFree = !downLeftFree;
        }

        if (downLeftFree)       cell.SwapPositions(matrix, downLeft);
        else if (downRightFree) cell.SwapPositions(matrix, downRight);

        return downLeftFree || downRightFree;
    }

    public static bool MoveDown3Dir(SandMatrix matrix, Cell cell)
    {
        bool downFree = matrix.TryGetCell(cell.x, cell.y - 1, out Cell down) && CanBeSwapped(matrix, cell, down);
        bool downLeftFree = matrix.TryGetCell(cell.x - 1, cell.y - 1, out Cell downLeft) && CanBeSwapped(matrix, cell, downLeft);
        bool downRightFree = matrix.TryGetCell(cell.x + 1, cell.y - 1, out Cell downRight) && CanBeSwapped(matrix, cell, downRight);

        int flip = Rubedo.Lib.Random.Range(0, 3);
        switch (flip)
        {
            case 0:
                if (downFree)           cell.SwapPositions(matrix, down);
                else if (downLeftFree)  cell.SwapPositions(matrix, downLeft);
                else if (downRightFree) cell.SwapPositions(matrix, downRight);
                break;
            case 1:
                if (downLeftFree)       cell.SwapPositions(matrix, downLeft);
                else if (downRightFree) cell.SwapPositions(matrix, downRight);
                else if (downFree)      cell.SwapPositions(matrix, down);
                break;
            case 2:
                if (downRightFree)      cell.SwapPositions(matrix, downRight);
                else if (downFree)      cell.SwapPositions(matrix, down);
                else if (downLeftFree)  cell.SwapPositions(matrix, downLeft);
                break;
        }
        return downFree || downLeftFree || downRightFree;
    }

    public static bool MoveSide(SandMatrix matrix, Cell cell, int dispersion)
    {
        int left = CheckSide(matrix, cell, dispersion, -1, out Cell leftTarget);
        int right = CheckSide(matrix, cell, dispersion, 1, out Cell rightTarget);

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

        if (leftFree)       cell.SwapPositions(matrix, leftTarget);
        else if (rightFree) cell.SwapPositions(matrix, rightTarget);

        return leftFree || rightFree;
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

}