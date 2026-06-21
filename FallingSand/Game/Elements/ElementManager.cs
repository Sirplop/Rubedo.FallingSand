using Microsoft.Xna.Framework;
using Rubedo.Resources;
using System.Collections.Generic;
using System.IO;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am ElementManager, and I don't have a summary yet.
/// </summary>
public static class ElementManager
{
    public static Dictionary<ReactionKey, Reaction> reactions;

    public static Dictionary<string, Element> elementsByName;
    public static Dictionary<Color, Element> elementsByColor;

    public static Dictionary<string, HashSet<Element>> tags;

    private static Element[] elements;

    public static void Initialize()
    {
        reactions = new Dictionary<ReactionKey, Reaction>();
        elementsByName = new Dictionary<string, Element>();
        elementsByColor = new Dictionary<Color, Element>();
        tags = new Dictionary<string, HashSet<Element>>();
    }

    public static void LoadElements(string folderPath)
    {
        string path = Path.Combine(Assets.RootDirectory, folderPath);
        DirectoryInfo baseDirectoryInfo = new DirectoryInfo(path);

        List<DirectoryInfo> directories = new List<DirectoryInfo>();
        directories.Add(baseDirectoryInfo);

        List<ElementLoader.ProtoElement> prototypes = new List<ElementLoader.ProtoElement>();

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

        elements = ElementLoader.PopulateElements(prototypes);

        for (int i = 0; i < elements.Length; i++)
        {
            Element element = elements[i];
            elementsByName.Add(element.internalName, element);
            elementsByColor.Add(element.color, element);
            if (element.tags != null)
            {
                for (int j = 0; j < element.tags.Length; j++)
                {
                    AddToTag(element, element.tags[j]);
                }
            }
        }
    }

    public static void AddToTag(Element element, string tag)
    {
        if (!tags.TryGetValue(tag, out HashSet<Element> tagSet))
        {
            tagSet = new HashSet<Element>();
            tags.Add(tag, tagSet);
        }
        tagSet.Add(element);
    }
}