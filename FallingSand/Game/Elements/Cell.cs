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
    public const byte FREE_FALLING_THRESHOLD = 2; //number of frames the pixel must not move to reset free falling.

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
        color = Color.Transparent;
    }

    public void SetFreeFalling(bool value)
    {
        this.freeFalling = value;
        freeFallingCount = 0;
    }

    public void SwapPositions(WorldChunk caller, Cell toSwap)
    {
        SwapPositions(caller, toSwap, toSwap.x, toSwap.y);
    }
    public void SwapPositions(WorldChunk caller, int x, int y)
    {
        if (this.x == x && this.y == y)
            return;

        if (!caller.TryGetCell(x, y, out Cell cell))
            return; //failed to swap
        caller.SetCell(cell, this.x, this.y);
        caller.SetCell(cell, x, y);

        this.freeFalling = true;
        caller.SetFreeFalling(caller, x + 1, y);
        caller.SetFreeFalling(caller, x - 1, y);
    }

    private void SwapPositions(WorldChunk caller, Cell toSwap, int toSwapX, int toSwapY)
    {
        if (this.x == toSwapX && this.y == toSwapY)
            return;

        caller.SetCell(toSwap, this.x, this.y);
        caller.SetCell(this, toSwapX, toSwapY);

        this.freeFalling = true;
        caller.SetFreeFalling(caller, toSwapX + 1, toSwapY);
        caller.SetFreeFalling(caller, toSwapX - 1, toSwapY);
    }

    public void Displace(WorldChunk caller, Cell toSwap)
    {
        Displace(caller, toSwap, toSwap.x, toSwap.y);
    }
    private void Displace(WorldChunk caller, Cell target, int targetX, int targetY)
    {
        if (this.x == targetX && this.y == targetY)
        {
            return;
        }
        Displace(caller, target, this);
        SwapPositions(caller, target);
    }

    private static bool Displace(WorldChunk caller, Cell cell, Cell source)
    {
        bool upFree = caller.TryGetCell(cell.x, cell.y + 1, out Cell up) && up != source && up.IsEmpty;
        bool upLeftFree = caller.TryGetCell(cell.x - 1, cell.y + 1, out Cell upLeft) && upLeft != source && upLeft.IsEmpty;
        bool upRightFree = caller.TryGetCell(cell.x + 1, cell.y + 1, out Cell upRight) && upRight != source && upRight.IsEmpty;

        int flip = Rubedo.Lib.Random.Range(0, 3);
        switch (flip)
        {
            case 0:
                if (upFree) cell.SwapPositions(caller, up);
                else if (upLeftFree) cell.SwapPositions(caller, upLeft);
                else if (upRightFree) cell.SwapPositions(caller, upRight);
                break;
            case 1:
                if (upLeftFree) cell.SwapPositions(caller, upLeft);
                else if (upRightFree) cell.SwapPositions(caller, upRight);
                else if (upFree) cell.SwapPositions(caller, up);
                break;
            case 2:
                if (upRightFree) cell.SwapPositions(caller, upRight);
                else if (upFree) cell.SwapPositions(caller, up);
                else if (upLeftFree) cell.SwapPositions(caller, upLeft);
                break;
        }
        return upFree || upLeftFree || upRightFree;
    }

}