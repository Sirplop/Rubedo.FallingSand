using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using Rubedo;
using System;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Cell, and I don't have a summary yet.
/// </summary>
public class Cell
{
    public const byte FREE_FALLING_THRESHOLD = 3; //number of frames the pixel must not move to reset free falling.

    public Element element;
    public int x;
    public int y;
    public Color color;

    public int xVel = 0;
    public float yVel = 0;
    public bool freeFalling = false;
    public byte freeWiggle = 1;
    public byte freeFallingCount = 0;

    public bool IsEmpty => element == null;

    public Cell(int x, int y)
    {
        this.x = x;
        this.y = y;
        color = Color.White;
    }

    public void SetFreeFalling(bool value)
    {
        this.freeFalling = value;
        freeFallingCount = 0;
    }

    public void SwapPositions(SandMatrix matrix, Cell toSwap)
    {
        SwapPositions(matrix, toSwap, toSwap.x, toSwap.y);
    }
    public void SwapPositions(SandMatrix matrix, int x, int y)
    {
        if (this.x == x && this.y == y)
            return;

        Cell cell = matrix.GetCell(x, y);
        matrix.SetCell(this.x, this.y, cell);
        matrix.SetCell(x, y, this);

        this.freeFalling = true;
        matrix.SetFreeFalling(x + 1, y);
        matrix.SetFreeFalling(x - 1, y);
    }

    private void SwapPositions(SandMatrix matrix, Cell toSwap, int toSwapX, int toSwapY)
    {
        if (this.x == toSwapX && this.y == toSwapY)
            return;

        if (!matrix.SetCell(this.x, this.y, toSwap))
            throw new System.IndexOutOfRangeException();
        if (!matrix.SetCell(toSwapX, toSwapY, this))
            throw new System.IndexOutOfRangeException();

        this.freeFalling = true;
        matrix.SetFreeFalling(toSwapX + 1, toSwapY);
        matrix.SetFreeFalling(toSwapX - 1, toSwapY);
    }

    public void Displace(SandMatrix matrix, Cell toSwap)
    {
        Displace(matrix, toSwap, toSwap.x, toSwap.y);
    }
    private void Displace(SandMatrix matrix, Cell target, int targetX, int targetY)
    {
        if (this.x == targetX && this.y == targetY)
        {
            return;
        }
        Displace(matrix, target, this);
        SwapPositions(matrix, target);
    }

    private static bool Displace(SandMatrix matrix, Cell cell, Cell source)
    {
        bool upFree = matrix.TryGetCell(cell.x, cell.y + 1, out Cell up) && up != source && up.IsEmpty;
        bool upLeftFree = matrix.TryGetCell(cell.x - 1, cell.y + 1, out Cell upLeft) && upLeft != source && upLeft.IsEmpty;
        bool upRightFree = matrix.TryGetCell(cell.x + 1, cell.y + 1, out Cell upRight) && upRight != source && upRight.IsEmpty;

        int flip = Rubedo.Lib.Random.Range(0, 3);
        switch (flip)
        {
            case 0:
                if (upFree) cell.SwapPositions(matrix, up);
                else if (upLeftFree) cell.SwapPositions(matrix, upLeft);
                else if (upRightFree) cell.SwapPositions(matrix, upRight);
                break;
            case 1:
                if (upLeftFree) cell.SwapPositions(matrix, upLeft);
                else if (upRightFree) cell.SwapPositions(matrix, upRight);
                else if (upFree) cell.SwapPositions(matrix, up);
                break;
            case 2:
                if (upRightFree) cell.SwapPositions(matrix, upRight);
                else if (upFree) cell.SwapPositions(matrix, up);
                else if (upLeftFree) cell.SwapPositions(matrix, upLeft);
                break;
        }
        return upFree || upLeftFree || upRightFree;
    }

}