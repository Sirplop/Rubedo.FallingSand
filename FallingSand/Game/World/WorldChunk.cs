#define USE_SHUFFLE_X

using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Rubedo.Graphics;
using Rubedo.Lib;
using Rubedo.Lib.Extensions;
using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am WorldChunk, and I don't have a summary yet.
/// </summary>
public class WorldChunk
{
    private Squirrel3 chunkRNG;
    public ref Squirrel3 ChunkRNG => ref chunkRNG;

    //TODO: Implement switching between two bitarrays to make resets faster.
    /*private readonly BitArray movedWithFrame2;
    private BitArray movedWithFrameActual;
    private bool mwfFlip = false;*/

    private readonly RectF cameraIntersection;

    public readonly int chunkX; //starting coordinate in world space
    public readonly int chunkY; //starting coordinate in world space

    public ref Rectangle DirtyRect => ref dirtyRect;
    public ref Rectangle RenderRect => ref renderRect;

    private Rectangle dirtyRect;
    private Rectangle prevDirtyRect;
    private Rectangle renderRect;
    public readonly SandWorld parentMatrix;
    public readonly WorldRegion region;

    public readonly int size;
    public readonly int sizeShift;
    public readonly int halfSize;
    public readonly int halfSizeShift;

    private int[] shuffledX; //this is the entire chunk grid.
    private WorldChunk[] multithreadChunkRef;

    public WorldChunk(SandWorld parent, WorldRegion region, int worldX, int worldY, int size)
    {
        chunkRNG = new Squirrel3(unchecked((long)worldX << 32 | (uint)worldY));

        this.region = region;
        this.parentMatrix = parent;
        this.size = size;
        this.sizeShift = Rubedo.Lib.Math.GetPower2Exponent(size);
        this.halfSize = size / 2;
        this.halfSizeShift = sizeShift - 1;
        this.chunkX = worldX * size;
        this.chunkY = worldY * size;
        cameraIntersection = new RectF(chunkX - 4, chunkY - 4, size + 8, size + 8);
        shuffledX = new int[size * size];

        renderRect = new Rectangle(chunkX, chunkY, size, size);

        for (int i = 0; i < shuffledX.Length; i++)
        {
            shuffledX[i] = i;
        }

        multithreadChunkRef = new WorldChunk[9];
        multithreadChunkRef[4] = this;
    }
    private void ShuffleXIndices(int startX, int endX, int startY, int endY)
    {
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                int i = GetIndex(x, y);
                shuffledX[GetLocalIndex(x, y)] = i;
            }
            int v = GetLocalIndex(startX, y);
            shuffledX.FYSubShuffle(v, endX - startX, ref ChunkRNG);
        }
    }
    #region Multithreaded
#if !USE_SHUFFLE_X
    bool flip = true;
