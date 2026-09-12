using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using Rubedo;
using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using static FallingSand.Game.World.WorldChunk;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FallingSand.Game.Elements;
public static class ElementBehaviour
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotBorderCellPowder(in int x, in int y, in int chunkSize, in int max_speed)
    {
        //powder can move left and right 1, but a large down distance.
        return x > 1 && x < chunkSize - 2 && y >= max_speed && y < chunkSize - 2;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotBorderCellLiquid(in int x, in int y, in int chunkSize, in int max_speed, in int dispersion)
    {
        //liquid cares about being able to move a large horizontal and vertical distance, but not up.
        return x > dispersion + 1 && x < chunkSize - dispersion - 1 && y >= max_speed && y < chunkSize - 2;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotBorderCellGas(in int x, in int y, in int chunkSize)
    {
        //gas can move up, left, and right by 1, so it does not care about beneath itself.
        return x > 1 && x < chunkSize - 2 && y < chunkSize - 1 && y > 0;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNotBorderCellFire(in int x, in int y, in int chunkSize)
    {
        //fire cares about its immediate halo.
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
            if (CellBehaviour.FireIsExtinguishedSameChunk(in caller, in fireType, in x, in y))
            {
                FireBuried(in caller, in cellID);
                return;
            }

            CellBehaviour.UpdateFireIntensitySameChunk(in caller, in x, in y, in cellID, in fireType);
            byte intensity = caller.burningIntensity[cellID];

            int decayThreshold = ElementManager.FIRE_DECAY_THRESHOLD
                               + intensity * ElementManager.FIRE_DECAY_THRESHOLD_INTENSITY;
            if (caller.chunkRNG.Percent() > decayThreshold)
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

            //apply a stain to the 4 cardinal cells

            const float DARKEN_STRENGTH = 0.001f;
            Color stain = ElementManager.stainColor[elementID];

            int upCell = caller.GetCellIndex(x, y + 1);
            int leftCell = caller.GetCellIndex(x - 1, y);
            int rightCell = caller.GetCellIndex(x + 1, y);
            int downCell = caller.GetCellIndex(x, y - 1);

            if (ElementManager.canBeStained[caller.element[upCell]])
                caller.ApplyStain(in upCell, stain, DARKEN_STRENGTH);
            if (ElementManager.canBeStained[caller.element[leftCell]])
                caller.ApplyStain(in leftCell, stain, DARKEN_STRENGTH);
            if (ElementManager.canBeStained[caller.element[rightCell]])
                caller.ApplyStain(in rightCell, stain, DARKEN_STRENGTH);
            if (ElementManager.canBeStained[caller.element[downCell]])
                caller.ApplyStain(in downCell, stain, DARKEN_STRENGTH);

            CellBehaviour.TryIgniteNeighborsSameChunk(in caller, in x, in y, in fireType, in intensity);

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
            if (CellBehaviour.TryMoveSideOneSameChunk(in caller, in x, in y, ref cellID))
                return;

            //only try to spawn more fire if this didn't move
            //if (caller.chunkRNG.Percent() < ElementManager.FIRE_SPAWN_CHANCE)
            //    CellBehaviour.TrySpawnFlameAroundSameChunk(caller, in x, in y, fireType);
            caller.ThreadEnvelop(cellID);
        }
        else
        {
            if (CellBehaviour.FireIsExtinguished(in caller, in elementID, in x, in y))
            {
                FireBuried(in caller, in cellID);
                return;
            }

            CellBehaviour.UpdateFireIntensity(in caller, in x, in y, in cellID, in fireType);
            byte intensity = caller.burningIntensity[cellID];

            int decayThreshold = ElementManager.FIRE_DECAY_THRESHOLD
                               + intensity * ElementManager.FIRE_DECAY_THRESHOLD_INTENSITY;
            if (caller.chunkRNG.Percent() > decayThreshold)
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

            const float DARKEN_STRENGTH = 0.001f;
            Color stain = ElementManager.stainColor[fireType];

            if (caller.TryGetCell(x, y + 1, out WorldChunk containing, out int stainCell) && ElementManager.canBeStained[containing.element[stainCell]])
                caller.ApplyStain(in stainCell, stain, DARKEN_STRENGTH);
            if (caller.TryGetCell(x - 1, y, out containing, out stainCell) && ElementManager.canBeStained[containing.element[stainCell]])
                caller.ApplyStain(in stainCell, stain, DARKEN_STRENGTH);
            if (caller.TryGetCell(x + 1, y, out containing, out stainCell) && ElementManager.canBeStained[containing.element[stainCell]])
                caller.ApplyStain(in stainCell, stain, DARKEN_STRENGTH);
            if (caller.TryGetCell(x, y - 1, out containing, out stainCell) && ElementManager.canBeStained[containing.element[stainCell]])
                caller.ApplyStain(in stainCell, stain, DARKEN_STRENGTH);

            CellBehaviour.TryIgniteNeighbors(in caller, in x, in y, in fireType, in intensity);

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
            if (CellBehaviour.TryMoveSideOne(ref callerNonRef, in x, in y, ref cellID))
                return;

            //only try to spawn more fire if this didn't move
            //if (caller.chunkRNG.Percent() < ElementManager.FIRE_SPAWN_CHANCE)
            //    CellBehaviour.TrySpawnFlameAround(in caller, in x, in y, fireType);
            caller.ThreadEnvelop(cellID);
        }
    }

    public static void StepBurning(in WorldChunk caller, in int x, in int y, int cellID, in int elementID)
    {
        caller.ThreadEnvelop(cellID);
        int fireType = caller.burnFireType[cellID];

        if (ElementManager.fire_requiresAir[elementID] && 
            CellBehaviour.FireIsExtinguished(in caller, in fireType, in x, in y))
        {
            BurningBuried(in caller, in cellID);
            return;
        }

        float hp = caller.hp[cellID].Value - Time.FixedDeltaTime;
        if (hp <= 0)
        {
            // TODO: burn-down result becomes a reaction lookup between elementID and
            // caller.burnFireType[cellID] once that's wired up. Placeholder for now:
            caller.element[cellID] = ElementManager.EMPTY;
            caller.color[cellID] = ElementManager.colorCode[ElementManager.EMPTY];
            caller.stain[cellID] = ShortColor.Clear;
            caller.hp[cellID].Zero();
            caller.lifetime[cellID] = 0;
            caller.burnFireType[cellID] = ElementManager.EMPTY;
            caller.burningIntensity[cellID] = 0;
            return;
        }
        caller.hp[cellID].Value = hp;

        //only powders get darkened.
        if (ElementManager.liquid_isSand[elementID])
        {
            const float TIME_TO_DARKEN = 1f / 0.5f; //should reach max darkening after burning half the max hp

            float baseHP = ElementManager.hp[elementID];

            float strength = (1f / baseHP) * Time.FixedDeltaTime * TIME_TO_DARKEN;

            caller.ApplyStain(in cellID, ElementManager.stainColor[fireType], strength);
        }

        if (IsNotBorderCellFire(x - caller.chunkX, y - caller.chunkY, in caller.size))
        {
            byte intensity = caller.burningIntensity[cellID];

            CellBehaviour.UpdateFireIntensitySameChunk(in caller, in x, in y, in cellID, in fireType);
            CellBehaviour.TryIgniteNeighborsSameChunk(in caller, in x, in y, in fireType, in intensity);

            if (caller.chunkRNG.Percent() < ElementManager.FIRE_SPAWN_CHANCE)
                CellBehaviour.TrySpawnFlameAroundSameChunk(in caller, in x, in y, in fireType, in intensity);
        }
        else
        {
            byte intensity = caller.burningIntensity[cellID];

            CellBehaviour.UpdateFireIntensity(in caller, in x, in y, in cellID, in fireType);
            CellBehaviour.TryIgniteNeighbors(in caller, in x, in y, in fireType, in intensity);

            if (caller.chunkRNG.Percent() < ElementManager.FIRE_SPAWN_CHANCE)
                CellBehaviour.TrySpawnFlameAround(in caller, in x, in y, in fireType, in intensity);
        }
    }

    private static void FireFizzle(in WorldChunk caller, in int cellID, in int fireType)
    {
        if (caller.chunkRNG.Percent() < ElementManager.FIRE_FIZZLE_CHANCE)
        {
            int result = ElementManager.fire_fizzle[fireType];
            caller.element[cellID] = result;
            caller.color[cellID] = ElementManager.GetNewCellColor(result, ref caller.chunkRNG);
            caller.stain[cellID] = ShortColor.Clear;
            caller.hp[cellID].Value = ElementManager.hp[result];
            caller.lifetime[cellID] = ElementManager.lifetime[result];
            caller.burnFireType[cellID] = ElementManager.EMPTY;
            caller.burningIntensity[cellID] = 0;
            caller.ThreadEnvelop(cellID);
        }
        else
        {
            caller.element[cellID] = ElementManager.EMPTY;
            caller.color[cellID] = ElementManager.colorCode[ElementManager.EMPTY];
            caller.stain[cellID] = ShortColor.Clear;
            caller.hp[cellID].Zero();
            caller.lifetime[cellID] = 0;
            caller.burnFireType[cellID] = ElementManager.EMPTY;
            caller.burningIntensity[cellID] = 0;
        }
    }
    private static void FireBuried(in WorldChunk caller, in int cellID)
    {
        caller.element[cellID] = ElementManager.EMPTY;
        caller.color[cellID] = Color.Transparent;
        caller.hp[cellID].Zero();
        caller.lifetime[cellID] = 0;
        caller.burnFireType[cellID] = ElementManager.EMPTY;
        caller.burningIntensity[cellID] = 0;
        caller.velocity[cellID].Zero();
        caller.moving[cellID] = new WorldChunk.Moving();
    }

    private static void BurningBuried(in WorldChunk caller, in int cellID)
    {
        caller.burnFireType[cellID] = ElementManager.EMPTY;
        caller.burningIntensity[cellID] = 0;
    }
}