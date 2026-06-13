using Microsoft.Xna.Framework.Content;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.Json;
using FallingSand.Game.World;
using System.Xml.Linq;
using Rubedo.Lib;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am ElementSerializer, and I don't have a summary yet.
/// </summary>
public static class ElementLoader
{
    public static List<Element> PopulateElements(List<ProtoElement> elements)
    {
        List<Element> result = new List<Element>();

        //create a dictionary for quick lookup.
        Dictionary<string, ProtoElement> nameToElement = new Dictionary<string, ProtoElement>();
        foreach (ProtoElement element in elements)
        {
            nameToElement.Add(element.internalName, element);
        }

        HashSet<ProtoElement> workingElements = new HashSet<ProtoElement>(elements);
        HashSet<ProtoElement> removeFromWorking = new HashSet<ProtoElement>();

        const int CUTOFF = 50;
        int iteration = 0;

        //this is pretty bad, it would be significantly better as a dependency tree.
        while (workingElements.Count > 0)
        {
            iteration++;
            if (iteration >= CUTOFF) //oops we have a cycle or something
            {
                throw new ContentLoadException($"Got stuck loading elements! Here's what was left in working: {workingElements.Print()}");
            }

            foreach (ProtoElement element in elements)
            {
                //Does this thing inherit from some other base?
                if (element.def_parent)
                {

                    if (nameToElement.TryGetValue(element.parent, out ProtoElement parent))
                    {
                        if (!parent.finishedConstruction)
                        {
                            continue; //we haven't constructed this one yet, wait.
                        }

                        result.Add(element.Finish(parent));
                        removeFromWorking.Add(element);
                    }
                    else
                    {
                        throw new ContentLoadException($"Element '{element.parent}' does not exist, which {element.internalName} tries to inherit from!");
                    }
                }
                else
                {
                    //assemble and remove from working
                    result.Add(element.Finish());
                    removeFromWorking.Add(element);
                }
            }

            foreach (ProtoElement element in removeFromWorking)
            {
                workingElements.Remove(element);
            }
            removeFromWorking.Clear();
        }

        //TODO: Reactions

        return result;
    }

    public static List<ProtoElement> LoadProtoElements(string[] paths)
    {
        List<ProtoElement> elements = new List<ProtoElement>();
        for (int i = 0; i < paths.Length; i++)
        {
            ProtoElement[] protos = LoadElementFile(paths[i]);
            elements.AddRange(protos);
        }
        return elements;
    }

