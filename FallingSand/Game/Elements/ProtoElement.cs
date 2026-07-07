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

    public bool def_liquid_isStatic = false;
    public bool def_liquid_isSand = false;

    public bool def_liquid_maxSpeed = false;
    public bool def_liquid_gravity = false;
    public bool def_liquid_dispersion = false;
    public bool def_liquid_inertialResistance = false;
    public bool def_liquid_friction = false;

    public FinishedElement Finish()
    {
        FinishedElement element = new FinishedElement();
        element.internalName = this.internalName;
        element.elementType = elementType;

        element.tags = tags;
        element.color = color;
        element.density = density;
        element.liquid_isStatic = liquid_isStatic;
        element.liquid_isSand = liquid_isSand;
        element.liquid_maxSpeed = liquid_maxSpeed;
        element.liquid_gravity = liquid_gravity;
        element.liquid_dispersion = liquid_dispersion;
        element.liquid_inertialResistance = liquid_inertialResistance;
        element.liquid_friction = liquid_friction;

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

        element.color = color;

        element.density = def_density ? density : parentElement.density; ;
        element.liquid_isStatic = def_liquid_isStatic ? liquid_isStatic : parentElement.liquid_isStatic;
        element.liquid_isSand = def_liquid_isSand ? liquid_isSand : parentElement.liquid_isSand;
        element.liquid_maxSpeed = def_liquid_maxSpeed ? liquid_maxSpeed : parentElement.liquid_maxSpeed;
        element.liquid_gravity = def_liquid_gravity ? liquid_gravity : parentElement.liquid_gravity;
        element.liquid_dispersion = def_liquid_dispersion ? liquid_dispersion : parentElement.liquid_dispersion;
        element.liquid_inertialResistance = def_liquid_inertialResistance ? liquid_inertialResistance : parentElement.liquid_inertialResistance;
        element.liquid_friction = def_liquid_friction ? liquid_friction : parentElement.liquid_friction;

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
