using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using Rubedo.Lib;
using System.Collections.Generic;

namespace FallingSand.Game.Elements;

/// <summary>
/// Finalized element on loading.
/// </summary>
public class FinishedElement
{
    public Dictionary<ReactionKey, ReactionValue> reactions = new Dictionary<ReactionKey, ReactionValue>();

    public int element_id; //assigned by the system on element load, for quicker reaction lookups.

    public string[] tags;
    public ElementManager.Type elementType;
    public string internalName; //internal name
    public Color color;
    public float density = 5;

    public bool liquid_isStatic = false; //does this particle move
    public bool liquid_isSand = false; //is this particle a powder, aka only moves downwards?

    public byte liquid_maxSpeed = 5; //how fast can a single pixel go?
    public byte liquid_gravity = 1; //vertical acceleration rate
    public byte liquid_dispersion = 5; //how far the particle looks left and right to move to the side
    public byte liquid_inertialResistance = 50; //[0, 100] how likely is this element to become freefalling when something passes by?
    public byte liquid_friction = 40; //how fast does this element slow down?
}