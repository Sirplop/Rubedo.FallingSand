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
    public Color colorCode; //material color code
    public float density = 5;
    public float hp = 5;
    public int hardness = 5;

    public Color color; //actual color of cells
    public string textureTarget = ""; //draw from a texture?
    public bool isGradient = false; //is the texture a gradient texture?

    public bool liquid_isStatic = false; //does this particle move
    public bool liquid_isSand = false; //is this particle a powder, aka only moves downwards?

    public byte liquid_maxSpeed = 5; //how fast can a single pixel go?
    public byte liquid_gravity = 1; //vertical acceleration rate
    public byte liquid_dispersion = 5; //how far the particle looks left and right to move to the side
    public byte liquid_inertialResistance = 50; //[0, 100] how likely is this element to become freefalling when something passes by?
    public byte liquid_friction = 40; //how fast does this element slow down?

    public byte fire_temperature = 0;           // 0 is fireproof. Difference between fire and fuel's value determines spread speed.
    public float fire_burnTime = 0;              // seconds until this fuel is consumed or the fire fizzles
    public bool fire_requiresAir = true;           // does this material require air to burn?

    public string fire_fizzle = "";                // what a fire cell becomes at end of life
}