using FallingSand.Game.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Rubedo;
using Rubedo.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am ElementManager, and I don't have a summary yet.
/// </summary>
public static class ElementManager
{
    public enum Type
    {
        EMPTY = 0,
        LIQUID = 1,
        GAS = 2,
        PHYSICS_SOLID = 3,
    }

    public const int FREE_FALLING_THRESHOLD = 5; //number of frames the pixel must not move to reset free falling.
    public const int EMPTY = 0; //makes comparing to element 0 more obvious.

    public static bool Loaded { get; private set; }

    public static Dictionary<long, Reaction> reactions;

    public static Dictionary<string, int> elementsByName;
    public static Dictionary<Color, int> elementsByColor;

    public static Dictionary<string, HashSet<int>> elementsByTag;

    public static string[][] tags;

    public static Type[] typeLookup;
    public static string[]  internalName;
    public static Color[]   color;
    public static float[]   density;

    public static bool[]    liquid_isStatic; //does this particle move
    public static bool[]    liquid_isSand; //is this particle a powder, aka only moves downwards?

    public static byte[]     liquid_maxSpeed; //how fast can a single pixel go?
    public static byte[]     liquid_gravity; //vertical acceleration rate
    public static byte[]     liquid_dispersion; //how far the particle looks left and right to move to the side
    public static byte[]     liquid_inertialResistance; //[0, 100] how likely is this element to become freefalling when something passes by?
    public static byte[]     liquid_friction; //how fast does this element slow down?

    private static List<ProtoElement> elementPrototypes;

    public static void Initialize()
    {
        if (Loaded)
        {
            Log.Warn("Tried to re-initialize elements! That's not supported! Yet... ;)");
            return;
        }

        reactions = new Dictionary<long, Reaction>();
        elementsByName = new Dictionary<string, int>() { { "air", 0 } };
        elementsByColor = new Dictionary<Color, int>() { { Color.Transparent, 0 } };
        elementsByTag = new Dictionary<string, HashSet<int>>();
        elementPrototypes = new List<ProtoElement>();
    }

    /// <summary>
    /// Marks the manager as loaded, and populates all element fields.
    /// </summary>
    public static void FinishInitialize()
    {
        List<FinishedElement> elements = ElementLoader.PopulateElements(elementPrototypes);
        int count = elements.Count;

        typeLookup = new Type[count];
        tags = new string[count][];
        internalName = new string[count];
        color = new Color[count];
        density = new float[count];
        liquid_isStatic = new bool[count];
        liquid_isSand = new bool[count];
        liquid_maxSpeed = new byte[count];
        liquid_gravity = new byte[count];
        liquid_dispersion = new byte[count];
        liquid_inertialResistance = new byte[count];
        liquid_friction = new byte[count];

        typeLookup[0] = Type.EMPTY;
        internalName[0] = "air";
        color[0] = Color.Transparent;
        density[0] = 0;
        liquid_isStatic[0] = true;
        liquid_isSand[0] = false;
        liquid_maxSpeed[0] = 0;
        liquid_gravity[0] = 0;
        liquid_dispersion[0] = 0;
        liquid_inertialResistance[0] = 0;
        liquid_friction[0] = 0;

        for (int i = 1; i < count; i++)
        {
            FinishedElement element = elements[i];
            element.element_id = i;
            typeLookup[i] = element.elementType;
            elementsByName.Add(element.internalName, i);
            elementsByColor.Add(element.color, i);
            if (element.tags != null)
            {
                for (int j = 0; j < element.tags.Length; j++)
                {
                    AddToTag(i, element.tags[j]);
                }
                tags[i] = element.tags;
            }

            typeLookup[i] = element.elementType;
            internalName[i] = element.internalName;
            color[i] = element.color;
            density[i] = element.density;
            liquid_isStatic[i] = element.liquid_isStatic;
            liquid_isSand[i] = element.liquid_isSand;
            liquid_maxSpeed[i] = element.liquid_maxSpeed;
            liquid_gravity[i] = element.liquid_gravity;
            liquid_dispersion[i] = element.liquid_dispersion;
            liquid_inertialResistance[i] = element.liquid_inertialResistance;
            liquid_friction[i] = element.liquid_friction;
        }

        LoadReactions(elements);

        Loaded = true;
    }

