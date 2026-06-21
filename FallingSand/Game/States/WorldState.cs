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
using Rubedo.Resources;
using FallingSand.Game.UI;
using Rubedo.Lib.Extensions;

namespace FallingSand.Game.States;

/// <summary>
/// TODO: I am WorldState, and I don't have a summary yet.
/// </summary>
public class WorldState : GameState
{
    public SandWorld world;

    public readonly AllCondition leftClickCondition = new AllCondition(new MouseCondition(InputManager.MouseButtons.Left), new NotCondition(new KeyCondition(Keys.LeftShift, true)));
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
    private readonly KeyCondition togglePosition = new KeyCondition(Keys.B);
    private readonly KeyCondition toggleCellDetails = new KeyCondition(Keys.N);
    private readonly KeyCondition toggleDrawOverride = new KeyCondition(Keys.X);

    private Shapes shapes;

    public Element selectedElement;

    public Vertical debugRoot;
    public Vertical mouseVertical;
    public List<DebugTextEntry> debugText = new List<DebugTextEntry>();
    private double deltaTime = 0.0f;
    private bool drawCells = false;
    private bool drawRects = false;
    private bool drawPosition = false;
    private bool drawCellDetails = false;
    private bool drawMoveOverride = false;

    public WorldState(StateManager sm) : base(sm)
    {
        shapes = new Shapes(RubedoEngine.Instance);
        _name = "WorldState";
    }

    public override void LoadContent()
    {
        ElementManager.Initialize();
        ElementManager.LoadElements("materials");

        Time.SetFixedDeltaTime(1f / 50f);
        Assets.CreateNewFontSystem("fs-default", "DroidSans.ttf", "DroidSansJapanese.ttf", "Symbola-Emoji.ttf");
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

        world = new SandWorld(64, 8);
        Add(new Entity() { world });

        ElementSideBar bar = new ElementSideBar(this);

        AddDebugLabel(debugRoot, () => string.Format("{0:0.0} ms ({1:0.} fps)", deltaTime * 1000.0f, 1.0f / deltaTime));
        AddDebugLabel(debugRoot, () => $"Selected Material: {selectedElement.internalName}");
        CreateMouseDebugGUI();
    }

    public void AddDebugLabel(Vertical vert, Func<string> valueFunc)
    {
        FontSystem font = Assets.GetFont("fs-default");
        Label label = new Label(font, string.Empty, Color.Green, 18);
        label.TightLineHeight = true;
        DebugTextEntry entry = new DebugTextEntry(label, valueFunc);
        debugText.Add(entry);
        vert.AddChild(label);
    }
    public void CreateMouseDebugGUI()
    {
        mouseVertical = new Vertical();
        FontSystem font = Assets.GetFont("fs-default");
        Label world = new Label(font, string.Empty, Color.AntiqueWhite, 18);
        mouseVertical.AddChild(world);
        GUI.Root.AddChild(mouseVertical);
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
            foreach (WorldRegion region in world.regions)
            {
                foreach (WorldChunk chunk in region.chunks)
                {
                    shapes.DrawBox(chunk.chunkX, chunk.chunkY, chunk.size, chunk.size, Color.DarkGray, 0.5f);
                }
            }
        }
        if (drawRects)
        {
            foreach (WorldRegion region in world.regions)
            {
                foreach (WorldChunk chunk in region.chunks)
                {
                    if (chunk.DirtyRect.Height != 0 && chunk.DirtyRect.Width != 0)
                    shapes.DrawBox(chunk.DirtyRect, Color.Red, 0.5f);
                }
            }
        }
        shapes.End();

        if (drawPosition && mouseVertical != null && !mouseVertical.IsDestroyed)
        {
            Vector2 mouse = InputManager.MouseWorldPosition(MainCamera);
            int x = Rubedo.Lib.Math.FloorToInt(mouse.X);
            int y = Rubedo.Lib.Math.FloorToInt(mouse.Y);
            Cell cell = this.world.GetCell(x, y);
            string material = "???";
            if (cell != null)
            {
                if (cell.IsEmpty())
                    material = "air";
                else
                    material = cell.element.internalName;
            }

            Vector2 mouseScreen = InputManager.MouseScreenPosition(MainCamera);
            mouse = new Vector2(MathF.Ceiling(mouse.X), MathF.Ceiling(mouse.Y));
            mouseVertical.Offset = GUI.Root.ScreenToUI(mouseScreen + new Vector2(15, 0));

            Label world = mouseVertical.Children[0] as Label;
            world.Text = material + " - "+mouse.ToNiceString("0");

            if (drawCellDetails && cell != null && !cell.IsEmpty())
            {
                world.Text += $"\nCell Pos: {cell.x}, {cell.y}" +
                    $"\nVelocity: {cell.xVel}, {cell.yVel}" +
                    $"\nFreefalling: {cell.freeFalling}, {cell.freeFallingCount}" +
                    $"\nLast Frame: {cell.lastFrame}";
            }
            else if (drawCellDetails && cell != null)
            {
                world.Text += $"\nCell Pos: {cell.x}, {cell.y}";
            }
        }
    }

    private int brushSize = 3;
    public override void HandleInput()
    {
        int scroll = InputManager.MouseScroll() / 60;
        if (Math.Abs(scroll) > 1)
        {
            brushSize = Rubedo.Lib.Math.Clamp(brushSize + scroll, 0, 16);
        }

        SpawnCell(leftClickCondition.Pressed(), leftClickCondition.Held(), selectedElement, selectedElement.liquid_isStatic ? 100 : 35);
        SpawnCell(shiftLeftClickCondition.Pressed(), shiftLeftClickCondition.Held(), null, 101);

        if (middleClickCondition.Pressed())
        {
            Point mouse = InputManager.MouseWorldPosition().ToPoint();
            Entity entity = new Entity(mouse.ToVector2())
            {
                new Spout(world, selectedElement, brushSize)
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
        if (togglePosition.Pressed())
        {
            drawPosition = !drawPosition;
            mouseVertical.SetVisible(drawPosition);
        }
        if (toggleCellDetails.Pressed())
        {
            drawCellDetails = !drawCellDetails;
        }
        if (toggleDrawOverride.Pressed())
        {
            drawMoveOverride = !drawMoveOverride;
            world.drawMoveOverride = drawMoveOverride;
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
                world.IterateAndApplyBetweenPoints(prevPos, curPos, (x, y) => SpawnCellRun(x, y, type, chance, brushSize));
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