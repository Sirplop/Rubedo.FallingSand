#define USE_MULTITHREADING

using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rubedo;
using Rubedo.Graphics;
using System;
using System.Collections;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am WorldRegion, and I don't have a summary yet.
/// </summary>
public class WorldRegion
{
    public bool active = true;

    private Color[] textureData;
    private readonly Texture2D texture;
    private readonly int regionSize;
    private readonly int chunkSize;
    private readonly int chunksPerRegion;
    private readonly int regionX;
    private readonly int regionY;
    private readonly int sizeShift;
    private readonly int regionMask;

    public readonly Cell[] cells;
    public readonly bool[] dirtyRectStep;
    public WorldChunk[] chunks;

    //we double up the movedWithFrame array so we can reset
    //one while we use the other for the frame. Purely for performance.
    private readonly bool[] movedWithFrame1;
    private readonly bool[] movedWithFrame2;
    private bool frameFlip = false;
    private Task movedWithFrameReset = null;

    public WorldRegion(SandWorld world, int chunkSize, int chunksPerRegion, int x, int y)
    {
        this.chunkSize = chunkSize;
        this.chunksPerRegion = chunksPerRegion;
        this.regionSize = chunkSize * chunksPerRegion;
        this.regionX = x * regionSize;
        this.regionY = y * regionSize;

        sizeShift = Rubedo.Lib.Math.GetPower2Exponent(this.chunkSize);
        regionMask = regionSize - 1;

        textureData = new Color[regionSize * regionSize];
        texture = new Texture2D(RubedoEngine.Graphics.GraphicsDevice, regionSize, regionSize);
        chunks = new WorldChunk[chunksPerRegion * chunksPerRegion];
        dirtyRectStep = new bool[regionSize * regionSize];
        movedWithFrame1 = new bool[regionSize * regionSize];
        movedWithFrame2 = new bool[regionSize * regionSize];
        cells = new Cell[regionSize * regionSize];

        int cellCount = regionSize * regionSize;
        for (int i = 0; i < cellCount; i++)
        {
            int y1 = (i / regionSize) + regionY;
            int x1 = (i % regionSize) + regionX;
            cells[i] = new Cell(x1, y1);
        }

        for (int my = 0; my < chunksPerRegion; my++)
        {
            int dY = (y * chunksPerRegion) + my;
            for (int mx = 0; mx < chunksPerRegion; mx++)
            {
                int dX = (x * chunksPerRegion) + mx;
                chunks[mx + (my * chunksPerRegion)] = new WorldChunk(world, this, dX, dY, chunkSize);
            }
        }
    }

#region Querying
    public WorldChunk GetChunk(int x, int y)
    {
        int regionMask = regionSize - 1; // must be power of 2

        int localX = x & regionMask;
        int localY = y & regionMask;

        int ax = localX >> sizeShift;
        int ay = localY >> sizeShift;

        int index = ay * chunksPerRegion + ax;
        return chunks[index];
    }

    public Cell GetCell(int x, int y)
    {
        int i = GetIndex(x, y);
        return cells[i];
    }
    public Cell GetCell(int index)
    {
        return cells[index];
    }

    public int GetIndex(int x, int y)
    {
        int localX = x & regionMask;
        int localY = y & regionMask;
        return localY * regionSize + localX;
    }

    public bool GetMovedWithFrame(int x, int y)
    {
        int i = GetIndex(x, y);
        return frameFlip ? movedWithFrame2[i] : movedWithFrame1[i];
    }
    public bool GetMovedWithFrame(int index)
    {
        return frameFlip ? movedWithFrame2[index] : movedWithFrame1[index];
    }
    #endregion
    #region Setters
    public void SetCell(Cell cell, int x, int y)
    {
        WorldChunk chunk = GetChunk(x, y);

        if (chunk != null)
            chunk.SetCell(cell, x, y);
    }
    public void SetMovedWithFrame(int x, int y)
    {
        int i = GetIndex(x, y);
        if (frameFlip)
        {
            movedWithFrame2[i] = true;
        }
        else
        {
            movedWithFrame1[i] = true;
        }
    }
    public void SetMovedWithFrame(int index)
    {
        if (frameFlip)
        {
            movedWithFrame2[index] = true;
        }
        else
        {
            movedWithFrame1[index] = true;
        }
    }
#endregion

    private void ResetMovedWithFrame()
    {
        if (frameFlip)
        {
            for (int i = 0; i < movedWithFrame1.Length; i++)
            {
                movedWithFrame1[i] = false;
            }
        }
        else
        {
            for (int i = 0; i < movedWithFrame1.Length; i++)
            {
                movedWithFrame2[i] = false;
            }
        }
    }

    public void MultithreadSetup(SandWorld world)
    {
        movedWithFrameReset?.Wait();
        frameFlip = !frameFlip;
        movedWithFrameReset = new Task(ResetMovedWithFrame);
        movedWithFrameReset.Start();

#if USE_MULTITHREADING
        Parallel.For(0, chunks.Length, (i) =>
        {
            chunks[i].MultithreadSetup(world);
        });
#else
        for (int i = 0; i < chunks.Length; i++)
            chunks[i].MultithreadSetup(world);
#endif
    }
    public void MultithreadFinish(SandWorld world)
    {
#if USE_MULTITHREADING
        Parallel.For(0, chunks.Length, (i) =>
        {
            chunks[i].MultithreadFinish(world);
        });
#else
        for (int i = 0; i < chunks.Length; i++)
            chunks[i].MultithreadFinish(world);
#endif
    }


    public void Draw(Renderer renderer, Camera camera, float layer)
    {
        bool updated = false;
#if USE_MULTITHREADING
        Parallel.For(0, chunks.Length, (i) =>
        {
            updated |= chunks[i].Draw(this, camera, ref textureData);
        });
#else
        for (int i = 0; i < chunks.Length; i++)
        {
            updated |= chunks[i].Draw(this, camera, ref textureData);
        }
#endif
        if (updated)
            texture.SetData(textureData);

        renderer.Draw(
            texture,
            new Vector2(regionX, regionY),
            null,
            Color.White,
            0,
            Vector2.Zero,
            Vector2.One,
            SpriteEffects.FlipVertically, layer);
    }
}