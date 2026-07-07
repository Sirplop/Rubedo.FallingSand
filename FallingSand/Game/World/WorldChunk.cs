//#define USE_DOUBLE_MWF_BUFFER
#define USE_CHECKERBOARD_UPDATE
#define USE_ALTERNATING_UPDATE

using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Rubedo.Graphics;
using Rubedo.Lib;
using System.Runtime.CompilerServices;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am WorldChunk, and I don't have a summary yet.
/// </summary>
public class WorldChunk
{
    public Squirrel3 chunkRNG;

    private readonly RectF cameraIntersection;

    public readonly int chunkX; //starting coordinate in world space
    public readonly int chunkY; //starting coordinate in world space
    public int worldTick;

    //CELL DATA
    public readonly int[] element;
    public readonly Vector2[] velocity;
    public readonly Moving[] moving;
    public readonly Color[] color;

    public readonly bool[] dirtyRectStep;

#if USE_DOUBLE_MWF_BUFFER
    //we double up the movedWithFrame array so we can reset
    //one while we use the other for the frame. Purely for performance.
    private readonly bool[] movedWithFrame1;
    private readonly bool[] movedWithFrame2;
    private bool frameFlip = false;
    private Task movedWithFrameReset = null;
#else
    public readonly bool[] movedWithFrame;
#endif

    public ref Rectangle DirtyRect => ref dirtyRect;
    public ref Rectangle RenderRect => ref renderRect;

    private Rectangle dirtyRect;
    private Rectangle prevDirtyRect;
    private Rectangle renderRect;
    public readonly SandWorld parentMatrix;
    public readonly WorldRegion region;

    public readonly int size;
    public readonly int indexSize;
    public readonly int sizeShift;
    public readonly int halfSize;
    public readonly int halfSizeShift;

    private WorldChunk[] multithreadChunkRef;

    public int gravity;

