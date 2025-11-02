using FallingSand.Game.Elements;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rubedo;
using Rubedo.Components;
using Rubedo.Graphics;
using Rubedo.Lib;
using Rubedo.Physics2D.Common;

namespace FallingSand.Game.World;

/// <summary>
/// TODO: I am SandWorld, and I don't have a summary yet.
/// </summary>
public class SandWorld : RenderableComponent
{
    Point worldMin;
    Point worldMax;
    public static Vector2 Gravity = new Vector2(0, 5f);

    public SandMatrix matrix;
    private Color[] drawData;
    private readonly Texture2D texture;

    private float accumulatedDelta = 0;
    public const float SAND_UPDATE_TIME = 1f / 100f;
    public bool doTick = true;
    public bool stepTick = true;

    public override RectF Bounds => new RectF(worldMin, worldMax);

    public SandWorld(Point worldMin, Point worldMax, int cellSize = 64)
    {
        LayerDepth = 0;
        this.worldMin = worldMin;
        this.worldMax = worldMax;
        matrix = new SandMatrix(worldMin, worldMax, cellSize);
        drawData = new Color[(worldMax.X - worldMin.X) * (worldMax.Y - worldMin.Y)];
        texture = new Texture2D(RubedoEngine.Graphics.GraphicsDevice, worldMax.X - worldMin.X, worldMax.Y - worldMin.Y);
    }

    public override void FixedUpdate()
    {
        accumulatedDelta += Time.FixedDeltaTime;

        // Avoid accumulator death spiral
        if (accumulatedDelta > SAND_UPDATE_TIME * 5)
            accumulatedDelta = SAND_UPDATE_TIME * 5;

        while (accumulatedDelta > SAND_UPDATE_TIME)
        {
            if (doTick)
            {
                matrix.StepAll();
            } else if (stepTick)
            {
                stepTick = false;
                matrix.StepAll();
            }
            accumulatedDelta -= SAND_UPDATE_TIME;
        }
    }

    public bool SpawnCell(Element e, int x, int y)
    {
        return matrix.SpawnCell(e, x, y);
    }

    public bool ClearCell(int x, int y)
    {
        return matrix.ClearCell(x, y);
    }

    public override void Render(Renderer renderer, Camera camera)
    {
        matrix.Draw(ref drawData, new Point(Math.FloorToInt(camera.X), Math.FloorToInt(camera.Y)), texture);

        if (texture == null)
            return; //nothing to render.

        if (!Visible || !IsVisibleToCamera(camera))
            return;

        renderer.Draw(
            texture,
            Entity.Transform,
            null,
            Color.White,
            Vector2.UnitY,
            SpriteEffects.FlipVertically, _layerDepth);
    }
}