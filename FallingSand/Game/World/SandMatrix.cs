using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rubedo;
using Rubedo.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am CellularMatrix, and I don't have a summary yet.
/// </summary>
public class SandMatrix
{

    public Point worldMin;
    public Point worldMax;

    public int chunkSize;

    public readonly Dictionary<long, WorldChunk> chunkLookup;
    public readonly List<WorldChunk> chunks;

    public SandMatrix(Point worldMin, Point worldMax, int chunkSize)
    {
        this.chunkSize = chunkSize;
        this.worldMin = worldMin;
        this.worldMax = worldMax;
        chunkLookup = new Dictionary<long, WorldChunk>();
        chunks = new List<WorldChunk>();

        //TEMP LOGIC
        for (int y = worldMin.Y / chunkSize; y < worldMax.Y / chunkSize; y++)
        {
            for (int x = worldMin.X / chunkSize; x < worldMax.X / chunkSize; x++)
            {
                WorldChunk chunk = new WorldChunk(x, y, chunkSize);
                chunks.Add(chunk);
                chunkLookup.Add(ChunkHash(x, y), chunk);
            }
        }
    }

    private static long ChunkHash(int x, int y)
    {
        return unchecked((long)x << 32 | (uint)y);
    }


    bool flip = false;
    public void StepAll()
    {
        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].ResetMovedWithFrame();
        }
        //interleave the chunk update order so we don't end up with any left-right bias.
        for (int i = flip ? 1 : 0; i < chunks.Count; i += 2)
        {
            chunks[i].Step(this);
        }
        for (int i = flip ? 0 : 1; i < chunks.Count; i += 2)
        {
            chunks[i].Step(this);
        }
        flip = !flip;
    }

    public void Draw(ref Color[] data, Point cameraPosition, Texture2D texture)
    {
        int xStart = worldMin.X;
        int xEnd = worldMax.X;
        int y = worldMin.Y;
        int yEnd = worldMax.Y;

        int i = 0;
        for (; y < yEnd; y++)
        {
            for (int x = xStart; x < xEnd; x++)
            {
                data[i++] = GetCell(x, y).color;
            }
        }

        texture.SetData(data);
    }


    public long GetChunkLocation(int x, int y)
    {
        int regX = (x / chunkSize) + (x >> 31); //should get the sign bit, which is either 0 or -1
        int regY = (y / chunkSize) + (y >> 31);
        return ChunkHash(regX, regY);
    }
    public void GetChunkLocation(int x, int y, out int regX, out int regY)
    {
        regX = (x / chunkSize) + (x >> 31); //should get the sign bit, which is either 0 or -1
        regY = (y / chunkSize) + (y >> 31);
    }

    public WorldChunk GetChunk(int x, int y)
    {
        long chunk = GetChunkLocation(x, y);
        if (chunkLookup.TryGetValue(chunk, out WorldChunk value))
            return value;
        return null;
    }
    public bool TryGetCell(int x, int y, out Cell target)
    {
        target = null;
        if (InBounds(x, y))
            target = GetChunk(x, y)?.GetCell(x, y);
        return target != null;
    }

    public Cell GetCell(int x, int y)
    {
        if (InBounds(x, y))
            return GetChunk(x, y)?.GetCell(x, y);
        return null;
    }

    public bool SetCell(int x, int y, Cell cell)
    {
        WorldChunk chunk = GetChunk(x, y);
        bool exists = chunk != null;
        if (exists)
            chunk.SetCell(this, cell, x, y);
        return exists;
    }

    public bool InBounds(int x, int y)
    {
        return x >= worldMin.X && x < worldMax.X && y >= worldMin.Y && y < worldMax.Y;
    }

    public bool SpawnCell(Element element, int x, int y)
    {
        if (!InBounds(x, y))
        {
            return false;
        }

        Cell current = GetCell(x, y);
        if (!current.IsEmpty)
            return false;

        current.element = element;
        current.color = element.color * Rubedo.Lib.Random.Range(0.9f, 1.1f);
        SetCell(x, y, current);
        return true;
    }

    public bool ClearCell(int x, int y)
    {
        if (!InBounds(x, y))
        {
            return false;
        }
        Cell current = GetCell(x, y);
        if (current.IsEmpty)
            return false;
        current.element = null;
        current.color = Color.White;
        SetCell(x, y, current);
        return true;
    }


    public void IterateAndApplyBetweenPoints(Point pos1, Point pos2, Action<int, int> func)
    {
        // If the two points are the same no need to iterate. Just run the provided function
        if (pos1 == pos2)
        {
            func?.Invoke(pos1.X, pos1.Y);
            return;
        }

        int matrixX1 = pos1.X;
        int matrixY1 = pos1.Y;
        int matrixX2 = pos2.X;
        int matrixY2 = pos2.Y;

        int xDiff = matrixX1 - matrixX2;
        int yDiff = matrixY1 - matrixY2;
        bool xDiffIsLarger = System.Math.Abs(xDiff) > System.Math.Abs(yDiff);

        int xModifier = xDiff < 0 ? 1 : -1;
        int yModifier = yDiff < 0 ? 1 : -1;

        int longerSideLength = System.Math.Max(System.Math.Abs(xDiff), System.Math.Abs(yDiff));
        int shorterSideLength = System.Math.Min(System.Math.Abs(xDiff), System.Math.Abs(yDiff));
        float slope = (shorterSideLength == 0 || longerSideLength == 0) ? 0 : ((float)(shorterSideLength) / (longerSideLength));

        int shorterSideIncrease;
        for (int i = 1; i <= longerSideLength; i++)
        {
            shorterSideIncrease = Rubedo.Lib.Math.RoundToInt(i * slope);
            int yIncrease, xIncrease;
            if (xDiffIsLarger)
            {
                xIncrease = i;
                yIncrease = shorterSideIncrease;
            }
            else
            {
                yIncrease = i;
                xIncrease = shorterSideIncrease;
            }
            int currentY = matrixY1 + (yIncrease * yModifier);
            int currentX = matrixX1 + (xIncrease * xModifier);
            if (InBounds(currentX, currentY))
            {
                func.Invoke(currentX, currentY);
            }
        }
    }
}