#endif
    public void MultithreadSetup(SandWorld matrix)
    {

#if !USE_SHUFFLE_X
        flip = !flip;
#else
        int dirtyX = Rubedo.Lib.Math.Clamp(dirtyRect.X, chunkX, chunkX + size);
        int finX = Rubedo.Lib.Math.Clamp(dirtyRect.Right, chunkX, chunkX + size);
        int dirtyY = Rubedo.Lib.Math.Clamp(dirtyRect.Y, chunkY, chunkY + size);
        int finY = Rubedo.Lib.Math.Clamp(dirtyRect.Bottom, chunkY, chunkY + size);
        
        ShuffleXIndices(dirtyX, finX, dirtyY, finY);
#endif
        for (int y = -1; y <= 1; y++)
        {
            int valY = chunkY + (y * size);
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0)
                    continue; //skip ourself
                int valX = chunkX + (x * size);
                int i = ((y + 1) * 3) + (x + 1);

                if (multithreadChunkRef[i] == null && matrix.InBounds(valX, valY))
                { //doesn't have a reference to it, but it should exist. Get it!
                    multithreadChunkRef[i] = matrix.GetChunk(valX, valY);
                } 
                else if (multithreadChunkRef[i] != null && !matrix.InBounds(valX, valY))
                { //chunk is gone, but we still have a reference. Remove it!
                    multithreadChunkRef[i] = null;
                }
            }
        }
    }
    public void MultithreadStep(SandWorld matrix, int step)
    {
        int dirtyX = Rubedo.Lib.Math.Clamp(dirtyRect.X, chunkX, chunkX + size);
        int finX = Rubedo.Lib.Math.Clamp(dirtyRect.Right, chunkX, chunkX + size);
        int dirtyY = Rubedo.Lib.Math.Clamp(dirtyRect.Y, chunkY, chunkY + size);
        int finY = Rubedo.Lib.Math.Clamp(dirtyRect.Bottom, chunkY, chunkY + size);

#if USE_SHUFFLE_X

        for (int y = dirtyY; y < finY; y++)
        {
            for (int x = dirtyX; x < finX; x++)
            {
                int i = shuffledX[GetLocalIndex(x, y)];
                if (!region.GetMovedWithFrame(i))
                {
                    Cell cell = region.GetCell(i);
                    Element element = cell.element;
                    if (!cell.IsEmpty())
                    {
                        element.Step(this, cell);
                    }
                }
            }
        }
#else
        if (flip)
        {
            for (int y = dirtyY; y < finY; y++)
            {
                for (int x = finX - 1; x >= dirtyX; x--)
                {
                    int i = GetIndex(x, y);
                    if (!region.GetMovedWithFrame(i))
                    {
                        Cell cell = region.GetCell(i);
                        Element element = cell.element;
                        if (!cell.IsEmpty())
                        {
                            element.Step(this, cell);
                        }
                    }
                }
            }
        }
        else
        {
            for (int y = finY - 1; y >= dirtyY; y--)
            {
                for (int x = dirtyX; x < finX; x++)
                {
                    int i = GetIndex(x, y);
                    if (!region.GetMovedWithFrame(i))
                    {
                        Cell cell = region.GetCell(i);
                        Element element = cell.element;
                        if (!cell.IsEmpty())
                        {
                            element.Step(this, cell);
                        }
                    }
                }
            }
        }

#endif
    }

    public void MultithreadFinish(SandWorld matrix)
    {
        //we need to do rect stuff after updates so that changes are accurately drawn this frame.

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int y = chunkY; y < chunkY + size; y++)
        {
            for (int x = chunkX; x < chunkX + size; x++)
            {
                int i = GetIndex(x, y);
                if (region.dirtyRectStep[i])
                {
                    minX = minX == int.MaxValue ? x : System.Math.Min(minX, x);
                    minY = minY == int.MaxValue ? y : System.Math.Min(minY, y);
                    maxX = maxX == int.MinValue ? x : System.Math.Max(maxX, x);
                    maxY = maxY == int.MinValue ? y : System.Math.Max(maxY, y);
                }
                region.dirtyRectStep[i] = false;
            }
        }

        if (minX != int.MaxValue)
        { //it updated

            const int PADDING = 3;

            minX = minX - PADDING < chunkX ? chunkX : minX - PADDING;
            minY = minY - PADDING < chunkY ? chunkY : minY - PADDING;
            maxX = maxX + PADDING > chunkX + size ? chunkX + size : maxX + PADDING;
            maxY = maxY + PADDING > chunkY + size ? chunkY + size : maxY + PADDING;

            dirtyRect.X = minX;
            dirtyRect.Y = minY;
            dirtyRect.Width = maxX - minX;
            dirtyRect.Height = maxY - minY;
        } else
        {
            dirtyRect.X = 0;
            dirtyRect.Y = 0;
            dirtyRect.Width = 0;
            dirtyRect.Height = 0;
        }

        Rectangle rect = dirtyRect;

        if (dirtyRect.Width == 0)
        {
            rect = prevDirtyRect;
        }
        else if (prevDirtyRect.Width != 0)
            Rectangle.Union(ref dirtyRect, ref prevDirtyRect, out rect);

        if (renderRect.Width == 0 || renderRect.Height == 0)
            renderRect = rect;
        else if (rect.Width != 0)
        {
            Rectangle.Union(ref renderRect, ref rect, out Rectangle temp);
            renderRect = temp;
        }

        prevDirtyRect = dirtyRect;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetLocalIndex(int x, int y)
    {
        return (x - this.chunkX) + (y - chunkY) * size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ThreadEnvelop(int x, int y)
    {
        int i = GetIndex(x, y);
        region.dirtyRectStep[i] = true;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ThreadEnvelop(int index)
    {
        region.dirtyRectStep[index] = true;
    }

    public void SetCell(Cell cell, int x, int y)
    {
        const int PADDING = 1;

        WorldChunk chunk;
        if (InBounds(x, y))
        {
            chunk = this;
        }
        else
        {
            chunk = GetMultiChunk(x, y);
        }

        cell.x = x;
        cell.y = y;

        int index = chunk.GetIndex(x, y);
        chunk.SetCell(cell, index);
        chunk.ThreadEnvelop(index);

        if (x - PADDING < chunk.chunkX)
        {
            if (y - PADDING < chunk.chunkY)
                chunk.multithreadChunkRef[0]?.ThreadEnvelop(x - PADDING, y - PADDING);
            else if (y +  PADDING >= chunk.chunkY + chunk.size)
                chunk.multithreadChunkRef[6]?.ThreadEnvelop(x - PADDING, y + PADDING);
            else
                chunk.multithreadChunkRef[3]?.ThreadEnvelop(x - PADDING, y);
        }
        else if (x + PADDING >= chunk.chunkX + chunk.size)
        {
            if (y - PADDING < chunk.chunkY)
                chunk.multithreadChunkRef[2]?.ThreadEnvelop(x + PADDING, y - PADDING);
            else if (y + PADDING >= chunk.chunkY + chunk.size)
                chunk.multithreadChunkRef[8]?.ThreadEnvelop(x + PADDING, y + PADDING);
            else
                chunk.multithreadChunkRef[5]?.ThreadEnvelop(x + PADDING, y);
        }
        else
        {
            if (y - PADDING < chunk.chunkY)
                chunk.multithreadChunkRef[1]?.ThreadEnvelop(x, y - PADDING);
            else if (y + PADDING >= chunk.chunkY + chunk.size)
                chunk.multithreadChunkRef[7]?.ThreadEnvelop(x, y + PADDING);
        }
    }

    public bool TryGetCell(int x, int y, out Cell cell)
    {
        cell = null;

        if (InBounds(x, y))
        {
            cell = GetCell(x, y);
        }
        else if (InMultiBounds(x, y))
        {
            WorldChunk chunk = GetMultiChunk(x, y);
            if (chunk != null)
                cell = chunk.GetCell(x, y);
        }
        return cell != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WorldChunk GetMultiChunk(int x, int y)
    {
        int xSec = ((x - chunkX + size) >> halfSizeShift) - 1; //range 0-3
        xSec = (xSec + 1) >> 1; //mapping 0 -> 0, 1,2 -> 1, 3 -> 2
        int ySec = ((y - chunkY + size) >> halfSizeShift) - 1;
        ySec = (ySec + 1) >> 1;

        return multithreadChunkRef[ySec * 3 + xSec];
    }
    /// <summary>
    /// Returns true if the requested coordinate is within the half-size ring area around the chunk for multithreading.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OnlyMultiBounds(int x, int y)
    {   
        // Precompute boundaries
        int left = chunkX;
        int right = chunkX + size;
        int top = chunkY;
        int bottom = chunkY + size;

        int leftExt = left - halfSize;
        int rightExt = right + halfSize;
        int topExt = top - halfSize;
        int bottomExt = bottom + halfSize;

        // In multibounds but not in inner bounds
        return
            x >= leftExt && x < rightExt &&
            y >= topExt && y < bottomExt &&
            (x < left || x >= right || y < top || y >= bottom);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InMultiBounds(int x, int y)
    {
        return x >= this.chunkX - halfSize && x < this.chunkX + size + halfSize
            && y >= chunkY - halfSize && y < chunkY + size + halfSize;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(int x, int y)
    {
        return x >= this.chunkX && x < this.chunkX + size
            && y >= chunkY && y < chunkY + size;
    }

    public void SetFreeFalling(WorldChunk caller, int x, int y)
    {
        if (TryGetCell(x, y, out Cell cell))
        {
            if (!cell.IsEmpty() && cell.element.elementType == Element.Type.LIQUID)
            {
                cell.SetFreeFalling(cell.freeFalling || cell.element.liquid_inertialResistance < caller.ChunkRNG.Percent());
            }
        }
    }

#endregion
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCell(Cell cell, int index)
    {
        region.cells[index] = cell;
        region.SetMovedWithFrame(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Cell GetCell(int x, int y)
    {
        return region.GetCell(x, y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(int x, int y)
    {
        return region.GetIndex(x, y);
    }

    public bool Draw(WorldRegion region, Camera camera, ref Color[] buffer)
    {
        if (!camera.ViewRect.Intersects(in cameraIntersection))
            return false; //not on screen, don't update it.
        if (renderRect.Height != 0 && renderRect.Width != 0)
        {
            int dirtyX = Rubedo.Lib.Math.Clamp(renderRect.X, chunkX, chunkX + size);
            int finX = Rubedo.Lib.Math.Clamp(renderRect.Right, chunkX, chunkX + size);
            int dirtyY = Rubedo.Lib.Math.Clamp(renderRect.Y, chunkY, chunkY + size);
            int finY = Rubedo.Lib.Math.Clamp(renderRect.Bottom, chunkY, chunkY + size);

            for (int y = dirtyY; y < finY; y++)
            {
                for (int x = dirtyX; x < finX; x++)
                {
                    int i = GetIndex(x, y);
                    Cell cell = region.GetCell(i);

                    if (parentMatrix.drawMoveOverride)
                    {
                        if (cell.IsEmpty())
                        {
                            buffer[i] = Color.Transparent;
                        }
                        else if (!region.GetMovedWithFrame(i))
                        {
                            buffer[i] = Color.Red;
                        }
                        else
                        {
                            buffer[i] = Color.Green;
                        }
                    }
                    else
                    {
                        buffer[i] = cell.color;
                    }
                }
            }
            renderRect.Width = 0;
            renderRect.Height = 0;
            return true;
        }
        return false;
    }
}