using FallingSand.Game.Elements;
using FallingSand.Game.World;
using Rubedo;
using Rubedo.Components;

namespace FallingSand.Game;

/// <summary>
/// TODO: I am Spout, and I don't have a summary yet.
/// </summary>
public class Spout : Component
{
    SandWorld worldRef;
    readonly int element;
    float life;

    public Spout(SandWorld world, int element, float lifetime)
    {
        this.worldRef = world;
        this.element = element;
        this.life = lifetime;
    }

    public override void Update()
    {
        worldRef.SpawnCell(element, Rubedo.Lib.Math.CeilToInt(Transform.Position.X), Rubedo.Lib.Math.CeilToInt(Transform.Position.Y));
        life -= Time.DeltaTime;
        if (life < 0)
            Entity.Destroy();
    }
}