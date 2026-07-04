using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using NLog.Targets;
using Rubedo;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am CellBehaviour, and I don't have a summary yet.
/// </summary>
public static class CellBehaviour
{
    public enum ActResult
    {
        /// <summary>
        /// The cell has moved, and is allowed to continue moving
        /// </summary>
        Move,
        /// <summary>
        /// The cell has moved, and can't move anymore
        /// </summary>
        StopMove,
        /// <summary>
        /// The cell hasn't moved, and should not try any more
        /// </summary>
        Stop,
        /// <summary>
        /// The cell reacted with its target, which is a stop.
        /// </summary>
        Reaction
    }


    /// <summary>
    /// Version of CanBeSwapped that guarantees the target and actor are in the same chunk.
    /// </summary>
    public static bool CanBeSwapped(in WorldChunk caller, in int actorElement, in ElementManager.Type actorType, in int targetElement, in ElementManager.Type targetType)
    {
        return
            (targetElement == ElementManager.EMPTY) || 
            (ElementManager.liquid_isStatic[targetElement] &&
            ((targetType == ElementManager.Type.LIQUID && CompareDensities(actorElement, targetElement) && (!ElementManager.liquid_isSand[actorElement] || !ElementManager.liquid_isSand[targetElement])) ||
            (targetType == ElementManager.Type.GAS && actorType == ElementManager.Type.LIQUID) ||
            (targetType == ElementManager.Type.GAS && actorType == ElementManager.Type.GAS && CompareGasDensities(actorElement, targetElement))));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanBeSwapped(WorldChunk caller, WorldChunk target, int actorID, int targetID)
    {
        int targetElement = target.element[targetID];
        if (targetElement == ElementManager.EMPTY)
            return true;
        if (ElementManager.liquid_isStatic[targetElement])
            return false;
        int actorElement = caller.element[actorID];

        ElementManager.Type targetType = ElementManager.typeLookup[targetElement];
        ElementManager.Type actorType = ElementManager.typeLookup[actorElement];

        return
            (targetType == ElementManager.Type.LIQUID && CompareDensities(actorElement, targetElement) && (!ElementManager.liquid_isSand[actorElement] || !ElementManager.liquid_isSand[targetElement])) ||
            (targetType == ElementManager.Type.GAS && actorType == ElementManager.Type.LIQUID) ||
            (targetType == ElementManager.Type.GAS && actorType == ElementManager.Type.GAS && CompareGasDensities(actorElement, targetElement));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CompareDensities(int element1, int element2)
    {
        return ElementManager.density[element1] > ElementManager.density[element2];// && cell.y >= target.y;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CompareGasDensities(int element1, int element2)
    {
        return ElementManager.density[element1] < ElementManager.density[element2];// && cell.y <= target.y;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapForDensities(in WorldChunk caller, in int x1, in int y1, ref int actorID, in int x2, in int y2, in int targetID)
    {
        Vector2 velocity = caller.velocity[actorID];
        velocity.Y *= 0.5f;
        if (Rubedo.Lib.Random.Percent > 80)
        {
            velocity.X *= -1;
        }
        caller.velocity[actorID] = velocity;
        SwapPositions(caller, x1, y1, ref actorID, x2, y2, targetID);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapForDensities(ref WorldChunk caller, WorldChunk target, int x1, int y1, ref int actorID, int x2, int y2, ref int targetID)
    {
        Vector2 velocity = caller.velocity[actorID];
        velocity.Y *= 0.5f;
        if (Rubedo.Lib.Random.Percent > 80)
        {
            velocity.X *= -1;
        }
        caller.velocity[actorID] = velocity;
        SwapPositions(ref caller, target, x1, y1, ref actorID, x2, y2, ref targetID);
    }

    #region LIQUID
    public static bool TryMoveSide(ref WorldChunk caller, int x, int y, ref int cellID, int dispersion)
    {
        Vector2 velocity = caller.velocity[cellID];

        if (velocity.X != 0)
        {
            int dir = System.Math.Sign(velocity.X);
            ActResult res = CheckSide(ref caller, x, y, ref cellID, dispersion, dir);
            velocity.X += dir;
            switch (res)
            {
                case ActResult.StopMove:
                    velocity.X *= -1;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Move:
                case ActResult.Reaction:
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Stop:
                    velocity.X *= -1;
                    caller.velocity[cellID] = velocity;
                    return false;
            }
        }
        else
        {
            bool leftFirst = caller.chunkRNG.Flip();
            int firstDir = leftFirst ? -1 : 1;
            int secondDir = -firstDir;

            // Try first direction
            ActResult res = CheckSide(ref caller, x, y, ref cellID, dispersion, firstDir);
            switch (res)
            {
                case ActResult.StopMove:
                    velocity.X *= -1;
                    velocity.X += firstDir;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Move:
                case ActResult.Reaction:
                    velocity.X += firstDir;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Stop:
                    break; // try second
            }

            // Try second direction
            res = CheckSide(ref caller, x, y, ref cellID, dispersion, secondDir);
            switch (res)
            {
                case ActResult.StopMove:
                    velocity.X *= -1;
                    velocity.X += secondDir;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Move:
                case ActResult.Reaction:
                    velocity.X += secondDir;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Stop:
                    // both failed
                    return false;
            }
            return false;
        }
        return false;
    }

    private static ActResult CheckSide(ref WorldChunk caller, int cellX, int cellY, ref int cellID, int dispersion, int direction)
    {
        ActResult res = ActResult.Stop;
        int moveX = cellX;
        for (int i = 1; i <= dispersion; i++)
        {
            int targetX = cellX + (direction * i);
            if (caller.TryGetCell(targetX, cellY, out WorldChunk container, out int otherID ))
            {
                ActResult actRes = ActOnCell(ref caller, container, moveX, cellY, ref cellID, targetX, cellY, ref otherID);
                switch (actRes)
                {
                    case ActResult.Move:
                        res = actRes;
                        moveX = targetX;
                        continue;
                    case ActResult.Reaction:
                    case ActResult.StopMove:
                        return actRes;
                    case ActResult.Stop:
                        return res;
                }
            }
        }
        return res;
    }

    /// <summary>
    /// MoveDownTriple that cannot move out of its chunk.
    /// </summary>
    public static bool TryMoveDownTripleSameChunk(in WorldChunk caller, in int x, in int y, ref int cellID)
    {
        int dID = caller.GetCellIndex(x, y - 1);
        int dlID = dID - 1;
        int drID = dID + 1;

        int actorElement = caller.element[cellID];
        int dlElement = caller.element[dlID];
        int dElement = caller.element[dID];
        int drElement = caller.element[drID];

        ElementManager.Type actorType = ElementManager.typeLookup[actorElement];
        ElementManager.Type dlType = ElementManager.typeLookup[dlElement];
        ElementManager.Type dType = ElementManager.typeLookup[dElement];
        ElementManager.Type drType = ElementManager.typeLookup[drElement];

        bool canMoveDL = CanBeSwapped(in caller, in actorElement, in actorType, in dlElement, in dlType);
        bool canMoveD = CanBeSwapped(in caller, in actorElement, in actorType, in dElement, in dType);
        bool canMoveDR = CanBeSwapped(in caller, in actorElement, in actorType, in drElement, in drType);

        if (canMoveD)// && !((canMoveDR || canMoveDL) && caller.chunkRNG.Percent() < 25))
        {
            return TryFallSameChunk(in caller, in x, in y, ref cellID);
        }
        else if (canMoveDR || canMoveDL)
        {
            return TryDiagonalDownSameChunk(in caller, in x, in y, ref cellID);
        }
        return false;
    }

    public static bool TryMoveDownTriple(ref WorldChunk caller, int x, int y, ref int cellID)
    {
        bool canMoveDL = caller.TryGetCell(x - 1, y - 1, out WorldChunk chunkDL, out int dlID) && CanBeSwapped(caller, chunkDL, cellID, dlID);
        bool canMoveDR = caller.TryGetCell(x + 1, y - 1, out WorldChunk chunkDR, out int drID) && CanBeSwapped(caller, chunkDR, cellID, drID);
        bool canMoveD = caller.TryGetCell(x, y - 1, out WorldChunk chunkD, out int dID) && CanBeSwapped(caller, chunkD, cellID, dID);

        if (canMoveD)// && !((canMoveDR || canMoveDL) && caller.chunkRNG.Percent() < 25))
        {
            return TryFall(ref caller, x, y, ref cellID);
        }
        else if (canMoveDR || canMoveDL)
        {
            return TryDiagonalDown(ref caller, x, y, ref cellID);
        }
        return false;
    }

    public static bool TryFallSameChunk(in WorldChunk caller, in int cellX, in int cellY, ref int actorID)
    {
        int elementID = caller.element[actorID];
        ref Vector2 velocity = ref caller.velocity[actorID];

        float velUpdate = caller.gravity * ElementManager.liquid_gravity[elementID] * Time.FixedDeltaTime * 2;
        int maxSpeed = ElementManager.liquid_maxSpeed[elementID];
        velocity.Y = Rubedo.Lib.Math.Clamp(velocity.Y - velUpdate, -maxSpeed, maxSpeed);
        int yVel = System.Math.Abs(Rubedo.Lib.Math.RoundAwayFromZero(velocity.Y));

        //caller.velocity[actorID] = velocity;

        int moveY = cellY;

        bool ret = false;
        for (int y = 1; y <= yVel; y++)
        {
            int lowerMoveY = moveY - 1;
            int otherID = caller.GetCellIndex(in cellX, in lowerMoveY);
            ActResult actRes = ActOnCellSameChunk(in caller, in cellX, in moveY, ref actorID, in cellX, in lowerMoveY, in otherID);
            switch (actRes)
            {
                case ActResult.Move:
                    ret = true;
                    moveY = lowerMoveY;
                    actorID = otherID;
                    continue;
                case ActResult.Reaction:
                case ActResult.StopMove:
                    return true;
                case ActResult.Stop:
                    return ret;
            }
        }
        return ret;
    }

    public static bool TryFall(ref WorldChunk caller, int cellX, int cellY, ref int actorID)
    {
        int elementID = caller.element[actorID];
        ref Vector2 velocity = ref caller.velocity[actorID];

        float velUpdate = caller.gravity * ElementManager.liquid_gravity[elementID] * Time.FixedDeltaTime * 2;
        int maxSpeed = ElementManager.liquid_maxSpeed[elementID];
        velocity.Y = Rubedo.Lib.Math.Clamp(velocity.Y - velUpdate, -maxSpeed, maxSpeed);
        int yVel = System.Math.Abs(Rubedo.Lib.Math.RoundAwayFromZero(velocity.Y));

        caller.velocity[actorID] = velocity;

        int moveY = cellY;

        bool ret = false;
        for (int y = 1; y <= yVel; y++)
        {
            if (caller.TryGetCell(cellX, moveY - 1, out WorldChunk container, out int otherID))
            {
                ActResult actRes = ActOnCell(ref caller, container, cellX, moveY, ref actorID, cellX, moveY - 1, ref otherID);
                switch (actRes)
                {
                    case ActResult.Move:
                        ret = true;
                        moveY -= 1;
                        continue;
                    case ActResult.Reaction:
                    case ActResult.StopMove:
                        return true;
                    case ActResult.Stop:
                        return ret;
                }
            }
        }
        return ret;
    }

    public static bool TryDiagonalDownSameChunk(in WorldChunk caller, in int cellX, in int cellY, ref int cellID)
    {
        Vector2 velocity = caller.velocity[cellID];

        int dlID = caller.GetCellIndex(cellX - 1, cellY - 1);
        int drID = dlID + 2;

        int actorElement = caller.element[cellID];
        int dlElement = caller.element[dlID];
        int drElement = caller.element[drID];

        ElementManager.Type actorType = ElementManager.typeLookup[actorElement];
        ElementManager.Type dlType = ElementManager.typeLookup[dlElement];
        ElementManager.Type drType = ElementManager.typeLookup[drElement];

        bool downLeftSwappable = CanBeSwapped(in caller, in actorElement, in actorType, in dlElement, in dlType);
        bool downRightSwappable = CanBeSwapped(in caller, in actorElement, in actorType, in drElement, in drType);

        if (downLeftSwappable && downRightSwappable)
        {
            bool leftFirst = caller.chunkRNG.Flip();
            int firstID = leftFirst ? dlID : drID;
            int secondID = leftFirst ? drID : dlID;
            int firstDir = leftFirst ? -1 : 1;
            int secondDir = -firstDir;

            // try first
            ActResult actRes = ActOnCellSameChunk(in caller, in cellX, in cellY, ref cellID, cellX + firstDir, cellY - 1, in firstID);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    velocity.X = firstDir;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Stop:
                    break;
            }

            // try second
            actRes = ActOnCellSameChunk(in caller, in cellX, in cellY, ref cellID, cellX + secondDir, cellY - 1, in secondID);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    velocity.X = secondDir;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Stop:
                    return false;
            }
            return false;
        }
        else if (downLeftSwappable)
        {
            ActResult actRes = ActOnCellSameChunk(in caller, in cellX, in cellY, ref cellID, cellX - 1, cellY - 1, in dlID);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    velocity.X = -1;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        else if (downRightSwappable)
        {
            ActResult actRes = ActOnCellSameChunk(in caller, in cellX, in cellY, ref cellID, cellX + 1, cellY - 1, in drID);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    velocity.X = 1;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        return false;
    }

    public static bool TryDiagonalDown(ref WorldChunk caller, int cellX, int cellY, ref int cellID)
    {
        Vector2 velocity = caller.velocity[cellID];

        bool downLeftExists = caller.TryGetCell(cellX - 1, cellY - 1, out WorldChunk chunkDL, out int dlID);
        bool downRightExists = caller.TryGetCell(cellX + 1, cellY - 1, out WorldChunk chunkDR, out int drID);

        if (downLeftExists && downRightExists)
        {
            bool leftFirst = caller.chunkRNG.Flip();
            int firstID = leftFirst ? dlID : drID;
            int secondID = leftFirst ? drID : dlID;
            WorldChunk firstChunk = leftFirst ? chunkDL : chunkDR;
            WorldChunk secondChunk = leftFirst ? chunkDR : chunkDL;
            int firstDir = leftFirst ? -1 : 1;
            int secondDir = -firstDir;

            // try first
            {
                ActResult actRes = ActOnCell(ref caller, firstChunk, cellX, cellY, ref cellID, cellX + firstDir, cellY - 1, ref firstID);
                switch (actRes)
                {
                    case ActResult.Move:
                    case ActResult.Reaction:
                    case ActResult.StopMove:
                        velocity.X = firstDir;
                        caller.velocity[cellID] = velocity;
                        return true;
                    case ActResult.Stop:
                        break;
                }
            }

            // try second
            {
                ActResult actRes = ActOnCell(ref caller, secondChunk, cellX, cellY, ref cellID, cellX + secondDir, cellY - 1, ref secondID);
                switch (actRes)
                {
                    case ActResult.Move:
                    case ActResult.Reaction:
                    case ActResult.StopMove:
                        velocity.X = secondDir;
                        caller.velocity[cellID] = velocity;
                        return true;
                    case ActResult.Stop:
                        return false;
                }
            }
            return false;
        }
        else if (downLeftExists)
        {
            ActResult actRes = ActOnCell(ref caller, chunkDL, cellX, cellY, ref cellID, cellX - 1, cellY - 1, ref dlID);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    velocity.X = -1;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        else if (downRightExists)
        {
            ActResult actRes = ActOnCell(ref caller, chunkDR, cellX, cellY, ref cellID, cellX + 1, cellY - 1, ref drID);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    velocity.X = 1;
                    caller.velocity[cellID] = velocity;
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Try to move down 1 cell.
    /// </summary>
    /// <returns></returns>
    public static bool TryMoveDown(ref WorldChunk caller, int x, int y, ref int actorID)
    {
        if (caller.TryGetCell(x, y - 1, out WorldChunk targetChunk, out int targetID))
        {
            ActResult actRes = ActOnCell(ref caller, targetChunk, x, y, ref actorID, x, y - 1, ref targetID);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        return false;
    }
    #endregion

    #region GAS

    public static bool TryDiagonalUp(ref WorldChunk caller, int x, int y, ref int actorID)
    {
        Vector2 velocity = caller.velocity[actorID];

        bool upLeftExists = caller.TryGetCell(x - 1, y + 1, out WorldChunk upLeftChunk, out int upLeft);
        bool upRightExists = caller.TryGetCell(x + 1, y + 1, out WorldChunk upRightChunk, out int upRight);

        if (upLeftExists && upRightExists)
        {
            bool leftFirst = caller.chunkRNG.Flip();
            int firstID = leftFirst ? upLeft : upRight;
            int secondID = leftFirst ? upRight : upLeft;
            WorldChunk firstChunk = leftFirst ? upLeftChunk : upRightChunk;
            WorldChunk secondChunk = leftFirst ? upRightChunk : upLeftChunk;
            int firstDir = leftFirst ? -1 : 1;
            int secondDir = -firstDir;

            // try first
            {
                ActResult actRes = ActOnCell(ref caller, firstChunk, x, y, ref actorID, x + firstDir, y + 1, ref firstID);
                switch (actRes)
                {
                    case ActResult.Move:
                    case ActResult.Reaction:
                    case ActResult.StopMove:
                        velocity.X = firstDir;
                        caller.velocity[actorID] = velocity;
                        return true;
                    case ActResult.Stop:
                        break;
                }
            }

            // try second
            {
                ActResult actRes = ActOnCell(ref caller, secondChunk, x, y, ref actorID, x + secondDir, y + 1, ref secondID);
                switch (actRes)
                {
                    case ActResult.Move:
                    case ActResult.Reaction:
                    case ActResult.StopMove:
                        velocity.X = secondDir;
                        caller.velocity[actorID] = velocity;
                        return true;
                    case ActResult.Stop:
                        return false;
                }
            }
            return false;
        }
        else if (upLeftExists)
        {
            ActResult actRes = ActOnCell(ref caller, upLeftChunk, x, y, ref actorID, x - 1, y + 1, ref upLeft);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    velocity.X = -1;
                    caller.velocity[actorID] = velocity;
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        else if (upRightExists)
        {
            ActResult actRes = ActOnCell(ref caller, upRightChunk, x, y, ref actorID, x + 1, y + 1, ref upRight);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    velocity.X = 1;
                    caller.velocity[actorID] = velocity;
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        return false;
    }

    public static bool TryRise(ref WorldChunk caller, int x, int y, ref int actorID)
    {
        if (caller.TryGetCell(x, y + 1, out WorldChunk targetChunk, out int targetID))
        {
            ActResult actRes = ActOnCell(ref caller, targetChunk, x, y, ref actorID, x, y + 1, ref targetID);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        return false;
    }
    #endregion

    /// <summary>
    /// Version of SwapPositions that requires all cells in a 3x2 to share a chunk.
    /// </summary>
    public static void SwapPositions(in WorldChunk caller, in int x1, in int y1, ref int actorID, in int x2, in int y2, in int targetID)
    {
        int i1 = actorID - 1;
        int i2 = actorID + 1;
        int i3 = targetID - 1;
        int i4 = targetID + 1;

        int aEl = caller.element[actorID];
        int tEl = caller.element[targetID];
        int i1El = caller.element[i1];
        int i2El = caller.element[i2];
        int i3El = caller.element[i3];
        int i4El = caller.element[i4];

        ElementManager.Type tType = ElementManager.typeLookup[tEl];
        ElementManager.Type i1Type = ElementManager.typeLookup[i1El];
        ElementManager.Type i2Type = ElementManager.typeLookup[i2El];
        ElementManager.Type i3Type = ElementManager.typeLookup[i3El];
        ElementManager.Type i4Type = ElementManager.typeLookup[i4El];

        byte tInRes = ElementManager.liquid_inertialResistance[tEl];
        byte i1InRes = ElementManager.liquid_inertialResistance[i1El];
        byte i2InRes = ElementManager.liquid_inertialResistance[i2El];
        byte i3InRes = ElementManager.liquid_inertialResistance[i3El];
        byte i4InRes = ElementManager.liquid_inertialResistance[i4El];

        caller.movedWithFrame[actorID] = true;
        caller.movedWithFrame[targetID] = true;

        if (tType == ElementManager.Type.LIQUID)
        {
            caller.SetMovingFaster(in targetID, in tType, in tInRes);
        }
        if (i1Type == ElementManager.Type.LIQUID)
        {
            caller.SetMovingFaster(in i1, in i1Type, in i1InRes);
        }
        if (i2Type == ElementManager.Type.LIQUID)
        {
            caller.SetMovingFaster(in i2, in i2Type, in i2InRes);
        }
        if (i3Type == ElementManager.Type.LIQUID)
        {
            caller.SetMovingFaster(in i3, in i3Type, in i3InRes);
        }
        if (i4Type == ElementManager.Type.LIQUID)
        {
            caller.SetMovingFaster(in i4, in i4Type, in i4InRes);
        }

        ref WorldChunk.Moving actorMoving = ref caller.moving[actorID];
        actorMoving.movingCount = 0;

        caller.SwapCells(in x1, in y1, in actorID, in x2, in y2, in targetID);

        actorID = targetID;
    }

    public static void SwapPositions(ref WorldChunk callingChunk, WorldChunk targetChunk, int x1, int y1, ref int actorID, int x2, int y2, ref int targetID)
    {
        if (targetChunk == callingChunk)
        {
            if (actorID == targetID)
                return;
            callingChunk.SwapCells(x1, y1, actorID, x2, y2, targetID);

            int aEl = callingChunk.element[actorID];
            int tEl = callingChunk.element[targetID];

            callingChunk.SetMoving(actorID, aEl);
            callingChunk.SetMovingPos(x1 - 1, y1);
            callingChunk.SetMovingPos(x1 + 1, y1);
            callingChunk.SetMovingPos(x2 - 1, y2);
            callingChunk.SetMoving(targetID, tEl);
            callingChunk.SetMovingPos(x2 + 1, y2);

            callingChunk.SetMovedWithFrame(x1, y1);
            callingChunk.SetMovedWithFrame(x2, y2);

            actorID = targetID;
        }
        else
        {
            callingChunk.Swap(targetChunk, actorID, targetID);
            //actor is now where target was, and vice-versa.

            callingChunk.ThreadEnvelop(x1, y1);
            targetChunk.ThreadEnvelop(x2, y2);

            int aEl = callingChunk.element[actorID];
            int tEl = targetChunk.element[targetID];

            callingChunk.SetMoving(actorID, aEl);
            targetChunk.SetMoving(targetID, tEl);

            callingChunk.SetMovingPos(x1 - 1, y1);
            callingChunk.SetMovingPos(x1 + 1, y1);
            targetChunk.SetMovingPos(x2 - 1, y2);
            targetChunk.SetMovingPos(x2 + 1, y2);

            callingChunk.SetMovedWithFrame(x1, y1);
            targetChunk.SetMovedWithFrame(x2, y2);

            callingChunk = targetChunk;
            int temp = actorID;
            actorID = targetID;
            targetID = temp;
        }
    }

    /// <summary>
    /// Handles reactions and what happens when cells run into eachother.
    /// </summary>
    public static ActResult ActOnCell(ref WorldChunk caller, WorldChunk targetChunk, int x1, int y1, ref int actorID, int x2, int y2, ref int targetID)
    {
        //if (caller == targetChunk)
        //    return ActOnCellSameChunk(caller, x1, y1, actorID, x2, y2, targetID);

        int actorElement = caller.element[actorID];
        int targetElement = targetChunk.element[targetID];

        caller.reactionCache.cellType1 = actorElement;
        caller.reactionCache.cellType2 = targetElement;
        if (ElementManager.HasReaction(in actorElement, in targetElement))
        {
            //TODO: Try to react
            return ActResult.Reaction;
        }
        if (targetElement == ElementManager.EMPTY)
        {
            SwapPositions(ref caller, targetChunk, x1, y1, ref actorID, x2, y2, ref targetID);
            return ActResult.Move; //it wasn't stopped.
        }

        ElementManager.Type actorType = ElementManager.typeLookup[actorElement];
        ElementManager.Type targetType = ElementManager.typeLookup[targetElement];

        switch (targetType)
        {
            case ElementManager.Type.PHYSICS_SOLID:
                break;
            case ElementManager.Type.LIQUID:
                ref WorldChunk.Moving moving = ref caller.moving[actorID];

                if (moving.isMoving) //we've hit something solid
                {
                    Vector2 velocity = caller.velocity[actorID];
                    float absY = System.Math.Abs(velocity.Y);
                    velocity.X = velocity.X > 0 ? absY : -absY;
                    caller.velocity[actorID] = velocity;
                }
                if (CanBeSwapped(in caller, in actorElement, in actorType, in targetElement, in targetType))
                {
                    if (ElementManager.liquid_isSand[targetElement])
                    {
                        SwapPositions(ref caller, targetChunk, x1, y1, ref actorID, x2, y2, ref targetID);
                        return ActResult.StopMove;
                    }
                    else
                    {
                        SwapForDensities(ref caller, targetChunk, x1, y1, ref actorID, x2, y2, ref targetID);
                        if (actorType == ElementManager.Type.LIQUID && !ElementManager.liquid_isSand[actorElement])
                        {
                            return ActResult.Move; //fluids can move fast through other fluids.
                        }
                        return ActResult.StopMove;
                    }
                }
                else
                {
                    return ActResult.Stop;
                }
               
            case ElementManager.Type.GAS:
                if (CanBeSwapped(caller, targetChunk, actorID, targetID))
                {
                    SwapForDensities(ref caller, targetChunk, x1, y1, ref actorID, x2, y2, ref targetID);
                    return ActResult.StopMove;
                }
                return ActResult.Stop;
        }
        return ActResult.Stop; //something unknown?
    }

    public static ActResult ActOnCellSameChunk(in WorldChunk caller, in int x1, in int y1, ref int actorID, in int x2, in int y2, in int targetID)
    {
        int actorElement = caller.element[actorID];
        int targetElement = caller.element[targetID];

        caller.reactionCache.cellType1 = actorElement;
        caller.reactionCache.cellType2 = targetElement;
        if (ElementManager.HasReaction(in actorElement, in targetElement))
        {
            //TODO: Try to react
            return ActResult.Reaction;
        }
        if (targetElement == ElementManager.EMPTY)
        {
            SwapPositions(in caller, in x1, in y1, ref actorID, in x2, in y2, in targetID);
            return ActResult.Move; //it wasn't stopped.
        }

        ElementManager.Type actorType = ElementManager.typeLookup[actorElement];
        ElementManager.Type targetType = ElementManager.typeLookup[targetElement];

        switch (targetType)
        {
            case ElementManager.Type.PHYSICS_SOLID:
                {
                    break;
                }
            case ElementManager.Type.LIQUID:
                {
                    ref WorldChunk.Moving moving = ref caller.moving[actorID];

                    if (moving.isMoving) //we've hit something solid
                    {
                        Vector2 velocity = caller.velocity[actorID];
                        float absY = System.Math.Abs(velocity.Y);
                        velocity.X = velocity.X > 0 ? absY : -absY;
                        caller.velocity[actorID] = velocity;
                    }

                    if (CanBeSwapped(in caller, in actorElement, in actorType, in targetElement, in targetType))
                    {
                        if (ElementManager.liquid_isSand[targetElement])
                        {
                            SwapPositions(caller, x1, y1, ref actorID, x2, y2, targetID);
                            return ActResult.StopMove;
                        }
                        else
                        {
                            SwapForDensities(in caller, in x1, in y1, ref actorID, in x2, in y2, in targetID);
                            if (actorType == ElementManager.Type.LIQUID && !ElementManager.liquid_isSand[actorElement])
                            {
                                return ActResult.Move; //fluids can move fast through other fluids.
                            }
                            return ActResult.StopMove;
                        }
                    }
                    else
                    {
                        return ActResult.Stop;
                    }
                }

            case ElementManager.Type.GAS:
                {
                    if (CanBeSwapped(in caller, in actorElement, in actorType, in targetElement, in targetType))
                    {
                        SwapForDensities(in caller, in x1, in y1, ref actorID, in x2, in y2, in targetID);
                        return ActResult.StopMove;
                    }
                    return ActResult.Stop;
                }
        }
        return ActResult.Stop; //something unknown?
    }
}