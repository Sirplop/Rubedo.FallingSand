using FallingSand.Game.World;
using Rubedo;

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

    public static bool CanBeSwapped(Cell cell, Cell target)
    {
        if (target.IsEmpty)
            return true;
        if (target.element.liquid_isStatic)
            return false;

        return
            (target.element.elementType == Element.Type.LIQUID && CompareDensities(cell, target) && (!cell.element.liquid_isSand || !target.element.liquid_isSand)) ||
            (target.element.elementType == Element.Type.GAS && cell.element.elementType == Element.Type.LIQUID) ||
            (target.element.elementType == Element.Type.GAS && cell.element.elementType == Element.Type.GAS && CompareGasDensities(cell, target));
    }

    private static bool CompareDensities(Cell cell, Cell target)
    {
        return cell.element.density > target.element.density && cell.y >= target.y;
    }
    private static bool CompareGasDensities(Cell cell, Cell target)
    {
        return cell.element.density < target.element.density && cell.y <= target.y;
    }
    private static void SwapForDensities(WorldChunk caller, Cell cell, Cell target)
    {
        cell.yVel *= 0.5f;
        if (Rubedo.Lib.Random.Percent > 80)
        {
            cell.xVel *= -1;
        }
       SwapOrDisplace(caller, cell, target);
    }

    #region LIQUID
    public static bool TryMoveLine(WorldChunk caller, Cell cell)
    {
        int matrixX1 = cell.x;
        int matrixY1 = cell.y;
        int matrixX2 = Rubedo.Lib.Math.CeilToInt(cell.x + cell.xVel);
        int matrixY2 = Rubedo.Lib.Math.CeilToInt(cell.y + cell.yVel);

        if (matrixX1 == matrixX2 && matrixY1 == matrixY2)
            return false; //same position.

        int xDiff = matrixX1 - matrixX2;
        int yDiff = matrixY1 - matrixY2;
        bool xDiffIsLarger = System.Math.Abs(xDiff) > System.Math.Abs(yDiff);

        int xModifier = xDiff < 0 ? 1 : -1;
        int yModifier = yDiff < 0 ? 1 : -1;

        int longerSideLength = System.Math.Max(System.Math.Abs(xDiff), System.Math.Abs(yDiff));
        int shorterSideLength = System.Math.Min(System.Math.Abs(xDiff), System.Math.Abs(yDiff));
        float slope = (shorterSideLength == 0 || longerSideLength == 0) ? 0 : ((float)(shorterSideLength) / (longerSideLength));

        int shorterSideIncrease;
        bool ret = false;
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
            if (caller.InMultiBounds(currentX, currentY))
            {
                if (caller.TryGetCell(currentX, currentY, out Cell target))
                {
                    ActResult actRes = ActOnCell(caller, cell, target);
                    switch (actRes)
                    {
                        case ActResult.Move:
                            ret = true;
                            continue;
                        case ActResult.Reaction:
                        case ActResult.StopMove:
                            return true;
                        case ActResult.Stop:
                            return ret;
                    }
                }
            }
            else
            {
                return ret;
            }
        }
        return ret;
    }

    public static bool TryMoveSide(WorldChunk caller, Cell cell)
    {
        int distance = Rubedo.Lib.Math.RoundAwayFromZero(cell.xVel);
        int dispersion = cell.element.liquid_dispersion;//System.Math.Min(System.Math.Abs(distance), cell.element.liquid_dispersion);

        if (distance > 0)
        {
            ActResult res = CheckSide(caller, cell, dispersion, 1);
            cell.xVel += 1;
            if (res != ActResult.Move && res != ActResult.Reaction)
            {
                cell.xVel *= -1;
            }
            switch (res)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        else if (distance < 0)
        {
            ActResult res = CheckSide(caller, cell, dispersion, -1);
            cell.xVel += -1;
            if (res != ActResult.Move && res != ActResult.Reaction)
            {
                cell.xVel *= -1;
            }
            switch (res)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        else
        {
            int[] order = new int[2];
            bool leftFirst = caller.ChunkRNG.Flip();
            if (leftFirst)
            {
                order[0] = -1;
                order[1] = 1;
            }
            else
            {
                order[0] = 1;
                order[1] = -1;
            }

            for (int i = 0; i < 2; i++)
            {
                ActResult res = CheckSide(caller, cell, dispersion, order[i]);
                if (res != ActResult.Move && res != ActResult.Reaction)
                {
                    cell.xVel *= -1;
                }
                switch (res)
                {
                    case ActResult.Move:
                    case ActResult.Reaction:
                    case ActResult.StopMove:
                        cell.xVel += leftFirst ^ i == 0 ? 1 : -1;
                        return true;
                    case ActResult.Stop:
                        continue;
                }
            }
            return false;
        }
        return false;
    }

    private static ActResult CheckSide(WorldChunk caller, Cell cell, int dispersion, int direction)
    {
        int cellY = cell.y;
        int cellX = cell.x;
        ActResult res = ActResult.Stop;
        for (int i = 1; i <= dispersion; i++)
        {
            if (caller.TryGetCell(cellX + (direction * i), cellY, out Cell target))
            {
                ActResult actRes = ActOnCell(caller, cell, target);
                switch (actRes)
                {
                    case ActResult.Move:
                        res = actRes;
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

    public static bool TryMoveDownTriple(WorldChunk caller, Cell cell)
    {
        bool canMoveDL = caller.TryGetCell(cell.x - 1, cell.y - 1, out Cell downLeft) && CanBeSwapped(cell, downLeft);
        bool canMoveDR = caller.TryGetCell(cell.x + 1, cell.y - 1, out Cell downRight) && CanBeSwapped(cell, downRight);
        bool canMoveD = caller.TryGetCell(cell.x, cell.y - 1, out Cell down) && CanBeSwapped(cell, down);

        if (canMoveD && !((canMoveDR || canMoveDL) && caller.ChunkRNG.Percent() < 25))
        {
            if (TryFall(caller, cell))
                return true;
            else if (canMoveDR || canMoveDL)
            {
                return TryDiagonalDown(caller, cell);
            }
        }
        else if (canMoveDR || canMoveDL)
        {
            if (TryDiagonalDown(caller, cell))
                return true;
            else if (canMoveD)
            {
                return TryFall(caller, cell);
            }
        }
        return false;
    }

    public static bool TryFall(WorldChunk caller, Cell cell)
    {
        float velUpdate = caller.parentMatrix.gravity * cell.element.liquid_gravity * Time.FixedDeltaTime * 2;
        cell.yVel = System.MathF.Min(cell.element.liquid_maxSpeed, cell.yVel - velUpdate);
        int yVel = System.Math.Abs(Rubedo.Lib.Math.RoundAwayFromZero(cell.yVel));

        bool ret = false;
        int cellY = cell.y;
        int cellX = cell.x;
        for (int y = 1; y <= yVel; y++)
        {
            if (caller.TryGetCell(cellX, cellY - y, out Cell target))
            {
                ActResult actRes = ActOnCell(caller, cell, target);
                switch (actRes)
                {
                    case ActResult.Move:
                        ret = true;
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

    public static bool TryDiagonalDown(WorldChunk caller, Cell cell)
    {
        bool downLeftExists = caller.TryGetCell(cell.x - 1, cell.y - 1, out Cell downLeft);
        bool downRightExists = caller.TryGetCell(cell.x + 1, cell.y - 1, out Cell downRight);


        if (downLeftExists && downRightExists)
        {
            Cell[] order = new Cell[2];
            bool leftFirst = caller.ChunkRNG.Flip();
            if (leftFirst)
            {
                order[0] = downLeft;
                order[1] = downRight;
            }
            else
            {
                order[0] = downRight;
                order[1] = downLeft;
            }

            for (int i = 0; i < 2; i++)
            {
                ActResult actRes = ActOnCell(caller, cell, order[i]);
                switch (actRes)
                {
                    case ActResult.Move:
                    case ActResult.Reaction:
                    case ActResult.StopMove:
                        cell.xVel = leftFirst ^ i == 0 ? 1 : -1;
                        //cell.yVel = -1;
                        return true;
                    case ActResult.Stop:
                        continue;
                }
            }
            return false;
        }
        else if (downLeftExists)
        {
            ActResult actRes = ActOnCell(caller, cell, downLeft);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    cell.xVel = -1;
                    //cell.yVel = -1;
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        else if (downRightExists)
        {
            ActResult actRes = ActOnCell(caller, cell, downRight);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    cell.xVel = 1;
                    //cell.yVel = -1;
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
    public static bool TryMoveDown(WorldChunk caller, Cell cell)
    {
        int cellX = cell.x;
        int cellY = cell.y;
        if (caller.TryGetCell(cellX, cellY - 1, out Cell target))
        {
            ActResult actRes = ActOnCell(caller, cell, target);
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

    public static bool TryDiagonalUp(WorldChunk caller, Cell cell)
    {
        bool upLeftExists = caller.TryGetCell(cell.x - 1, cell.y + 1, out Cell upLeft);
        bool upRightExists = caller.TryGetCell(cell.x + 1, cell.y + 1, out Cell upRight);

        Cell[] order = new Cell[2];

        if (upLeftExists && upRightExists)
        {
            bool leftFirst = caller.ChunkRNG.Flip();
            if (leftFirst)
            {
                order[0] = upLeft;
                order[1] = upRight;
            }
            else
            {
                order[0] = upRight;
                order[1] = upLeft;
            }

            for (int i = 0; i < 2; i++)
            {
                ActResult actRes = ActOnCell(caller, cell, order[i]);
                switch (actRes)
                {
                    case ActResult.Move:
                    case ActResult.Reaction:
                    case ActResult.StopMove:
                        cell.xVel = leftFirst ^ i == 0 ? 1 : -1;
                        //cell.yVel = -1;
                        return true;
                    case ActResult.Stop:
                        continue;
                }
            }
            return false;
        }
        else if (upLeftExists)
        {
            ActResult actRes = ActOnCell(caller, cell, upLeft);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    cell.xVel = -1;
                    //cell.yVel = -1;
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        else if (upRightExists)
        {
            ActResult actRes = ActOnCell(caller, cell, upRight);
            switch (actRes)
            {
                case ActResult.Move:
                case ActResult.Reaction:
                case ActResult.StopMove:
                    cell.xVel = 1;
                    //cell.yVel = -1;
                    return true;
                case ActResult.Stop:
                    return false;
            }
        }
        return false;
    }

    public static bool TryRise(WorldChunk caller, Cell cell)
    {
        int cellX = cell.x;
        int cellY = cell.y;
        if (caller.TryGetCell(cellX, cellY + 1, out Cell target))
        {
            ActResult actRes = ActOnCell(caller, cell, target);
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

    public static void SwapOrDisplace(WorldChunk caller, Cell cell, Cell target)
    {
        if (!target.IsEmpty)
            cell.Displace(caller, target);
        else
            cell.SwapPositions(caller, target);
    }
    public static void SetNeighborsFreefalling(WorldChunk caller, int x, int y)
    {
        caller.SetFreeFalling(caller, x + 1, y);
        caller.SetFreeFalling(caller, x - 1, y);
    }

    public static void ConvertVerticalToHorizontalMotion(Cell cell)
    {
        float absY = System.Math.Abs(cell.yVel);
        cell.xVel = cell.xVel > 0 ? absY : -absY;
        //cell.yVel = 0;
    }

    /// <summary>
    /// Handles reactions and what happens when cells run into eachother.
    /// </summary>
    public static ActResult ActOnCell(WorldChunk caller, Cell actor, Cell target)
    {
        bool reacted = React(actor, target);
        if (reacted)
        {
            return ActResult.Reaction;
        }

        if (target.IsEmpty)
        {
            actor.SwapPositions(caller, target);
            actor.SetFreeFalling(true);
            return ActResult.Move; //it wasn't stopped.
        }

        switch (target.element.elementType)
        {
            case Element.Type.PHYSICS_SOLID:
            {
                break;
            }
            case Element.Type.LIQUID:
            {
                if (actor.freeFalling) //we've hit something solid
                {
                    ConvertVerticalToHorizontalMotion(actor);
                }
                if (target.element.liquid_isSand)
                {
                    if (CanBeSwapped(actor, target))
                    {
                        actor.SwapPositions(caller, target);
                        return ActResult.StopMove;
                    }
                    return ActResult.Stop;
                }
                else
                {
                    if (CanBeSwapped(actor, target))
                    {
                        SwapForDensities(caller, actor, target);
                        if (actor.element.elementType == Element.Type.LIQUID && !actor.element.liquid_isSand)
                        {
                            return ActResult.Move; //fluids can move fast through other fluids.
                        }
                        return ActResult.StopMove;
                    }
                    return ActResult.Stop;
                }
            }
               
            case Element.Type.GAS:
            {
                if (CanBeSwapped(actor, target))
                {
                    SwapForDensities(caller, actor, target);
                    return ActResult.StopMove;
                }
                return ActResult.Stop;
            }
        }
        return ActResult.Stop; //something unknown?
    }


    public static bool React(Cell cell1, Cell cell2)
    {
        ReactionKey key;
        if (cell2.IsEmpty)
        {
            key = new ReactionKey() { cellType1 = cell1.element.internalName, cellType2 = "air" };
        }
        else
        {
            key = new ReactionKey() { cellType1 = cell1.element.internalName, cellType2 = cell2.element.internalName };
        }
        if (ElementManager.reactions.TryGetValue(key, out Reaction reaction))
        {
            //TODO: Try to react
            return true;
        }
        return false;
    }
}