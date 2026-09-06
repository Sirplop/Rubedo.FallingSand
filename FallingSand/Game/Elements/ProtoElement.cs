using Loyc;
using Rubedo.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FallingSand.Game.Elements;
public class ProtoElement : FinishedElement
{
    public string parent;
    public bool inheritReactions = false;

    public bool finishedConstruction = false;

    public bool def_parent = false;
    public bool def_inheritReactions = false;
    public bool def_tags = false;
    public bool def_elementType = false;
    public bool def_density = false;
    public bool def_hp = false;
    public bool def_hardness = false;

    public bool def_color = false;
    public bool def_texture = false;
    public bool def_isGradient = false;

    public bool def_liquid_isStatic = false;
    public bool def_liquid_isSand = false;

    public bool def_liquid_maxSpeed = false;
    public bool def_liquid_gravity = false;
    public bool def_liquid_dispersion = false;
    public bool def_liquid_inertialResistance = false;
    public bool def_liquid_friction = false;

    public bool def_fire_temperature = false;
    public bool def_fire_requires_air = false;
    public bool def_fire_fizzles = false;

    public FinishedElement Finish()
    {
        FinishedElement element = new FinishedElement();
        element.internalName = this.internalName;
        element.elementType = elementType;

        element.tags = tags;
        element.colorCode = colorCode;
        element.density = density;
        element.hp = hp;
        element.hardness = hardness;
        element.color = def_color ? color : colorCode;
        element.textureTarget = textureTarget;
        element.isGradient = isGradient;
        element.liquid_isStatic = liquid_isStatic;
        element.liquid_isSand = liquid_isSand;
        element.liquid_maxSpeed = liquid_maxSpeed;
        element.liquid_gravity = liquid_gravity;
        element.liquid_dispersion = liquid_dispersion;
        element.liquid_inertialResistance = liquid_inertialResistance;
        element.liquid_friction = liquid_friction;
        element.fire_temperature = fire_temperature;
        element.fire_burnTime = fire_burnTime;
        element.fire_requiresAir = fire_requiresAir;
        element.fire_fizzle = fire_fizzle;

        foreach (var react in reactions)
        {
            if (!element.reactions.ContainsKey(react.Key))
                element.reactions.Add(react.Key, react.Value);
        }

        finishedConstruction = true;

        return element;
    }

    public FinishedElement Finish(ProtoElement parentElement)
    {
        FinishedElement element = new FinishedElement();
        element.internalName = this.internalName;
        element.elementType = def_elementType ? elementType : parentElement.elementType;

        int tagCount = 0;
        if (def_tags)
        {
            tagCount += tags.Length;
        }
        if (parentElement.def_tags)
        {
            tagCount += parentElement.tags.Length;
        }

        element.tags = new string[tagCount];
        int i = 0;
        if (def_tags)
        {
            for (int x = 0; x < tags.Length; x++)
            {
                element.tags[i++] = tags[x];
            }
        }
        if (parentElement.def_tags)
        {
            for (int x = 0; x < parentElement.tags.Length; x++)
            {
                element.tags[i++] = parentElement.tags[x];
            }
        }

        element.colorCode = colorCode;
        
        element.color = def_color ? color : parentElement.color;
        element.textureTarget = def_texture ? textureTarget : parentElement.textureTarget;
        element.isGradient = def_isGradient ? isGradient : parentElement.isGradient;

        element.density = def_density ? density : parentElement.density;
        element.hp = def_hp ? hp : parentElement.hp;
        element.hardness = def_hardness ? hardness : parentElement.hardness;
        element.liquid_isStatic = def_liquid_isStatic ? liquid_isStatic : parentElement.liquid_isStatic;
        element.liquid_isSand = def_liquid_isSand ? liquid_isSand : parentElement.liquid_isSand;
        element.liquid_maxSpeed = def_liquid_maxSpeed ? liquid_maxSpeed : parentElement.liquid_maxSpeed;
        element.liquid_gravity = def_liquid_gravity ? liquid_gravity : parentElement.liquid_gravity;
        element.liquid_dispersion = def_liquid_dispersion ? liquid_dispersion : parentElement.liquid_dispersion;
        element.liquid_inertialResistance = def_liquid_inertialResistance ? liquid_inertialResistance : parentElement.liquid_inertialResistance;
        element.liquid_friction = def_liquid_friction ? liquid_friction : parentElement.liquid_friction;
        element.fire_temperature = def_fire_temperature ? fire_temperature : parentElement.fire_temperature;
        element.fire_requiresAir = def_fire_requires_air ? fire_requiresAir : parentElement.fire_requiresAir;
        element.fire_fizzle = def_fire_fizzles ? fire_fizzle : parentElement.fire_fizzle;

        foreach (var react in reactions)
        {
            if (!element.reactions.ContainsKey(react.Key))
                element.reactions.Add(react.Key, react.Value);
        }
        if (inheritReactions)
        {
            foreach (var react in parentElement.reactions)
            {
                ReactionKey key = react.Key;
                if (key.cellType1 == parentElement.internalName)
                {
                    key.cellType1 = element.internalName;
                }
                if (key.cellType2 == parentElement.internalName)
                {
                    key.cellType2 = element.internalName;
                }
                if (!element.reactions.ContainsKey(key))
                    element.reactions.Add(key, react.Value);
            }
        }

        finishedConstruction = true;

        return element;
    }
}
