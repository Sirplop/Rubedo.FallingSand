namespace FallingSand.Game.Elements;

/// <summary>
/// TODO: I am Reaction, and I don't have a summary yet.
/// </summary>
public struct Reaction
{
    public int probability;
    public string cellType1;
    public string cellType2;
    public string outputCell1;
    public string outputCell2;
}

public struct ReactionKey
{
    public int cellType1;
    public int cellType2;
}