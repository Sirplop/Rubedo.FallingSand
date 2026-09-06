using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using Rubedo;
using System.Runtime.CompilerServices;
using static FallingSand.Game.World.WorldChunk;

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotBorderCellFire(in int x, in int y, in int chunkSize)
    {
        return x > 1 && x < chunkSize - 2 && y > 1 && y < chunkSize - 2;
    }

    public static void StepLiquid(in WorldChunk caller, in int x, in int y, int cellID, in int elementID)
    {
        if (ElementManager.liquid_isStatic[elementID])
            return;

        if (ElementManager.liquid_isSand[elementID])
        {
            WorldChunk.Moving moving = caller.moving[cellID];
            if (moving.IsMoving)
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
                    if (CellBehaviour.TryMoveDownTriple(ref callerNonRef, in x, in y, ref cellID))
                        return;
                    if (CellBehaviour.TryReactDirection(ref callerNonRef, in x, in y, in cellID, in elementID, 1))
                        return;
                }

                ref WorldChunk.Moving move = ref caller.moving[cellID];
                moving.MovingCount++;
                if (moving.MovingCount >= ElementManager.FREE_FALLING_THRESHOLD)
                {
                    moving.IsMoving = false; //failed to move.
                    ref Velocity velocity = ref caller.velocity[cellID];
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
                    Velocity velocity = caller.velocity[cellID];
                    velocity.Y /= 2;
                    caller.velocity[cellID] = velocity;

                    return;
                }
                if (CellBehaviour.TryReactDirectionSameChunk(in caller, in x, in y, in cellID, in elementID, 1))
                    return;

                ref WorldChunk.Moving moving = ref caller.moving[cellID];
                moving.MovingCount++;
                if (moving.MovingCount >= ElementManager.FREE_FALLING_THRESHOLD)
                {
                    //liquid cells don't actually stop moving, they just reset velocities.
                    ref Velocity velocity = ref caller.velocity[cellID];
                    moving.MovingCount = 0;
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
                    Velocity velocity = callerNonRef.velocity[cellID];
                    velocity.Y /= 2;
                    callerNonRef.velocity[cellID] = velocity;

                    return;
                }
                if (CellBehaviour.TryReactDirection(ref callerNonRef, in x, in y, in cellID, in elementID, 1))
                    return;

                ref WorldChunk.Moving moving = ref callerNonRef.moving[cellID];
                moving.MovingCount++;
                if (moving.MovingCount >= ElementManager.FREE_FALLING_THRESHOLD)
                {
                    //liquid cells don't actually stop moving, they just reset velocities.
                    ref Velocity velocity = ref callerNonRef.velocity[cellID];
                    moving.MovingCount = 0;
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
                else if (CellBehaviour.TryDiagonalUp(ref callerNonRef, in x, in y, ref cellID))
                    return;
                else if (CellBehaviour.TryReactDirection(ref callerNonRef, in x, in y, in cellID, in elementID, 1))
                    return;
            }

            if (CellBehaviour.TryMoveSideOne(ref callerNonRef, in x, in y, ref cellID))
                return;
        }
    }

    public static void StepFire(in WorldChunk caller, in int x, in int y, int cellID, in int elementID)
    {
        int fireType = elementID;

        if (IsNotBorderCellFire(x - caller.chunkX, y - caller.chunkY, in caller.size))
        {
            if (CellBehaviour.FireIsExtinguishedSameChunk(caller, x, y))
            {
                FireBuried(in caller, in cellID);
                return;
            }


            if (caller.chunkRNG.Percent() > 25)
            {
                float life = caller.hp[cellID];
                life -= Time.FixedDeltaTime;
                if (life <= 0)
                {
                    FireFizzle(in caller, in cellID, in fireType);
                    return;
                }
                caller.hp[cellID] = life;
            }

            CellBehaviour.TryIgniteNeighborsSameChunk(in caller, in x, in y, fireType);

            if (caller.chunkRNG.Flip())
            {
                if (CellBehaviour.TryDiagonalUpSameChunk(in caller, in x, in y, ref cellID))
                    return;
                if (CellBehaviour.TryRiseSameChunk(in caller, in x, in y, ref cellID))
                    return;
            }
            else
            {
                if (CellBehaviour.TryRiseSameChunk(in caller, in x, in y, ref cellID))
                    return;
                if (CellBehaviour.TryDiagonalUpSameChunk(in caller, in x, in y, ref cellID))
                    return;
            }
            //if (CellBehaviour.TryMoveSideOneSameChunk(in caller, in x, in y, ref cellID))
            //    return;

            //only try to spawn more fire if this didn't move
            //if (caller.chunkRNG.Percent() < ElementManager.FIRE_SPAWN_CHANCE)
            //    CellBehaviour.TrySpawnFlameAroundSameChunk(caller, in x, in y, fireType);
            caller.ThreadEnvelop(cellID);
        }
        else
        {
            if (CellBehaviour.FireIsExtinguished(caller, x, y))
            {
                FireBuried(in caller, in cellID);
                return;
            }

            if (caller.chunkRNG.Percent() > 25)
            {
                float life = caller.hp[cellID];
                life -= Time.FixedDeltaTime;
                if (life <= 0)
                {
                    FireFizzle(in caller, in cellID, in fireType);
                    return;
                }
                caller.hp[cellID] = life;
            }

            CellBehaviour.TryIgniteNeighbors(in caller, in x, in y, fireType);

            WorldChunk callerNonRef = caller;

            if (caller.chunkRNG.Flip())
            {
                if (CellBehaviour.TryDiagonalUp(ref callerNonRef, in x, in y, ref cellID))
                    return;
                if (CellBehaviour.TryRise(ref callerNonRef, in x, in y, ref cellID))
                    return;
            }
            else
            {
                if (CellBehaviour.TryRise(ref callerNonRef, in x, in y, ref cellID))
                    return;
                if (CellBehaviour.TryDiagonalUp(ref callerNonRef, in x, in y, ref cellID))
                    return;
            }
            //if (CellBehaviour.TryMoveSideOne(ref callerNonRef, in x, in y, ref cellID))
            //    return;

            //only try to spawn more fire if this didn't move
            //if (caller.chunkRNG.Percent() < ElementManager.FIRE_SPAWN_CHANCE)
            //    CellBehaviour.TrySpawnFlameAround(in caller, in x, in y, fireType);
            caller.ThreadEnvelop(cellID);
        }
    }

    public static void StepBurning(in WorldChunk caller, in int x, in int y, int cellID, in int elementID)
    {
        caller.ThreadEnvelop(cellID);
        if (ElementManager.fire_requiresAir[elementID] && CellBehaviour.FireIsExtinguished(caller, x, y))
        {
            BurningBuried(in caller, in cellID);
            return;
        }

        float timer = caller.hp[cellID].Value - Time.FixedDeltaTime;
        if (timer <= 0)
        {
            // TODO: burn-down result becomes a reaction lookup between elementID and
            // caller.burnFireType[cellID] once that's wired up. Placeholder for now:
            caller.element[cellID] = ElementManager.EMPTY;
            caller.color[cellID] = ElementManager.colorCode[ElementManager.EMPTY];
            caller.hp[cellID].Zero();
            caller.burnFireType[cellID] = ElementManager.EMPTY;
            return;
        }
        caller.hp[cellID].Value = timer;

        /*
        Color c = caller.color[cellID];
        float d = 1f / ElementManager.fire_burnTime[elementID];
        caller.color[cellID] = new Color((byte)(c.R * d), (byte)(c.G * d), (byte)(c.B * d), c.A);
        */

        if (IsNotBorderCellFire(in x, in y, in caller.size))
        {
            int fireType = caller.burnFireType[cellID];
            CellBehaviour.TryIgniteNeighborsSameChunk(in caller, in x, in y, in fireType);

            if (caller.chunkRNG.Percent() < ElementManager.FIRE_SPAWN_CHANCE)
                CellBehaviour.TrySpawnFlameAroundSameChunk(in caller, in x, in y, in fireType);
        }
        else
        {
            int fireType = caller.burnFireType[cellID];
            CellBehaviour.TryIgniteNeighbors(in caller, in x, in y, in fireType);

            if (caller.chunkRNG.Percent() < ElementManager.FIRE_SPAWN_CHANCE)
                CellBehaviour.TrySpawnFlameAround(in caller, in x, in y, in fireType);
        }
    }

    private static void FireFizzle(in WorldChunk caller, in int cellID, in int fireType)
    {
        int result = ElementManager.fire_fizzle[fireType];
        caller.element[cellID] = result;
        caller.color[cellID] = ElementManager.color[result] * caller.chunkRNG.Range(0.9f, 1.1f);
        caller.hp[cellID].Value = ElementManager.hp[result];
        caller.burnFireType[cellID] = ElementManager.EMPTY;
        caller.ThreadEnvelop(cellID);
    }
    private static void FireBuried(in WorldChunk caller, in int cellID)
    {
        caller.element[cellID] = ElementManager.EMPTY;
        caller.color[cellID] = Color.Transparent;
        caller.hp[cellID].Zero();
        caller.burnFireType[cellID] = ElementManager.EMPTY;
        caller.velocity[cellID].Zero();
        caller.moving[cellID] = new WorldChunk.Moving();
    }

    private static void BurningBuried(in WorldChunk caller, in int cellID)
    {
        caller.burnFireType[cellID] = ElementManager.EMPTY;
    }
}