    public static void LoadElements(string folderPath)
    {
        string path = Path.Combine(Assets.RootDirectory, folderPath);
        DirectoryInfo baseDirectoryInfo = new DirectoryInfo(path);

        List<DirectoryInfo> directories = new List<DirectoryInfo>();
        directories.Add(baseDirectoryInfo);

        List<ProtoElement> prototypes = new List<ProtoElement>();

        for (int i = 0; i < directories.Count; i++)
        { //creates list of all directories.
            DirectoryInfo info = directories[i];
            foreach (DirectoryInfo dir in info.GetDirectories())
            {
                directories.Add(dir);
            }

            FileInfo[] fileInfo = info.GetFiles("*.json");
            string[] paths = new string[fileInfo.Length];
            for (int x = 0; x < fileInfo.Length; x++)
            {
                paths[x] = fileInfo[x].FullName;
            }
            prototypes.AddRange(ElementLoader.LoadProtoElements(paths));
        }
        elementPrototypes.AddRange(prototypes);
    }

    /// <summary>
    /// Takes the list of prototypes and their reactions, and maps them into the proper reaction format.
    /// </summary>
    private static void LoadReactions(List<FinishedElement> elements)
    {
        int count = elements.Count;
        for (int id = 1; id < count; id++)
        {
            FinishedElement element = elements[id];
            if (element.reactions.Count == 0)
                continue;

            foreach (KeyValuePair<ReactionKey, ReactionValue> reaction in element.reactions)
            {
                List<string> cellType1 = new List<string>();
                string preTag_1;
                string postTag_1;
                string tagName_1;
                if (reaction.Key.cellType1.Contains('['))
                { //this is a tag! split it up!
                    string[] split = reaction.Key.cellType1.Split('[');
                    preTag_1 = split[0];
                    split = split[1].Split(']');
                    postTag_1 = split[1];
                    tagName_1 = split[0];
                    HashSet<int> possibleElements = elementsByTag[tagName_1];
                    foreach (int pID in possibleElements)
                    {
                        string elementName = elements[pID].internalName;
                        cellType1.Add(elementName);
                    }
                }
                else
                {
                    preTag_1 = "";
                    postTag_1 = "";
                    tagName_1 = "";
                    cellType1.Add(reaction.Key.cellType1);
                }

                List<string> cellType2 = new List<string>();
                string preTag_2;
                string postTag_2;
                string tagName_2;
                if (reaction.Key.cellType2.Contains('['))
                { //this is a tag! split it up!
                    string[] split = reaction.Key.cellType2.Split('[');
                    preTag_2 = split[0];
                    split = split[1].Split(']');
                    postTag_2 = split[1];
                    tagName_2 = split[0];
                    HashSet<int> possibleElements = elementsByTag[tagName_2];
                    foreach (int pID in possibleElements)
                    {
                        string elementName = elements[pID].internalName;
                        cellType2.Add(elementName);
                    }
                }
                else
                {
                    preTag_2 = "";
                    postTag_2 = "";
                    tagName_2 = "";
                    cellType2.Add(reaction.Key.cellType2);
                }

                int out_1 = 0;
                string preTagOut_1;
                string postTagOut_1;
                if (reaction.Value.outputCell1.Contains('['))
                { //it's a tag again! make sure it's the same tag as in cell type 1
                    string[] split = reaction.Value.outputCell1.Split('[');
                    preTagOut_1 = split[0];
                    split = split[1].Split(']');
                    postTagOut_1 = split[1];
                    string tag = split[0];
                    if (tag != tagName_1)
                        throw new ContentLoadException($"Element {elements[id].internalName} has a malformed reaction! (Tag mismatch: {tagName_1}, {tag})");

                    out_1 = -1;
                }
                else
                {
                    preTagOut_1 = "";
                    postTagOut_1 = "";
                    out_1 = elementsByName[reaction.Value.outputCell1];
                }

                int out_2 = 0;
                string preTagOut_2;
                string postTagOut_2;
                if (reaction.Value.outputCell2.Contains('['))
                { //it's a tag again! make sure it's the same tag as in cell type 2
                    string[] split = reaction.Value.outputCell2.Split('[');
                    preTagOut_2 = split[0];
                    split = split[1].Split(']');
                    postTagOut_2 = split[1];
                    string tag = split[0];
                    if (tag != tagName_2)
                        throw new ContentLoadException($"Element {elements[id].internalName} has a malformed reaction! (Tag mismatch: {tagName_2}, {tag})");

                    out_2 = -1;
                }
                else
                {
                    preTagOut_2 = "";
                    postTagOut_2 = "";
                    out_2 = elementsByName[reaction.Value.outputCell2];
                }

                //now, we finally generate the reactions.
                int probability = reaction.Value.probability;
                for (int x1 = 0; x1 < cellType1.Count; x1++)
                {
                    string input_1 = cellType1[x1];
                    string output_1;
                    if (out_1 == -1)
                    {
                        output_1 = preTagOut_1 + input_1 + postTagOut_1;
                    }
                    else if (out_1 == 0)
                    {
                        output_1 = "air";
                    }
                    else
                    {
                        output_1 = elements[out_1].internalName;
                    }
                    input_1 = preTag_1 + input_1 + postTag_1;
                    if (!elementsByName.TryGetValue(input_1, out int i1) || !elementsByName.TryGetValue(output_1, out int o1))
                        continue;

                    for (int x2 = 0; x2 < cellType2.Count; x2++)
                    {
                        string input_2 = cellType2[x2]; 
                        string output_2;
                        if (out_2 == -1)
                        {
                            output_2 = preTagOut_2 + input_2 + postTagOut_2;
                        }
                        else if (out_2 == 0)
                        {
                            output_2 = "air";
                        }
                        else
                        {
                            output_2 = elements[out_2].internalName;
                        }
                            input_2 = preTag_2 + input_2 + postTag_2;
                        if (!elementsByName.TryGetValue(input_2, out int i2) || !elementsByName.TryGetValue(output_2, out int o2))
                            continue;

                        if (i1 == i2 && o1 == o2)
                            continue;

                        long key;
                        if (i1 > i2)
                        {
                            key = ((long)i1 << 32) + i2;
                        }
                        else
                        {
                            key = ((long)i2 << 32) + i1;
                        }
                        if (reactions.ContainsKey(key))
                            continue;

                        Reaction newReaction = new Reaction() { outputCell1 = o1, outputCell2 = o2, probability = probability };
                        reactions.Add(key, newReaction);
                    }
                }
            }
        }
    }

    public static bool HasReaction(in int actorElement, in int targetElement)
    {
        if (actorElement > targetElement)
        {
            long key = ((long)actorElement << 32) + targetElement;
            return reactions.ContainsKey(key);
        }
        else
        {
            long key = ((long)targetElement << 32) + actorElement;
            return reactions.ContainsKey(key);
        }
    }

    public static void CreateDebugElements()
    {
        ProtoElement powder = new ProtoElement();
        powder.color = Color.Brown;
        powder.internalName = "debug_powder";
        powder.liquid_isSand = true;
        powder.elementType = Type.LIQUID;
        elementPrototypes.Add(powder);
    }

    public static void AddToTag(int element, string tag)
    {
        string[] tags = tag.Split(',');
        for (int i = 0; i < tags.Length; i++)
        {
            string stripTag = tags[i].Trim().Split('[')[1].Split(']')[0];
            if (!elementsByTag.TryGetValue(stripTag, out HashSet<int> tagSet))
            {
                tagSet = new HashSet<int>();
                elementsByTag.Add(stripTag, tagSet);
            }
            tagSet.Add(element);
        }
    }
}