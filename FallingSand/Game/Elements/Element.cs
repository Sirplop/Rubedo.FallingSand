using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using Rubedo.Lib;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Element, and I don't have a summary yet.
/// </summary>
public abstract class Element
{
    public Color color;
    public float density;

    public abstract void Step(SandMatrix matrix, Cell cell);

   
}