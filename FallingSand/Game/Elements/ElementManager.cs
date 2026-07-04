using Microsoft.Xna.Framework;
using Rubedo;
using Rubedo.Resources;
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
        elementsByName = new Dictionary<string, int>();
        elementsByColor = new Dictionary<Color, int>();
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
        liquid_friction[0] =0;

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

    public static bool HasReaction(in int actorElement, in int targetElement)
    {
        if (actorElement > targetElement)
        {
            long key = actorElement << 32 + targetElement;
            return reactions.ContainsKey(key);
        }
        else
        {
            long key = targetElement << 32 + actorElement;
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
        if (!elementsByTag.TryGetValue(tag, out HashSet<int> tagSet))
        {
            tagSet = new HashSet<int>();
            elementsByTag.Add(tag, tagSet);
        }
        tagSet.Add(element);
    }
}