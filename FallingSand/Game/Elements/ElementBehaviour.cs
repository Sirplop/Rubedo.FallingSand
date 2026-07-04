using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using System.Runtime.CompilerServices;

namespace FallingSand.Game.Elements;
public static class ElementBehaviour
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotBorderCellLiquid(in int x, in int y, in int chunkSize, in int max_speed)
    {
        return x > 1 && x < chunkSize - 2 && y >= max_speed;
    }

    public static void StepLiquid(in WorldChunk caller, in int x, in int y, int cellID, int elementID)
    {
        if (ElementManager.liquid_isStatic[elementID])
            return;

        if (ElementManager.liquid_isSand[elementID])
        {
            WorldChunk.Moving moving = caller.moving[cellID];
            if (moving.isMoving)
            {
                if (IsNotBorderCellLiquid(x - caller.chunkX, y - caller.chunkY, caller.size, ElementManager.liquid_maxSpeed[elementID]))
                {
                    if (CellBehaviour.TryMoveDownTripleSameChunk(in caller, in x, in y, ref cellID))
                        return;
                }
                else
                {
                    WorldChunk callerNonRef = caller;
                    if (CellBehaviour.TryMoveDownTriple(ref callerNonRef, x, y, ref cellID))
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
            }
        }
        else //this is a liquid like water
        {
            WorldChunk callerNonRef = caller;
            if (CellBehaviour.TryMoveDownTriple(ref callerNonRef, x, y, ref cellID))
                return;

            if (CellBehaviour.TryMoveSide(ref callerNonRef, x, y, ref cellID, ElementManager.liquid_dispersion[elementID]))
            {
                //we should stop moving down so fast.
                Vector2 velocity = callerNonRef.velocity[cellID];
                velocity.Y /= 2;
                callerNonRef.velocity[cellID] = velocity;

                return;
            }

            ref WorldChunk.Moving moving = ref callerNonRef.moving[cellID];
            moving.movingCount++;
            if (moving.movingCount >= ElementManager.FREE_FALLING_THRESHOLD)
            {
                //liquid cells don't actually stop moving, they just reset velocities.
                ref Vector2 velocity = ref callerNonRef.velocity[cellID];
                velocity.X = 0;
                velocity.Y = 0;
                //caller.velocity[cellID] = velocity;
                //caller.moving[cellID] = moving;
            }
        }
    }

    public static void StepGas(WorldChunk caller, int x, int y, int cellID)
    {
        if (caller.chunkRNG.Percent() < 25)
        { //try to move diagonally first
            if (CellBehaviour.TryDiagonalUp(ref caller, x, y, ref cellID))
                return;
            else if (CellBehaviour.TryRise(ref caller, x, y, ref cellID))
                return;
        }
        else
        {
            if (CellBehaviour.TryRise(ref caller, x, y, ref cellID))
                return;
            else if (CellBehaviour.TryDiagonalUp(ref caller, x, y, ref cellID))
                return;
        }

        int elementID = caller.element[cellID];
        if (CellBehaviour.TryMoveSide(ref caller, x, y, ref cellID, ElementManager.liquid_dispersion[elementID]))
            return;
    }
}