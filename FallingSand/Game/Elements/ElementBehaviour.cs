using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using System.Runtime.CompilerServices;

namespace FallingSand.Game.Elements;
public static class ElementBehaviour
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotBorderCellPowder(in int x, in int y, in int chunkSize, in int max_speed)
    {
        return x > 1 && x < chunkSize - 2 && y >= max_speed && y < chunkSize - 2;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotBorderCellLiquid(in int x, in int y, in int chunkSize, in int max_speed, in int dispersion)
    {
        return x > dispersion + 1 && x < chunkSize - dispersion - 1 && y >= max_speed && y < chunkSize - 2;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotBorderCellGas(in int x, in int y, in int chunkSize)
    {
        return x > 1 && x < chunkSize - 2 && y < chunkSize - 1 && y > 0;
    }

    public static void StepLiquid(in WorldChunk caller, in int x, in int y, int cellID, in int elementID)
    {
        if (ElementManager.liquid_isStatic[elementID])
            return;

        if (ElementManager.liquid_isSand[elementID])
        {
            WorldChunk.Moving moving = caller.moving[cellID];
            if (moving.isMoving)
            {
                if (IsNotBorderCellPowder(x - caller.chunkX, y - caller.chunkY, caller.size, ElementManager.liquid_maxSpeed[elementID]))
                {
                    if (CellBehaviour.TryMoveDownTripleSameChunk(in caller, in x, in y, ref cellID))
                        return;
                    if (CellBehaviour.TryReactDirectionSameChunk(in caller, in x, in y, in cellID, in elementID, 1))
                        return;
                }
                else
                {
                    WorldChunk callerNonRef = caller;
                    if (CellBehaviour.TryMoveDownTriple(ref callerNonRef, x, y, ref cellID))
                        return;
                    if (CellBehaviour.TryReactDirection(ref callerNonRef, in x, in y, in cellID, in elementID, 1))
                        return;
                }

                ref WorldChunk.Moving move = ref caller.moving[cellID];
                moving.movingCount++;
                if (moving.movingCount >= ElementManager.FREE_FALLING_THRESHOLD)
                {
                    moving.isMoving = false; //failed to move.
                    ref Vector2 velocity = ref caller.velocity[cellID];
                    velocity.X = 0;
                    velocity.Y = 0;
                    caller.velocity[cellID] = velocity;
                    caller.moving[cellID] = moving;
                }
                return;
            }
            else
            {
                WorldChunk callerNonRef = caller;
                if (CellBehaviour.TryMoveDown(ref callerNonRef, x, y, ref cellID))
                    return;
                if (IsNotBorderCellPowder(x - caller.chunkX, y - caller.chunkY, caller.size, ElementManager.liquid_maxSpeed[elementID]))
                {
                    if (CellBehaviour.TryReactDirectionSameChunk(in caller, in x, in y, in cellID, in elementID, 1))
                        return;
                }
                else
                {
                    if (CellBehaviour.TryReactDirection(ref callerNonRef, in x, in y, in cellID, in elementID, 1))
                        return;
                }
            }
        }
        else //this is a liquid like water
        {
            int dispersion = ElementManager.liquid_dispersion[elementID];
            if (IsNotBorderCellLiquid(x - caller.chunkX, y - caller.chunkY, caller.size, ElementManager.liquid_maxSpeed[elementID], dispersion))
            {
                if (CellBehaviour.TryFallSameChunk(in caller, in x, in y, ref cellID))
                    return;

                if (CellBehaviour.TryMoveSideSameChunk(in caller, in x, in y, ref cellID, in dispersion))
                {
                    //we should stop moving down so fast.
                    Vector2 velocity = caller.velocity[cellID];
                    velocity.Y /= 2;
                    caller.velocity[cellID] = velocity;

                    return;
                }
                if (CellBehaviour.TryReactDirectionSameChunk(in caller, in x, in y, in cellID, in elementID, 1))
                    return;

                ref WorldChunk.Moving moving = ref caller.moving[cellID];
                moving.movingCount++;
                if (moving.movingCount >= ElementManager.FREE_FALLING_THRESHOLD)
                {
                    //liquid cells don't actually stop moving, they just reset velocities.
                    ref Vector2 velocity = ref caller.velocity[cellID];
                    moving.movingCount = 0;
                    velocity.X = 0;
                    velocity.Y = 0;
                }
            }
            else
            {
                WorldChunk callerNonRef = caller;
                if (CellBehaviour.TryFall(ref callerNonRef, in x, in y, ref cellID))
                    return;

                if (CellBehaviour.TryMoveSide(ref callerNonRef, in x, in y, ref cellID, in dispersion))
                {
                    //we should stop moving down so fast.
                    Vector2 velocity = callerNonRef.velocity[cellID];
                    velocity.Y /= 2;
                    callerNonRef.velocity[cellID] = velocity;

                    return;
                }
                if (CellBehaviour.TryReactDirection(ref callerNonRef, in x, in y, in cellID, in elementID, 1))
                    return;

                ref WorldChunk.Moving moving = ref callerNonRef.moving[cellID];
                moving.movingCount++;
                if (moving.movingCount >= ElementManager.FREE_FALLING_THRESHOLD)
                {
                    //liquid cells don't actually stop moving, they just reset velocities.
                    ref Vector2 velocity = ref callerNonRef.velocity[cellID];
                    moving.movingCount = 0;
                    velocity.X = 0;
                    velocity.Y = 0;
                }
            }
        }
    }

    public static void StepGas(in WorldChunk caller, in int x, in int y, int cellID, in int elementID)
    {
        bool doDiagonal = caller.chunkRNG.Percent() < 25;
        if (IsNotBorderCellGas(x - caller.chunkX, y - caller.chunkY, caller.size))
        {
            if (doDiagonal)
            { //try to move diagonally first
                if (CellBehaviour.TryDiagonalUpSameChunk(in caller, in x, in y, ref cellID))
                    return;
                else if (CellBehaviour.TryRiseSameChunk(in caller, in x, in y, ref cellID))
                    return;
                else if (CellBehaviour.TryReactDirectionSameChunk(in caller, in x, in y, in cellID, in elementID, -1))
                    return;
            }
            else
            {
                if (CellBehaviour.TryRiseSameChunk(in caller, in x, in y, ref cellID))
                    return;
                else if (CellBehaviour.TryDiagonalUpSameChunk(in caller, in x, in y, ref cellID))
                    return;
                else if (CellBehaviour.TryReactDirectionSameChunk(in caller, in x, in y, in cellID, in elementID, -1))
                    return;
            }

            if (CellBehaviour.TryMoveSideOneSameChunk(in caller, in x, in y, ref cellID))
                return;
        }
        else
        {
            WorldChunk callerNonRef = caller;
            if (doDiagonal)
            { //try to move diagonally first
                if (CellBehaviour.TryDiagonalUp(ref callerNonRef, x, y, ref cellID))
                    return;
                else if (CellBehaviour.TryRise(ref callerNonRef, x, y, ref cellID))
                    return;
                else if (CellBehaviour.TryReactDirection(ref callerNonRef, in x, in y, in cellID, in elementID, 1))
                    return;
            }
            else
            {
                if (CellBehaviour.TryRise(ref callerNonRef, x, y, ref cellID))
                    return;
                else if (CellBehaviour.TryDiagonalUp(ref callerNonRef, x, y, ref cellID))
                    return;
                else if (CellBehaviour.TryReactDirection(ref callerNonRef, in x, in y, in cellID, in elementID, 1))
                    return;
            }

            if (CellBehaviour.TryMoveSideOne(ref callerNonRef, in x, in y, ref cellID))
                return;
        }
    }
}