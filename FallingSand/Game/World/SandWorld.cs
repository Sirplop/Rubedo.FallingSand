#define USE_MULTITHREADING

using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Rubedo;
using Rubedo.Components;
using Rubedo.Graphics;
using Rubedo.Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am SandWorld, and I don't have a summary yet.
/// </summary>
public class SandWorld : RenderableComponent
{
    private float accumulatedDelta = 0;
    public const float SAND_UPDATE_TIME = 1f / 50;
    public bool doTick = true;
    public bool stepTick = true;
    public override RectF Bounds => WorldRect;

    public int gravity = 1;
    private int worldMinX;
    private int worldMinY;
    private int worldMaxX;
    private int worldMaxY;
    public Rectangle WorldRect => new Rectangle(worldMinX, worldMinY, worldMaxX - worldMinX, worldMaxY - worldMinY);

    public int chunkSize;
    public int chunksPerRegion;
    public int regionSize;

    public readonly Dictionary<int, Dictionary<int, WorldRegion>> regionLookup;
    public readonly List<WorldRegion> regions;

    public SandWorld(int chunkSize, int chunksPerRegion)
    {
        AlwaysDraw = true;
        LayerDepth = 0;

        this.chunksPerRegion = chunksPerRegion;
        this.chunkSize = chunkSize;
        this.regionSize = chunkSize * chunksPerRegion;
        this.worldMinX = int.MaxValue;
        this.worldMinY = int.MaxValue;
        this.worldMaxX = int.MinValue;
        this.worldMaxY = int.MinValue;
        regionLookup = new Dictionary<int, Dictionary<int, WorldRegion>>();
        regions = new List<WorldRegion>();

        AddRegion(0, 0);
        //AddRegion(0, -1);
        //AddRegion(-1, -1);
        //AddRegion(-1, 0);
    }

    public void AddRegion(int x, int y)
    {
        WorldRegion region = new WorldRegion(this, chunkSize, chunksPerRegion, x, y);
        regions.Add(region);
        if (!regionLookup.TryGetValue(y, out Dictionary<int, WorldRegion> xDict))
        {
            xDict = new Dictionary<int, WorldRegion>();
            regionLookup.Add(y, xDict);
        }
        xDict.Add(x, region);

        int sx = (x >> 31);
        int sy = (y >> 31);

        if (x * regionSize < worldMinX)
            worldMinX = x * regionSize;
        if ((x + 1) * regionSize > worldMaxX)
            worldMaxX = ((x + 1) * regionSize) - 1;
        if (y * regionSize < worldMinY)
            worldMinY = y * regionSize;
        if ((y + 1) * regionSize > worldMaxY)
            worldMaxY = ((y + 1) * regionSize) - 1;
    }

    public override void FixedUpdate()
    {
        accumulatedDelta += Time.FixedDeltaTime;

        // Avoid accumulator death spiral
        if (accumulatedDelta > SAND_UPDATE_TIME * 5)
            accumulatedDelta = SAND_UPDATE_TIME * 5;

        while (accumulatedDelta > SAND_UPDATE_TIME)
        {
            if (doTick)
            {
                MultiStep();
            }
            else if (stepTick)
            {
                stepTick = false;
                MultiStep();
            }
            accumulatedDelta -= SAND_UPDATE_TIME;
        }
    }

