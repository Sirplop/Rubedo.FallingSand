#define USE_MULTITHREADING

using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rubedo;
using Rubedo.Components;
using Rubedo.Graphics;
using Rubedo.Lib;
using Rubedo.Lib.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am SandWorld, and I don't have a summary yet.
/// </summary>
public class SandWorld : RenderableComponent
{
    public const int GRAVITY_FRAME = 10;
    public bool doTick = true;
    public bool stepTick = true;
    public bool uncapUpdates = false;

    public override RectF Bounds => WorldRect;

    public int gravity = 3;
    private int worldMinX;
    private int worldMinY;
    private int worldMaxX;
    private int worldMaxY;
    public Rectangle WorldRect => new Rectangle(worldMinX, worldMinY, worldMaxX - worldMinX, worldMaxY - worldMinY);

    protected override Texture2D MaterialTexture => null; //this does not render things itself.

    public int chunkSize;
    public int chunksPerRegion;
    public int regionSize;
    public int gravityFrame = 0;

    public bool drawMoveOverride = false;

    public readonly Dictionary<int, Dictionary<int, WorldRegion>> regionLookup;
    public readonly List<WorldRegion> regions;
    private Squirrel3 rnd = new Squirrel3();

    public readonly bool headless;
    public int worldTick = 0;

    public SandWorld(int chunkSize, int chunksPerRegion, bool headless = false)
    {
        this.headless = headless;
        AlwaysDraw = true;
        LayerDepth = 0;

        if (!Rubedo.Lib.Math.IsPowerOf2(chunkSize) || !Rubedo.Lib.Math.IsPowerOf2(chunkSize))
        {
            throw new ArgumentException("chunkSize and chunksPerRegion must be powers of 2!");
        }

        this.chunksPerRegion = chunksPerRegion;
        this.chunkSize = chunkSize;
        this.regionSize = chunkSize * chunksPerRegion;
        this.worldMinX = int.MaxValue;
        this.worldMinY = int.MaxValue;
        this.worldMaxX = int.MinValue;
        this.worldMaxY = int.MinValue;
        regionLookup = new Dictionary<int, Dictionary<int, WorldRegion>>();
        regions = new List<WorldRegion>();

        const int REGION_X = 4;
        const int REGION_Y = 4;

        for (int x = 0; x < REGION_X; x++)
        {
            for (int y = 0; y < REGION_Y; y++)
            {
                int x2 = x - (REGION_X / 2);
                int y2 = y - (REGION_Y / 2);
                AddRegion(x2, y2);
            }
        }
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
        if (uncapUpdates)
            return;
        if (doTick)
        {
            MultiStep();
        }
        else if (stepTick)
        {
            stepTick = false;
            MultiStep();
        }
    }

    public override void Update()
    {
        if (uncapUpdates)
            MultiStep();
    }

