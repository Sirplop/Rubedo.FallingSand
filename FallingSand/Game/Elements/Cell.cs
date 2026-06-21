using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using Rubedo;
using System;
using System.Runtime.CompilerServices;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Cell, and I don't have a summary yet.
/// </summary>
public class Cell
{
    public const byte FREE_FALLING_THRESHOLD = 255; //number of frames the pixel must not move to reset free falling.

    public Element element = null;
    private bool empty;
    public int x;
    public int y;
    public Color color;

    public float xVel = 0;
    public float yVel = 0;
    public bool freeFalling = false;
    public byte freeWiggle = 1;
    public byte freeFallingCount = 0;
    public double lastFrame = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEmpty()
    {
        return empty;
    }

    public Cell(int x, int y)
    {
        this.x = x;
        this.y = y;
        color = Color.Transparent;
        empty = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFreeFalling(bool value)
    {
        this.freeFalling = value;
        freeFallingCount = 0;
    }

    public void SetElement(Element element)
    {
        this.element = element;
        if (element == null)
        {
            this.color = Color.Transparent;
        }
        else
        {
            this.color = element.color * Rubedo.Lib.Random.Range(0.9f, 1.1f);
        }
        this.empty = element == null;
    }


    public void SwapPositions(WorldChunk caller, Cell toSwap)
    {
        int toSwapX = toSwap.x;
        int toSwapY = toSwap.y;
        int cellX = this.x;
        int cellY = this.y;

        if (cellX == toSwapX && cellY == toSwapY)
            return;

        caller.SetCell(toSwap, cellX, cellY);
        caller.SetCell(this, toSwapX, toSwapY);

        this.SetFreeFalling(true);
        toSwap.SetFreeFalling(true);

        caller.SetFreeFalling(caller, toSwapX + 1, toSwapY);
        caller.SetFreeFalling(caller, toSwapX - 1, toSwapY);
        caller.SetFreeFalling(caller, cellX + 1, cellY);
        caller.SetFreeFalling(caller, cellX - 1, cellY);
    }

    public void Displace(WorldChunk caller, Cell target)
    {
        int targetX = target.x;
        int targetY = target.y;
        int cellX = this.x;
        int cellY = this.y;

        if (cellX == targetX && cellY == targetY)
        {
            return;
        }
        Cell displaced = Displace(caller, target, this);
        if (displaced != null)
        {
            SwapPositions(caller, displaced);
        }
        else
        {
            SwapPositions(caller, target); //target didn't move, swap spots directly.
        }
    }

    private static Cell Displace(WorldChunk caller, Cell cell, Cell source)
    {
        int cellX = cell.x;
        int cellY = cell.y;

        bool upFree = caller.TryGetCell(cellX, cellY + 1, out Cell up) && up != source && up.IsEmpty();
        bool upLeftFree = caller.TryGetCell(cellX - 1, cellY + 1, out Cell upLeft) && upLeft != source && upLeft.IsEmpty();
        bool upRightFree = caller.TryGetCell(cellX + 1, cellY + 1, out Cell upRight) && upRight != source && upRight.IsEmpty();

        int flip = Rubedo.Lib.Random.Range(0, 3);
        Cell displaced = null;
        switch (flip)
        {
            case 0:
                if (upFree) { cell.SwapPositions(caller, up); displaced = up; }
                else if (upLeftFree) { cell.SwapPositions(caller, upLeft); displaced = upLeft; }
                else if (upRightFree) { cell.SwapPositions(caller, upRight); displaced = upRight; }
                break;
            case 1:
                if (upLeftFree) { cell.SwapPositions(caller, upLeft); displaced = upLeft; }
                else if (upRightFree) { cell.SwapPositions(caller, upRight); displaced = upRight; }
                else if (upFree) { cell.SwapPositions(caller, up); displaced = up; }
                break;
            case 2:
                if (upRightFree) { cell.SwapPositions(caller, upRight); displaced = upRight; }
                else if (upFree) { cell.SwapPositions(caller, up); displaced = up; }
                else if (upLeftFree) { cell.SwapPositions(caller, upLeft); displaced = upLeft; }
                break;
        }
        return displaced;
    }
}