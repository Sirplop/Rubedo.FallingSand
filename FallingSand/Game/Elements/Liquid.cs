using FallingSand.Game.World;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Liquid, and I don't have a summary yet.
/// </summary>
public class Liquid : Element
{
    public bool isStatic = false; //does this particle move
    public bool isSand = false; //is this particle a powder, aka only moves downwards?

    public int dispersion = 1; //how far the particle looks left and right to move to the side
    public int inertialResistance = 50; //[0, 100] how likely is this element to become freefalling when something passes by?


    public override void Step(SandMatrix matrix, Cell cell)
    {
        if (!isStatic)
        {
            if (isSand)
            {
                if (CellBehaviour.MoveDown(matrix, cell))
                    return;
                else if (CellBehaviour.MoveDownDiagonal(matrix, cell))
                    return;
                // CellBehaviour.MoveDown3Dir(matrix, cell);
            }
            else
            {

                if (CellBehaviour.MoveDown(matrix, cell))
                    return;
                else if (CellBehaviour.MoveDownDiagonal(matrix, cell))
                    return;
                if (CellBehaviour.MoveSide(matrix, cell, dispersion))
                    return;
            }
        }
    }
}