    public WorldChunk(SandWorld parent, WorldRegion region, int worldX, int worldY, int size)
    {
        chunkRNG = new Squirrel3(unchecked((long)worldX << 32 | (uint)worldY));

        this.indexSize = size * size;
        this.region = region;
        this.parentMatrix = parent;
        this.size = size;
        this.sizeShift = Rubedo.Lib.Math.GetPower2Exponent(size);
        this.halfSize = size / 2;
        this.halfSizeShift = sizeShift - 1;
        this.chunkX = worldX * size;
        this.chunkY = worldY * size;
        cameraIntersection = new RectF(chunkX - 4, chunkY - 4, size + 8, size + 8);

        dirtyRectStep = new bool[indexSize];
        element = new int[indexSize];
        velocity = new Vector2[indexSize];
        moving = new Moving[indexSize];
        color = new Color[indexSize];

#if USE_DOUBLE_MWF_BUFFER
        movedWithFrame1 = new bool[indexSize];
        movedWithFrame2 = new bool[indexSize];
#else
        movedWithFrame = new bool[indexSize];
#endif

        renderRect = new Rectangle(chunkX, chunkY, size, size);

        for (int i = 0; i < indexSize; i++)
        {
            int y1 = (i / size);
            int x1 = (i % size);

            element[i] = 0;
            velocity[i] = new Vector2(0, 0);
            moving[i] = new Moving() { isMoving = false, movingCount = 0 };
            color[i] = Color.Transparent;
        }

        multithreadChunkRef = new WorldChunk[9];
        multithreadChunkRef[4] = this;
    }
    #region Multithreaded
    bool flip = true;
    private void ResetMovedWithFrame()
    {
#if USE_DOUBLE_MWF_BUFFER
        if (frameFlip)
        {
            for (int i = 0; i < indexSize; i++)
            {
                movedWithFrame1[i] = false;
            }
        }
        else
        {
            for (int i = 0; i < indexSize; i++)
            {
                movedWithFrame2[i] = false;
            }
        }
#else
        for (int i = 0; i < indexSize; i++)
        {
            movedWithFrame[i] = false;
        }
#endif
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MovedWithFrame(int index)
    {
#if USE_DOUBLE_MWF_BUFFER
        if (frameFlip)
            return movedWithFrame2[index];
        else
            return movedWithFrame1[index];
#else
        return movedWithFrame[index];
#endif
    }

    public void MultithreadSetup(SandWorld matrix)
    {
        gravity = matrix.gravity;
#if USE_DOUBLE_MWF_BUFFER
        movedWithFrameReset?.Wait();
        frameFlip = !frameFlip;
        movedWithFrameReset = new Task(ResetMovedWithFrame);
        movedWithFrameReset.Start();
#else
        ResetMovedWithFrame();
#endif

        flip = !flip;

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
    public void MultithreadStep(SandWorld matrix, in int step)
    {
        if (dirtyRect.IsEmpty)
            return;

        int dirtyX = Rubedo.Lib.Math.Clamp(dirtyRect.X, chunkX, chunkX + size);
        int finX = Rubedo.Lib.Math.Clamp(dirtyRect.Right, chunkX, chunkX + size);
        int dirtyY = Rubedo.Lib.Math.Clamp(dirtyRect.Y, chunkY, chunkY + size);
        int finY = Rubedo.Lib.Math.Clamp(dirtyRect.Bottom, chunkY, chunkY + size);

        var localElementArray = this.element;
        var localMovedWithFrame = this.movedWithFrame;
        var localTypeLookup = ElementManager.typeLookup;

#if USE_ALTERNATING_UPDATE
        //bool sectionFlip = flip;
        bool sectionFlip = flip ^ (((dirtyY - chunkY) & 1) != 0);

#if USE_CHECKERBOARD_UPDATE

        int evenWidth = ((finX - dirtyX) & 1) == 0 ? 1 : 0;

        if (step == 1)
        {
            int xStart = 0;
            if (flip)
            {
                xStart = 1;
            }
            for (int y = dirtyY; y < finY; y++)
            {
                int yIndex = (y - chunkY) * size;
                if (flip)
                {
                    for (int x = finX - 1 - xStart; x >= dirtyX; x -= 2)
                    {
                        int cellID = yIndex + (x - chunkX);
                        bool moved = localMovedWithFrame[cellID];
                        if (!moved)
                        {
                            int elementID = localElementArray[cellID];
                            if (elementID == ElementManager.EMPTY)
                                continue;

                            ElementManager.Type elementType = localTypeLookup[elementID];
                            switch (elementType)
                            {
                                case ElementManager.Type.LIQUID:
                                    ElementBehaviour.StepLiquid(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.GAS:
                                    ElementBehaviour.StepGas(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.PHYSICS_SOLID:
                                    break;
                                case ElementManager.Type.EMPTY:
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    for (int x = dirtyX + xStart; x < finX; x += 2)
                    {
                        int cellID = yIndex + (x - chunkX);
                        bool moved = localMovedWithFrame[cellID];
                        if (!moved)
                        {
                            int elementID = localElementArray[cellID];
                            if (elementID == ElementManager.EMPTY)
                                continue;

                            ElementManager.Type elementType = localTypeLookup[elementID];
                            switch (elementType)
                            {
                                case ElementManager.Type.LIQUID:
                                    ElementBehaviour.StepLiquid(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.GAS:
                                    ElementBehaviour.StepGas(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.PHYSICS_SOLID:
                                    break;
                                case ElementManager.Type.EMPTY:
                                    break;
                            }
                        }
                    }
                }

                xStart = xStart == 0 ? 1 : 0;
            }
        }
        else
        {
#endif
            for (int y = dirtyY; y < finY; y++)
            {
                int yIndex = (y - chunkY) * size;
                if (sectionFlip)
                {
                    for (int x = finX - 1; x >= dirtyX; x--)
                    {
                        int cellID = yIndex + (x - chunkX);
                        bool moved = localMovedWithFrame[cellID];
                        if (!moved)
                        {
                            int elementID = localElementArray[cellID];
                            if (elementID == ElementManager.EMPTY)
                                continue;

                            ElementManager.Type elementType = localTypeLookup[elementID];
                            switch (elementType)
                            {
                                case ElementManager.Type.LIQUID:
                                    ElementBehaviour.StepLiquid(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.GAS:
                                    ElementBehaviour.StepGas(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.PHYSICS_SOLID:
                                    break;
                                case ElementManager.Type.EMPTY:
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    for (int x = dirtyX; x < finX; x++)
                    {
                        int cellID = yIndex + (x - chunkX);
                        bool moved = localMovedWithFrame[cellID];
                        if (!moved)
                        {
                            int elementID = localElementArray[cellID];
                            if (elementID == ElementManager.EMPTY)
                                continue;

                            ElementManager.Type elementType = localTypeLookup[elementID];
                            switch (elementType)
                            {
                                case ElementManager.Type.LIQUID:
                                    ElementBehaviour.StepLiquid(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.GAS:
                                    ElementBehaviour.StepGas(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.PHYSICS_SOLID:
                                    break;
                                case ElementManager.Type.EMPTY:
                                    break;
                            }
                        }
                    }
                }
                sectionFlip = !sectionFlip;
            }
#if USE_CHECKERBOARD_UPDATE
        }
#endif
#else
#if USE_CHECKERBOARD_UPDATE
        if (step == 1)
        {
            int xStart = 0;
            if (flip)
            {
                xStart = 1;
            }
            for (int y = dirtyY; y < finY; y++)
            {
                int yIndex = (y - chunkY) * size;
                if (flip)
                {
                    for (int x = finX - 1 - xStart; x >= dirtyX; x -= 2)
                    {
                        int cellID = yIndex + (x - chunkX);
                        bool moved = localMovedWithFrame[cellID];
                        if (!moved)
                        {
                            int elementID = localElementArray[cellID];
                            if (elementID == ElementManager.EMPTY)
                                continue;

                            ElementManager.Type elementType = localTypeLookup[elementID];
                            switch (elementType)
                            {
                                case ElementManager.Type.LIQUID:
                                    ElementBehaviour.StepLiquid(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.GAS:
                                    ElementBehaviour.StepGas(this, in x, in y, cellID);
                                    break;
                                case ElementManager.Type.PHYSICS_SOLID:
                                    break;
                                case ElementManager.Type.EMPTY:
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    for (int x = dirtyX + xStart; x < finX; x += 2)
                    {
                        int cellID = yIndex + (x - chunkX);
                        bool moved = localMovedWithFrame[cellID];
                        if (!moved)
                        {
                            int elementID = localElementArray[cellID];
                            if (elementID == ElementManager.EMPTY)
                                continue;

                            ElementManager.Type elementType = localTypeLookup[elementID];
                            switch (elementType)
                            {
                                case ElementManager.Type.LIQUID:
                                    ElementBehaviour.StepLiquid(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.GAS:
                                    ElementBehaviour.StepGas(this, in x, in y, cellID);
                                    break;
                                case ElementManager.Type.PHYSICS_SOLID:
                                    break;
                                case ElementManager.Type.EMPTY:
                                    break;
                            }
                        }
                    }
                }

                xStart = xStart == 0 ? 1 : 0;
            }
        }
        else
        {
#endif
            for (int y = dirtyY; y < finY; y++)
            {
                int yIndex = (y - chunkY) * size;
                if (flip)
                {
                    for (int x = finX - 1; x >= dirtyX; x--)
                    {
                        int cellID = yIndex + (x - chunkX);
                        bool moved = localMovedWithFrame[cellID];
                        if (!moved)
                        {
                            int elementID = localElementArray[cellID];
                            if (elementID == ElementManager.EMPTY)
                                continue;

                            ElementManager.Type elementType = localTypeLookup[elementID];
                            switch (elementType)
                            {
                                case ElementManager.Type.LIQUID:
                                    ElementBehaviour.StepLiquid(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.GAS:
                                    ElementBehaviour.StepGas(this, in x, in y, cellID);
                                    break;
                                case ElementManager.Type.PHYSICS_SOLID:
                                    break;
                                case ElementManager.Type.EMPTY:
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    for (int x = dirtyX; x < finX; x++)
                    {
                        int cellID = yIndex + (x - chunkX);
                        bool moved = localMovedWithFrame[cellID];
                        if (!moved)
                        {
                            int elementID = localElementArray[cellID];
                            if (elementID == ElementManager.EMPTY)
                                continue;

                            ElementManager.Type elementType = localTypeLookup[elementID];
                            switch (elementType)
                            {
                                case ElementManager.Type.LIQUID:
                                    ElementBehaviour.StepLiquid(this, in x, in y, cellID, in elementID);
                                    break;
                                case ElementManager.Type.GAS:
                                    ElementBehaviour.StepGas(this, in x, in y, cellID);
                                    break;
                                case ElementManager.Type.PHYSICS_SOLID:
                                    break;
                                case ElementManager.Type.EMPTY:
                                    break;
                            }
                        }
                    }
                }
            }
#if USE_CHECKERBOARD_UPDATE
        }
#endif
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
                int i = GetCellIndex(in x, in y);
                if (dirtyRectStep[i])
                {
                    minX = minX == int.MaxValue ? x : System.Math.Min(minX, x);
                    minY = minY == int.MaxValue ? y : System.Math.Min(minY, y);
                    maxX = maxX == int.MinValue ? x : System.Math.Max(maxX, x);
                    maxY = maxY == int.MinValue ? y : System.Math.Max(maxY, y);
                }
                dirtyRectStep[i] = false;
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
    public void SetMovedWithFrame(in int x, in int y)
    {
        int i = GetCellIndex(in x, in y);
        SetMovedWithFrame(i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMovedWithFrame(in int index)
    {
#if USE_DOUBLE_MWF_BUFFER
        if (frameFlip)
        {
            movedWithFrame2[index] = true;
        }
        else
        {
            movedWithFrame1[index] = true;
        }
#else
        movedWithFrame[index] = true;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ThreadEnvelop(in int x, in int y)
    {
        int i = GetCellIndex(in x, in y);
        dirtyRectStep[i] = true;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ThreadEnvelop(in int index)
    {
        dirtyRectStep[index] = true;
    }

    /// <summary>
    /// Swaps the indices of the two cells. Should only be called for intrachunk swaps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SwapCells(in int x1, in int y1, in int actorID, in int x2, in int y2, in int targetID)
    {
        Swap(in actorID, in targetID);

        int localX1 = x1 - chunkX;
        int localX2 = x2 - chunkX;
        int localY1 = y1 - chunkY;
        int localY2 = y2 - chunkY;

        ThreadEnvelop(actorID);
        ThreadEnvelop(targetID);
        Pad(localX1, localY1);
        Pad(localX2, localY2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Swap(in int actor, in int target)
    {
        (element[actor], element[target]) = (element[target], element[actor]);
        (velocity[actor], velocity[target]) = (velocity[target], velocity[actor]);
        (moving[actor], moving[target]) = (moving[target], moving[actor]);
        (color[actor], color[target]) = (color[target], color[actor]);
    }


    /// <summary>
    /// Swaps two cells around between two chunks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Swap(in WorldChunk other, int ours, int theirs)
    {
        (element[ours], other.element[theirs]) = (other.element[theirs], element[ours]);
        (velocity[ours], other.velocity[theirs]) = (other.velocity[theirs], velocity[ours]);
        (moving[ours], other.moving[theirs]) = (other.moving[theirs], moving[ours]);
        (color[ours], other.color[theirs]) = (other.color[theirs], color[ours]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Pad(in int localX, in int localY)
    {
        //determine which chunk edge we're on.
        int xDir = 1;
        if (localX == 0)
            xDir = 0;
        else if (localX == size - 1)
            xDir = 2;

        int yDir = 1;
        if (localY == 0)
            yDir = 0;
        else if (localY == size - 1)
            yDir = 2;

        if (yDir == 1 && xDir == 1)
        {
            return;
        }

        int chunkIndex = (yDir * 3) + xDir;
        multithreadChunkRef[chunkIndex]?.ThreadEnvelop(localX + chunkX + xDir - 1, localY + chunkY + yDir - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCellIndex(in int x, in int y)
    {
        return ((y - chunkY) * size) + (x - chunkX);
    }

    public bool TryGetCell(in int x, in int y, out WorldChunk containing, out int cellID)
    {
        if (InBounds(x, y))
        {
            containing = this;
            cellID = GetCellIndex(in x, in y);
            return true;
        }
        else if (InMultiBounds(in x, in y))
        {
            containing = GetMultiChunk(in x, in y);
            bool exists = containing != null;
            if (exists)
                cellID = containing.GetCellIndex(in x, in y);
            else
                cellID = -1;
            return exists;
        }

        containing = null;
        cellID = -1;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public WorldChunk GetMultiChunk(in int x, in int y)
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
    public bool OnlyMultiBounds(in int x, in int y)
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
    public bool InMultiBounds(in int x, in int y)
    {
        return x >= chunkX - halfSize && x < chunkX + size + halfSize
            && y >= chunkY - halfSize && y < chunkY + size + halfSize;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InBounds(in int x, in int y)
    {
        return x >= chunkX && x < chunkX + size
            && y >= chunkY && y < chunkY + size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMovingFaster(ref readonly int cellID, ref readonly ElementManager.Type elementType, ref readonly byte inertialRes)
    {
        ref Moving moving = ref this.moving[cellID];
        if (moving.isMoving)
            return;

        if (inertialRes < chunkRNG.Percent())
        {
            moving.isMoving = true;
            moving.movingCount = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetMoving(in int cellID, in int elementID)
    {
        if (elementID == ElementManager.EMPTY)
            return;

        ref Moving moving = ref this.moving[cellID];
        if (moving.isMoving || ElementManager.typeLookup[elementID] != ElementManager.Type.LIQUID)
            return;

        if (ElementManager.liquid_inertialResistance[elementID] < chunkRNG.Percent())
        {
            moving.isMoving = true;
            moving.movingCount = 0; //naughty naughty, mutating a struct...
        }
    }
    public void SetMovingPos(in int x, in int y)
    {
        if (InBounds(in x, in y))
        {
            int cell = GetCellIndex(in x, in y);
            int elementID = this.element[cell];
            SetMoving(cell, elementID);
        }
        else
        {
            WorldChunk chunk = GetMultiChunk(in x, in y);
            if (chunk != null)
            {
                int cell = chunk.GetCellIndex(in x, in y);
                int elementID = chunk.element[cell];
                chunk.SetMoving(in cell, in elementID);
            }
        }
    }

#endregion

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
                    int draw = region.GetDrawIndex(x, y);
                    int cellID = GetCellIndex(in x, in y);

                    if (parentMatrix.drawMoveOverride)
                    {
                        if (this.element[cellID] == 0)
                        {
                            buffer[draw] = Color.Transparent;
                        }
                        else if (!MovedWithFrame(cellID))
                        {
                            buffer[draw] = Color.Red;
                        }
                        else
                        {
                            buffer[draw] = Color.Green;
                        }
                    }
                    else
                    {
                        buffer[draw] = this.color[cellID];
                    }
                }
            }
            renderRect.Width = 0;
            renderRect.Height = 0;
            return true;
        }
        return false;
    }

    public struct Moving
    {
        public bool isMoving;
        public byte movingCount;
    }
}