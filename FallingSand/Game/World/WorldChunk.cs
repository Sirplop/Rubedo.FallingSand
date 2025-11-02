using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Rubedo.Lib.Extensions;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am WorldChunk, and I don't have a summary yet.
/// </summary>
public class WorldChunk
{
    private bool[] movedWithFrame;
    private readonly Cell[] elements;
    public readonly int chunkX; //starting coordinate in world space
    public readonly int chunkY; //starting coordinate in world space

    public Rectangle dirtyRect;
    public Rectangle prevDirtyRect;

    public readonly int size;

    public int[] shuffledX; //this is the entire grid.

    public WorldChunk(int worldX, int worldY, int size)
    {
        this.size = size;
        this.chunkX = worldX * size;
        this.chunkY = worldY * size;
        elements = new Cell[size * size];
        shuffledX = new int[size * size];
        movedWithFrame = new bool[size * size];
        int len = size * size;
        for (int i = 0; i < len; i++)
        {
            int y = (i / size) + this.chunkY;
            int x = (i % size) + this.chunkX;
            elements[i] = new Cell(x, y);
            shuffledX[i] = i;
            movedWithFrame[i] = false;
        }
    }

    private void ShuffleXIndices(int startX, int endX, int startY, int endY)
    {
        /*
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                int i = GetIndex(x, y);
                shuffledX[i] = i;
            }
        }
        shuffledX.FYRectShuffle(startX % size, startY % size, endX - startX, endY - startY);
        */
        
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                int i = GetIndex(x, y);
                shuffledX[i] = i;
            }
            int v = GetIndex(startX, y);
            shuffledX.FYSubShuffle(v, endX - startX);
        }
    }

    public void Step(SandMatrix matrix)
    {
        Rectangle.Union(ref dirtyRect, ref prevDirtyRect, out Rectangle rect);

        prevDirtyRect = dirtyRect;
        dirtyRect.X = chunkX;
        dirtyRect.Y = chunkY;
        dirtyRect.Width = 0;
        dirtyRect.Height = 0;

        int dirtyX = Rubedo.Lib.Math.Clamp(rect.X, chunkX, chunkX + size);
        int finX = Rubedo.Lib.Math.Clamp(rect.Right, chunkX, chunkX + size);
        int dirtyY = Rubedo.Lib.Math.Clamp(rect.Y, chunkY, chunkY + size);
        int finY = Rubedo.Lib.Math.Clamp(rect.Bottom, chunkY, chunkY + size);
        if (dirtyY == finY && dirtyX == finX)
            return; //nothing to do.
        
        ShuffleXIndices(dirtyX, finX, dirtyY, finY);

        for (int y = dirtyY; y < finY; y++)
        {
            for (int x = dirtyX; x < finX; x++)
            {
                int i = shuffledX[GetIndex(x, y)];
                if (!movedWithFrame[i] && !elements[i].IsEmpty)
                {
                    elements[i].element.Step(matrix, elements[i]);
                }
            }
        }
    }
    public void Envelop(int x, int y)
    {
        const int PADDING = 3;

        if (dirtyRect.Width == 0 && dirtyRect.Height == 0)
        {
            dirtyRect.X = Rubedo.Lib.Math.Clamp(x - PADDING, chunkX, chunkX + size);
            dirtyRect.Y = Rubedo.Lib.Math.Clamp(y - PADDING, chunkY, chunkY + size);
            dirtyRect.Width = PADDING * 2 + (PADDING % 2);
            dirtyRect.Height = PADDING * 2 + (PADDING % 2);
        }
        else
        {
            dirtyRect.Union(x - PADDING, y - PADDING);
            dirtyRect.Union(x + PADDING, y + PADDING);
        }
    }

    public void ResetMovedWithFrame()
    {
        for (int i = 0; i < movedWithFrame.Length; i++)
            movedWithFrame[i] = false;
    }

    public bool MovedWithFrame(int x, int y)
    {
        return movedWithFrame[GetIndex(x, y)];
    }

    public Cell GetCell(int x, int y)
    {
        return elements[GetIndex(x, y)];
    }
    public Cell GetCell(int index)
    {
        return elements[index];
    }
    public int GetIndex(int x, int y)
    {
        return (x - this.chunkX) + (y - chunkY) * size;
    }
    public bool InBounds(int x, int y)
    {
        return x >= this.chunkX && x < this.chunkX + size
            && y >= chunkY && y < chunkY + size;
    }

    public bool IsEmpty(int x, int y)
    {
        return IsEmpty(GetIndex(x, y));
    }
    public bool IsEmpty(int index)
    {
        return GetCell(index).element == null;
    }

    public void SetCell(SandMatrix matrix, Cell cell, int x, int y)
    {
        SetCell(cell, GetIndex(x, y));
        cell.x = x;
        cell.y = y;

        if (!InBounds(x + 2, y + 2))
        {
            matrix.GetChunk(x + 2, y + 2)?.Envelop(x + 2, y + 2);
        }
        if (!InBounds(x - 2, y + 2))
        {
            matrix.GetChunk(x - 2, y + 2)?.Envelop(x - 2, y + 2);
        }
        if (!InBounds(x - 2, y - 2))
        {
            matrix.GetChunk(x - 2, y - 2)?.Envelop(x - 2, y - 2);
        }
        if (!InBounds(x + 2, y - 2))
        {
            matrix.GetChunk(x + 2, y - 2)?.Envelop(x + 2, y - 2);
        }

        Envelop(x, y);

    }
    public void SetCell(Cell cell, int index)
    {
        elements[index] = cell;
        movedWithFrame[index] = true;
    }
}