using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using Rubedo.Lib;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Element, and I don't have a summary yet.
/// </summary>
public abstract class Element
{
    public enum Type
    {
        PHYSICS_SOLID,
        LIQUID,
        GAS
    }

    public int element_id; //assigned by the system on element load, for quicker reaction lookups.

    public string[] tags;
    public Type elementType;
    public string internalName; //internal name
    public Color color;
    public float density = 5;

    public bool liquid_isStatic = false; //does this particle move
    public bool liquid_isSand = false; //is this particle a powder, aka only moves downwards?

    public int liquid_maxSpeed = 5; //how fast can a single pixel go?
    public float liquid_gravity = 1f; //vertical acceleration rate
    public int liquid_dispersion = 5; //how far the particle looks left and right to move to the side
    public int liquid_inertialResistance = 50; //[0, 100] how likely is this element to become freefalling when something passes by?
    public float liquid_friction = 0.4f; //how fast does this element slow down?

    public abstract void Step(WorldChunk caller, Cell cell);

   
}