    private static ProtoElement[] LoadElementFile(string path)
    {
        FileInfo elementFile = new FileInfo(path);

        if (!elementFile.Exists)
        {
            throw new ContentLoadException($"Element '{path}' does not exist!");
        }

        JsonNode node = JsonNode.Parse(File.ReadAllText(elementFile.FullName), documentOptions: new JsonDocumentOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        JsonArray json = node.AsArray();
        ProtoElement[] elements = new ProtoElement[json.Count];
        int i = 0;
        foreach (JsonNode item in json)
        {
            JsonObject jsonObj = item.AsObject();

            ProtoElement element = new ProtoElement();

            if (!CheckValue(jsonObj, "internalName", out element.internalName))
            {
                throw new JsonException($"Missing section 'name' in element '{path}'!");
            }
            if (!CheckValue(jsonObj, "color", out string color))
            {
                throw new JsonException($"Missing section 'color' in element '{path}'!");
            }
            else
            {
                try
                {
                    element.color = Rubedo.Lib.Extensions.ColorExtensions.FromHexARGB(color);
                }
                catch
                {
                    throw new ContentLoadException($"Element file '{path}', element '{element.internalName}', has a malformed color!");
                }
            }


            if (CheckValue(jsonObj, "cellType", out string cellType))
            {
                element.elementType = cellType.ToLower() switch
                {
                    "liquid" => Element.Type.LIQUID,
                    "gas" => Element.Type.GAS,
                    "solid" => Element.Type.PHYSICS_SOLID,
                    _ => throw new ContentLoadException($"Element file '{path}', element '{element.internalName}', has a malformed cell type!"),
                };
                element.def_elementType = true;
            }

            if (CheckValue(jsonObj, "parent", out string parent))
            {
                element.parent = parent;
                element.def_parent = true;
            }
            if (CheckValue(jsonObj, "inheritReactions", out bool inheritReactions))
            {
                element.inheritReactions = inheritReactions;
                element.def_inheritReactions = true;
            }
            if (CheckValue(jsonObj, "tags", out string tags))
            {
                element.tags = tags.Split(',');
                for (int z = 0; z < element.tags.Length; z++)
                {
                    element.tags[z] = element.tags[z].Trim();
                }
                element.def_tags = true;
            }
            if (CheckValue(jsonObj, "density", out float density))
            {
                element.density = density;
                element.def_density = true;
            }

            if (CheckValue(jsonObj, "liquid_isSand", out bool isSand))
            {
                element.liquid_isSand = isSand;
                element.def_liquid_isSand = true;
            }
            if (CheckValue(jsonObj, "liquid_isStatic", out bool isStatic))
            {
                element.liquid_isStatic = isStatic;
                element.def_liquid_isStatic = true;
            }
            if (CheckValue(jsonObj, "liquid_maxSpeed", out int maxSpeed))
            {
                element.liquid_maxSpeed = maxSpeed;
                element.def_liquid_maxSpeed = true;
            }
            if (CheckValue(jsonObj, "liquid_gravity", out float gravity))
            {
                element.liquid_gravity = gravity;
                element.def_liquid_gravity = true;
            }
            if (CheckValue(jsonObj, "liquid_dispersion", out int dispersion))
            {
                element.liquid_dispersion = dispersion;
                element.def_liquid_dispersion = true;
            }
            if (CheckValue(jsonObj, "liquid_inertialResistance", out int inertialResistance))
            {
                element.liquid_inertialResistance = inertialResistance;
                element.def_liquid_inertialResistance = true;
            }
            if (CheckValue(jsonObj, "liquid_friction", out float friction))
            {
                element.liquid_friction = friction;
                element.def_liquid_friction = true;
            }

            elements[i++] = element;
        }

        return elements;
    }

    private static bool CheckValue<T>(JsonObject json, string name, out T value)
    {
        if (json.TryGetPropertyValue(name, out JsonNode node))
        {
            try
            {
                value = node.GetValue<T>();
            }
            catch
            {
                json.TryGetPropertyValue("internalName", out JsonNode elementName);
                throw new JsonException($"Wrong data type for '{name}' in element '{elementName.GetValue<string>()}'!");
            }
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    public class ProtoElement : Element
    {
        public string parent;
        public bool inheritReactions = false;

        public bool finishedConstruction = false;

        public Dictionary<ReactionKey, Reaction> reactions;

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

        public ProtoElement()
        {
            reactions = new Dictionary<ReactionKey, Reaction>();
        }

        public Element Finish()
        {
            Element element = elementType switch
            {
                Element.Type.LIQUID => new Liquid(internalName),
                Element.Type.GAS => new Gas(internalName),
                Element.Type.PHYSICS_SOLID => new Solid(internalName),
                _ => throw new System.NotImplementedException(),
            };

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

            finishedConstruction = true;

            return element;
        }

        public Element Finish(ProtoElement parentElement)
        {
            Element.Type type = def_elementType ? elementType : parentElement.elementType;

            Element element = type switch
            {
                Element.Type.LIQUID => new Liquid(internalName),
                Element.Type.GAS => new Gas(internalName),
                Element.Type.PHYSICS_SOLID => new Solid(internalName),
                _ => throw new System.NotImplementedException(),
            };

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

            finishedConstruction = true;

            return element;
        }

        //should not be doing anything with ProtoElement, it is just data blocks
        public override void Step(WorldChunk caller, Cell cell)
        {
            throw new System.NotImplementedException();
        }
    }
}