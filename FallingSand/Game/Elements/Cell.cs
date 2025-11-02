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
    public Element element;
    public int x;
    public int y;
    public Color color;

    public float yVel;
    public bool freeFalling = false;

    public bool IsEmpty => element == null;

    public Cell(int x, int y)
    {
        this.x = x;
        this.y = y;
        color = Color.White;
    }

    public void SwapPositions(SandMatrix matrix, Cell toSwap)
    {
        SwapPositions(matrix, toSwap, toSwap.x, toSwap.y);
    }

    private void SwapPositions(SandMatrix matrix, Cell toSwap, int toSwapX, int toSwapY)
    {
        if (this.x == toSwapX && this.y == toSwapY)
        {
            return;
        }
        if (!matrix.SetCell(this.x, this.y, toSwap))
            throw new System.IndexOutOfRangeException();
        if (!matrix.SetCell(toSwapX, toSwapY, this))
            throw new System.IndexOutOfRangeException();
    }
}