using FallingSand.Game.World;
using Rubedo;
using Rubedo.Graphics.Viewports;
using Rubedo.Graphics;
using Rubedo.Input.Conditions;
using Rubedo.UI;
using Rubedo.Object;
using Microsoft.Xna.Framework;
using Rubedo.Input;
using FallingSand.Game.Elements;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using Rubedo.EngineDebug;
using Rubedo.UI.Layout;
using System;
using Rubedo.UI.Text;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace FallingSand.Game.States;

/// <summary>
/// TODO: I am WorldState, and I don't have a summary yet.
/// </summary>
public class WorldState : GameState
{
    public SandWorld world;

    private readonly AllCondition leftClickCondition = new AllCondition(new MouseCondition(InputManager.MouseButtons.Left), new NotCondition(new KeyCondition(Keys.LeftShift, true)));
    private readonly AllCondition shiftLeftClickCondition = new AllCondition(new MouseCondition(InputManager.MouseButtons.Left), new KeyCondition(Keys.LeftShift, true));
    private readonly MouseCondition rightClickCondition = new MouseCondition(InputManager.MouseButtons.Right);
    private readonly MouseCondition middleClickCondition = new MouseCondition(InputManager.MouseButtons.Middle);
    private readonly KeyCondition cameraLeft = new KeyCondition(Keys.A);
    private readonly KeyCondition cameraRight = new KeyCondition(Keys.D);
    private readonly KeyCondition cameraUp = new KeyCondition(Keys.W);
    private readonly KeyCondition cameraDown = new KeyCondition(Keys.S);
    private readonly KeyCondition cameraScaleUp = new KeyCondition(Keys.OemPlus);
    private readonly KeyCondition cameraScaleDown = new KeyCondition(Keys.OemMinus);
    private readonly KeyCondition cameraRotateCW = new KeyCondition(Keys.E);
    private readonly KeyCondition cameraRotateCCW = new KeyCondition(Keys.Q);
    private readonly KeyCondition cameraReset = new KeyCondition(Keys.R);
    private readonly KeyCondition pauseSim = new KeyCondition(Keys.P);
    private readonly KeyCondition stepSim = new KeyCondition(Keys.O);
    private readonly KeyCondition toggleCells = new KeyCondition(Keys.C);
    private readonly KeyCondition toggleRects = new KeyCondition(Keys.V);

    private Shapes shapes;
    private Liquid sand;
    private Liquid water;
    private Liquid dirt;
    private Liquid stone;
    private Gas smoke;

    private List<(Element, int)> spawnables;
    private int spawnIndex = 0;

    public Vertical debugRoot;
    public List<DebugTextEntry> debugText = new List<DebugTextEntry>();
    private double deltaTime = 0.0f;
    private bool drawCells = false;
    private bool drawRects = false;

    public WorldState(StateManager sm) : base(sm)
    {
        shapes = new Shapes(RubedoEngine.Instance);
        _name = "WorldState";
    }

    public override void LoadContent()
    {
        Time.SetFixedDeltaTime(1f / 50f);
        Assets.CreateNewFontSystem("fs-default", "fonts/DroidSans.ttf", "fonts/DroidSansJapanese.ttf", "fonts/Symbola-Emoji.ttf");
        base.LoadContent();
    }

    public override void Enter()
    {
        GUI.Root = new GUIRoot(new Point(200, 120), false);
        Renderables.Add(GUI.Root);
        RubedoEngine.Instance.Renderer.GlobalScale = 1f;
        debugRoot = new Vertical();
        debugRoot.Offset = new Vector2(30, 0);
        GUI.Root.AddChild(debugRoot);

        Camera camera = new Camera(this, new BestFitViewport(RubedoEngine.Instance.GraphicsDevice, RubedoEngine.Instance.Window, 200, 120), 0);
        camera.RenderLayers.Add((int)Rubedo.Graphics.Sprites.RenderLayer.Default);
        camera.RenderLayers.Add((int)Rubedo.Graphics.Sprites.RenderLayer.UI);

        debugRoot = new Vertical();
        debugRoot.Offset = new Vector2(30, 0);
        GUI.Root.AddChild(debugRoot);

        sand = new Liquid("Sand");
        sand.density = 5;
        sand.color = new Color(213f / 255f, 185f / 255f, 113f / 255f);
        sand.liquid_isSand = true;
        sand.liquid_inertialResistance = 10;
        dirt = new Liquid("Dirt");
        dirt.color = new Color(0.318f, 0.2f, 0.03f);
        dirt.density = 10;
        dirt.liquid_isSand = true;
        dirt.liquid_inertialResistance = 50;
        dirt.liquid_gravity = 2;
        water = new Liquid("Water");
        water.density = 1;
        water.color = new Color(20f / 255f, 100f / 255f, 171f / 255f);
        water.liquid_dispersion = 5;
        water.liquid_gravity = 3;
        stone = new Liquid("Stone");
        stone.density = 50;
        stone.color = new Color(0.4f, 0.4f, 0.4f);
        stone.liquid_isStatic = true;
        smoke = new Gas("Smoke");
        smoke.density = 1;
        smoke.color = new Color(0.3f, 0.3f, 0.3f);

        spawnables = new List<(Element, int)>();
        spawnables.Add((sand, 100));
        spawnables.Add((dirt, 35));
        spawnables.Add((water, 35));
        spawnables.Add((stone, 100));
        spawnables.Add((smoke, 35));

        world = new SandWorld(new Point(0, 0), new Point(512, 512), 64);
        Add(new Entity() { world });

        AddDebugLabel(debugRoot, () => string.Format("{0:0.0} ms ({1:0.} fps)", deltaTime * 1000.0f, 1.0f / deltaTime));
        AddDebugLabel(debugRoot, () => $"Selected Material: {spawnables[spawnIndex].Item1.name}");
    }

