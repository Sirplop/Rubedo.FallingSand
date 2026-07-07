using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Rubedo.Lib;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am ElementSerializer, and I don't have a summary yet.
/// </summary>
public static class ElementLoader
{
    public static List<FinishedElement> PopulateElements(List<ProtoElement> elements)
    {
        List<FinishedElement> result = new List<FinishedElement>();
        result.Add(null); //represents empty space

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
                    "liquid" => ElementManager.Type.LIQUID,
                    "gas" => ElementManager.Type.GAS,
                    "solid" => ElementManager.Type.PHYSICS_SOLID,
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
            if (CheckValue(jsonObj, "liquid_maxSpeed", out byte maxSpeed))
            {
                element.liquid_maxSpeed = maxSpeed;
                element.def_liquid_maxSpeed = true;
            }
            if (CheckValue(jsonObj, "liquid_gravity", out byte gravity))
            {
                element.liquid_gravity = gravity;
                element.def_liquid_gravity = true;
            }
            if (CheckValue(jsonObj, "liquid_dispersion", out byte dispersion))
            {
                element.liquid_dispersion = dispersion;
                element.def_liquid_dispersion = true;
            }
            if (CheckValue(jsonObj, "liquid_inertialResistance", out byte inertialResistance))
            {
                element.liquid_inertialResistance = inertialResistance;
                element.def_liquid_inertialResistance = true;
            }
            if (CheckValue(jsonObj, "liquid_friction", out byte friction))
            {
                element.liquid_friction = friction;
                element.def_liquid_friction = true;
            }

            if (jsonObj.TryGetPropertyValue("reaction", out JsonNode reactNode))
            {
                JsonArray reactArray = reactNode.AsArray();
                foreach (JsonNode react in reactArray)
                {
                    JsonObject reaction = react.AsObject();
                    if (!CheckValue(reaction, "probability", out int probability))
                    {
                        throw new JsonException($"Malformed reaction for element {element.internalName} in file '{path}'");
                    }
                    if (!CheckValue(reaction, "input_cell_1", out string input_cell_1))
                    {
                        throw new JsonException($"Malformed reaction for element {element.internalName} in file '{path}'");
                    }
                    if (!CheckValue(reaction, "input_cell_2", out string input_cell_2))
                    {
                        throw new JsonException($"Malformed reaction for element {element.internalName} in file '{path}'");
                    }
                    if (!CheckValue(reaction, "output_cell_1", out string output_cell_1))
                    {
                        throw new JsonException($"Malformed reaction for element {element.internalName} in file '{path}'");
                    }
                    if (!CheckValue(reaction, "output_cell_2", out string output_cell_2))
                    {
                        throw new JsonException($"Malformed reaction for element {element.internalName} in file '{path}'");
                    }

                    ReactionValue r = new ReactionValue() { outputCell1 = output_cell_1, outputCell2 = output_cell_2, probability = probability };

                    element.reactions.Add(new ReactionKey() { cellType1 = input_cell_1, cellType2 = input_cell_2 }, r);
                }
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
                throw new JsonException($"Wrong data type for '{name}' in element '{elementName.GetValue<string>()}'! (was {typeof(T)})");
            }
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }
}