    private List<List<WorldChunk>> _updateGrid = new List<List<WorldChunk>>();
    private List<WorldChunk> _phaseGrid;
    private int _currentStep = 1;
    bool flip = false;
    public void MultiStep()
    {
        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].active)
                regions[i].MultithreadSetup(this);
        }

        const int GRIDSIZE = 2;
        const int PASSES = 3;

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
                chunk.worldTick = this.worldTick;
                if (chunk.DirtyRect.Width == 0 && chunk.DirtyRect.Height == 0)
                    continue; //nothing to do.
                int x = System.Math.Abs((chunk.chunkX / chunk.size) % GRIDSIZE);
                int y = System.Math.Abs((chunk.chunkY / chunk.size) % GRIDSIZE);
                int index = x + (y * GRIDSIZE);
                _updateGrid[index].Add(chunk);
            }
        }

        //_updateGrid.FYShuffle(ref rnd);
        if (flip)
        {
            _updateGrid.Reverse();
        }
        flip = !flip;

        for (int z = 1; z <= PASSES; z++)
        {
            _currentStep = z;
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
        }

        for (int i = 0; i < regions.Count; i++)
        {
            if (regions[i].active)
                regions[i].MultithreadFinish(this);
        }

        this.worldTick++;
    }

    private void RunChunkUpdate(int i)
    {
        _phaseGrid[i].MultithreadStep(this, in _currentStep);
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

    public bool InBounds(int x, int y)
    {
        return x >= worldMinX && x <= worldMaxX && y >= worldMinY && y <= worldMaxY;
    }

    public bool SpawnCell(int element, int x, int y)
    {
        if (!InBounds(x, y))
        {
            return false;
        }
        WorldChunk chunk = GetChunk(x, y);
        if (chunk == null)
            return false;

        int cellID = chunk.GetCellIndex(in x, in y);
        if (chunk.element[cellID] != 0)
            return false;

        chunk.element[cellID] = element;
        ref WorldChunk.Moving moving = ref chunk.moving[cellID];
        moving.IsMoving = true;
        moving.MovingCount = 0; //naughty naughty, mutating a struct...

        chunk.velocity[cellID].Zero();
        chunk.color[cellID] = ElementManager.color[element] * rnd.Range(0.9f, 1.1f);
        chunk.hp[cellID].Value = ElementManager.hp[element];

        if (ElementManager.typeLookup[element] == ElementManager.Type.FIRE)
        {
            chunk.burnFireType[cellID] = element;
        }
        else
        {
            chunk.burnFireType[cellID] = ElementManager.EMPTY;
        }

        chunk.ThreadEnvelop(x, y);
        chunk.RenderRect.Union(x, y);
        return true;
    }

    public bool SpawnCell(in int element, in WorldChunk chunk, in int cellID)
    {
        if (chunk.element[cellID] != ElementManager.EMPTY)
            return false;

        chunk.element[cellID] = element;
        ref WorldChunk.Moving moving = ref chunk.moving[cellID];
        moving.IsMoving = true;
        moving.MovingCount = 0; //naughty naughty, mutating a struct...

        chunk.velocity[cellID].Zero();
        chunk.color[cellID] = ElementManager.color[element] * rnd.Range(0.9f, 1.1f);
        chunk.hp[cellID].Value = ElementManager.hp[element];

        if (ElementManager.typeLookup[element] == ElementManager.Type.FIRE)
        {
            chunk.burnFireType[cellID] = element;
        }
        else
        {
            chunk.burnFireType[cellID] = ElementManager.EMPTY;
        }

        chunk.ThreadEnvelop(cellID);
        chunk.RenderRect.Union((cellID / chunkSize) + chunk.chunkY, (cellID % chunkSize) + chunk.chunkX);
        return true;
    }

    public bool ClearCell(int x, int y)
    {
        if (!InBounds(x, y))
        {
            return false;
        }
        WorldChunk chunk = GetChunk(x, y);
        if (chunk == null)
            return false;

        int cellID = chunk.GetCellIndex(in x, in y);
        if (chunk.element[cellID] == 0)
            return false;

        chunk.element[cellID] = 0;
        ref WorldChunk.Moving moving = ref chunk.moving[cellID];
        moving.IsMoving = true;
        moving.MovingCount = 0; //naughty naughty, mutating a struct...

        chunk.velocity[cellID].Zero();
        chunk.color[cellID] = ElementManager.colorCode[ElementManager.EMPTY];
        chunk.hp[cellID].Zero();
        chunk.burnFireType[cellID] = ElementManager.EMPTY;

        chunk.ThreadEnvelop(cellID);
        chunk.RenderRect.Union(x, y);
        return true;
    }
    public bool ClearCell(WorldChunk chunk, int cellID)
    {
        chunk.element[cellID] = 0;
        ref WorldChunk.Moving moving = ref chunk.moving[cellID];
        moving.IsMoving = true;
        moving.MovingCount = 0; //naughty naughty, mutating a struct...

        chunk.velocity[cellID].Zero();
        chunk.color[cellID] = ElementManager.colorCode[ElementManager.EMPTY];
        chunk.hp[cellID].Zero();
        chunk.burnFireType[cellID] = ElementManager.EMPTY;

        chunk.ThreadEnvelop(cellID);
        chunk.RenderRect.Union((cellID / chunkSize) + chunk.chunkY, (cellID % chunkSize) + chunk.chunkX);
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