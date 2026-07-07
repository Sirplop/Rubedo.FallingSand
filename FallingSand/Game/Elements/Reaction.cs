using System;

namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Reaction, and I don't have a summary yet.
/// </summary>
public struct Reaction
{
    public int probability;
    public int outputCell1;
    public int outputCell2;
}
public struct ReactionValue
{
    public int probability;
    public string outputCell1;
    public string outputCell2;
}
public struct ReactionKey
{
    public string cellType1;
    public string cellType2;
}