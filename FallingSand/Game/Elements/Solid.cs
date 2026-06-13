using FallingSand.Game.World;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Solid, and I don't have a summary yet.
/// </summary>
public class Solid : Element
{
    public Solid(string name)
    {
        this.elementType = Type.PHYSICS_SOLID;
        this.internalName = name;
    }

    public override void Step(WorldChunk caller, Cell cell)
    {
        throw new System.NotImplementedException();
    }
}