    private List<List<WorldChunk>> _updateGrid = new List<List<WorldChunk>>();
    private List<WorldChunk> _phaseGrid;
    bool flip = false;
    public void MultiStep()
    {
        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].active)
                regions[i].MultithreadSetup(this);
        }

        const int GRIDSIZE = 2;

        _updateGrid.Clear();
        for (int i = 0; i < GRIDSIZE * GRIDSIZE; i++)
        {
            _updateGrid.Add(new List<WorldChunk>());
        }
        for (int i = 0; i < regions.Count; i++)
        {
            WorldRegion region = regions[i];
            if (!region.active)
                continue;

            for (int j = 0; j < chunksPerRegion * chunksPerRegion; j++)
            {
                WorldChunk chunk = region.chunks[j];
                if (chunk.DirtyRect.Width == 0 && chunk.DirtyRect.Height == 0)
                    continue; //nothing to do.
                int x = System.Math.Abs((chunk.chunkX / chunk.size) % GRIDSIZE);
                int y = System.Math.Abs((chunk.chunkY / chunk.size) % GRIDSIZE);
                int index = x + (y * GRIDSIZE);
                _updateGrid[index].Add(chunk);
            }
        }

        /*if (flip)
        {
            (_updateGrid[0], _updateGrid[2]) = (_updateGrid[2], _updateGrid[0]);
            (_updateGrid[1], _updateGrid[3]) = (_updateGrid[3], _updateGrid[1]);
        }
        flip = !flip;*/

        for (int i = 0; i < _updateGrid.Count; i++)
        {
            _phaseGrid = _updateGrid[i];
            if (_phaseGrid.Count == 0)
                continue;
#if USE_MULTITHREADING
            Parallel.For(0, _phaseGrid.Count, RunChunkUpdate);
#else
            for (int j = 0; j < _phaseGrid.Count; j++)
                RunChunkUpdate(j);
#endif
        }

        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].active)
                regions[i].MultithreadFinish(this);
        }
    }

    private void RunChunkUpdate(int i)
    {
        _phaseGrid[i].MultithreadStep(this);
    }

    void UpdateWorldWithGravityWavefront()
    {
        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].active)
                regions[i].MultithreadSetup(this);
        }

        // Group active chunks by their chunk-row (based on Y)
        Dictionary<int, (List<WorldChunk>, List<WorldChunk>)> rows = new Dictionary<int, (List<WorldChunk>, List<WorldChunk>)>();
        for (int r = 0; r < regions.Count; r++)
        {
            WorldRegion region = regions[r];
            if (!region.active)
                continue;
            for (int i = 0; i < region.chunks.Length; i++)
            {
                WorldChunk chunk = region.chunks[i];
                if (chunk.DirtyRect.Width == 0 && chunk.DirtyRect.Height == 0)
                    continue;
                int row = chunk.chunkY >> chunk.sizeShift;

                if (!rows.TryGetValue(row, out var list))
                {
                    list = (new List<WorldChunk>(), new List<WorldChunk>());
                    rows.Add(row, list);
                }
                if (System.Math.Abs(chunk.chunkX >> chunk.sizeShift) % 2 == 1)
                    list.Item1.Add(chunk);
                else
                    list.Item2.Add(chunk);
            }
        }

        // Get rows sorted top -> bottom
        List<int> orderedRows = rows.Keys.OrderBy(r => r).ToList();

        // GRAVITY WAVEFRONT:
        // update chunk rows from top -> bottom,
        // chunks within a row run in parallel
        foreach (int row in orderedRows)
        {
            (List<WorldChunk> leftChunks, List<WorldChunk> rightChunks) = rows[row];
            if (leftChunks.Count == 0 && rightChunks.Count == 0)
                continue;

#if USE_MULTITHREADING
            Parallel.For(0, leftChunks.Count, i =>
            {
                leftChunks[i].MultithreadStep(this);
            });
            Parallel.For(0, rightChunks.Count, i =>
            {
                rightChunks[i].MultithreadStep(this);
            });
#else
            foreach (var chunk in rowChunks)
                chunk.MultithreadStep(this);
#endif
        }

        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].active)
                regions[i].MultithreadFinish(this);
        }
    }

    public void GetRegionLocation(int x, int y, out int regX, out int regY)
    {
        // Compute region X
        int sx = x >> 31;                   // -1 if x < 0, 0 otherwise
        regX = (x - sx) / regionSize + sx;  // branchless formula

        // Compute region Y
        int sy = y >> 31;                   // -1 if y < 0, 0 otherwise
        regY = (y - sy) / regionSize + sy;  // branchless formula
    }

    public WorldRegion GetRegion(int x, int y)
    {
        GetRegionLocation(x, y, out int regX, out int regY);
        if (regionLookup.TryGetValue(regY, out var xDict) && xDict.TryGetValue(regX, out var value))
            return value;
        return null;
    }

    public WorldChunk GetChunk(int x, int y)
    {
        WorldRegion region = GetRegion(x, y);
        if (region == null)
            return null;
        return region.GetChunk(x, y);
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
            chunk.SetCell(cell, x, y);
        return exists;
    }

    public bool InBounds(int x, int y)
    {
        return x >= worldMinX && x < worldMaxX && y >= worldMinY && y < worldMaxY;
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
        current.freeFalling = true;
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
        current.color = Color.Transparent;
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
    public override void Render(Renderer renderer, Camera camera)
    {
        for (int i = 0; i < regions.Count; i++)
        {
            WorldRegion region = regions[i];
            region.Draw(renderer, camera, _layerDepth);
        }
    }
}