    public void AddDebugLabel(Vertical vert, Func<string> valueFunc)
    {
        FontSystem font = Assets.GetFontSystem("fs-default");
        Label label = new Label(font, string.Empty, Color.Green, 18);
        label.TightLineHeight = true;
        DebugTextEntry entry = new DebugTextEntry(label, valueFunc);
        debugText.Add(entry);
        vert.AddChild(label);
    }

    public override void Update()
    {
        base.Update();
        deltaTime += (Time.RawDeltaTime - deltaTime) * 0.1f;
        for (int i = 0; i < debugText.Count; i++)
        {
            debugText[i].Update();
        }
    }

    public override void Draw(Renderer sb)
    {
        base.Draw(sb);

        shapes.Begin(MainCamera);
        if (drawCells)
        {
            foreach (WorldChunk chunk in world.matrix.chunks)
            {
                shapes.DrawBox(chunk.chunkX, chunk.chunkY, chunk.size, chunk.size, Color.DarkGray, 1f);
            }
        }
        if (drawRects)
        {
            foreach (WorldChunk chunk in world.matrix.chunks)
            {
                shapes.DrawBox(Rectangle.Union(chunk.dirtyRect, chunk.prevDirtyRect), Color.Red, 0.5f);
            }
        }
        shapes.End();
    }

    private int brushSize = 3;
    public override void HandleInput()
    {
        int scroll = InputManager.MouseScroll() / 60;
        if (Math.Abs(scroll) > 1)
        {
            brushSize = Rubedo.Lib.Math.Clamp(brushSize + scroll, 0, 16);
        }

        if (rightClickCondition.Pressed())
        {
            spawnIndex = spawnIndex == spawnables.Count - 1 ? 0 : spawnIndex + 1;
        }
        (Element type, int chance) = spawnables[spawnIndex];
        SpawnCell(leftClickCondition.Pressed(), leftClickCondition.Held(), type, chance);
        SpawnCell(shiftLeftClickCondition.Pressed(), shiftLeftClickCondition.Held(), null, 101);

        if (middleClickCondition.Pressed())
        {
            Point mouse = InputManager.MouseWorldPosition().ToPoint();
            Entity entity = new Entity(mouse.ToVector2())
            {
                new Spout(world, spawnables[spawnIndex].Item1, brushSize)
            };
            Add(entity);
        }

        if (cameraLeft.Pressed() || cameraLeft.Held())
            MainCamera.XY += new Vector2(-1, 0);
        if (cameraRight.Pressed() || cameraRight.Held())
            MainCamera.XY += new Vector2(1, 0);
        if (cameraUp.Pressed() || cameraUp.Held())
            MainCamera.XY += new Vector2(0, 1);
        if (cameraDown.Pressed() || cameraDown.Held())
            MainCamera.XY += new Vector2(0, -1);
        if (cameraRotateCW.Pressed() || cameraRotateCW.Held())
            MainCamera.Rotation += 0.01f;
        if (cameraRotateCCW.Pressed() || cameraRotateCCW.Held())
            MainCamera.Rotation -= 0.01f;
        if (cameraScaleDown.Pressed() || cameraScaleDown.Held())
            MainCamera.Scale -= new Vector2(0.01f, 0.01f);
        if (cameraScaleUp.Pressed() || cameraScaleUp.Held())
            MainCamera.Scale += new Vector2(0.01f, 0.01f);
        if (cameraReset.Pressed())
        {
            MainCamera.XY = Vector2.Zero;
            MainCamera.Rotation = 0;
            MainCamera.Scale = Vector2.One;
        }

        if (stepSim.Pressed())
        {
            world.stepTick = true;
        }
        if (pauseSim.Pressed())
        {
            world.doTick = !world.doTick;
        }
        if (toggleCells.Pressed())
        {
            drawCells = !drawCells;
        }
        if (toggleRects.Pressed())
        {
            drawRects = !drawRects;
        }
    }

    private void SpawnCell(bool pressed, bool held, Element type, int chance)
    {
        if (held || pressed)
        {
            if (held)
            {
                Point curPos = InputManager.MouseWorldPosition().ToPoint();
                Point prevPos = InputManager.PreviousMouseWorldPosition().ToPoint();
                world.matrix.IterateAndApplyBetweenPoints(prevPos, curPos, (x, y) => SpawnCellRun(x, y, type, chance, brushSize));
            }
            else
            {
                Point pos = InputManager.MouseWorldPosition().ToPoint();
                SpawnCellRun(pos.X, pos.Y, type, chance, brushSize);
            }
        }
    }

    private void SpawnCellRun(int m_x, int m_y, Element type, int chance, int brushSize)
    {
        if (brushSize == 0)
        {
            if (type == null)
                world.ClearCell(m_x, m_y);
            else
                world.SpawnCell(type, m_x, m_y);
            return;
        }
        for (int x = -brushSize; x < brushSize; x++)
        {
            for (int y = -brushSize; y < brushSize; y++)
            {
                if (Rubedo.Lib.Random.Percent < chance)
                {
                    if (type == null)
                        world.ClearCell(m_x + x, m_y + y);
                    else
                        world.SpawnCell(type, m_x + x, m_y + y);
                }
            }
        }
    }

    public class DebugTextEntry
    {
        Func<string> valueFunc;
        public Label label;

        public DebugTextEntry(Label label, Func<string> valueFunc)
        {
            this.label = label;
            this.valueFunc = valueFunc;
        }

        public void Update()
        {
            label.Text = valueFunc();
        }
    }
}