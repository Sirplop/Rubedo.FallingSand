using FallingSand.Game.Elements;
using FallingSand.Game.UI;
using FallingSand.Game.World;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rubedo;
using Rubedo.EngineDebug;
using Rubedo.Graphics;
using Rubedo.Graphics.Viewports;
using Rubedo.Input;
using Rubedo.Input.Conditions;
using Rubedo.Lib.Extensions;
using Rubedo.Object;
using Rubedo.Resources;
using Rubedo.UI;
using Rubedo.UI.Layout;
using Rubedo.UI.Text;
using System;
using System.Collections.Generic;
using static FallingSand.Game.World.WorldChunk;

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
    private readonly KeyCondition clearAll = new KeyCondition(Keys.Tab);
    private readonly AllCondition spawnCheckerboard = new AllCondition(new KeyCondition(Keys.Z), new NotCondition(new KeyCondition(Keys.LeftShift, true)), new NotCondition(new KeyCondition(Keys.LeftControl, true)));
    private readonly AllCondition spawnRandomCheckerboard = new AllCondition(new KeyCondition(Keys.Z), new KeyCondition(Keys.LeftShift, true), new NotCondition(new KeyCondition(Keys.LeftControl, true)));
    private readonly AllCondition spawnTopHalf = new AllCondition(new KeyCondition(Keys.Z), new KeyCondition(Keys.LeftControl, true), new NotCondition(new KeyCondition(Keys.LeftShift, true)));
    private readonly AllCondition spawnRandomTopHalf = new AllCondition(new KeyCondition(Keys.Z), new KeyCondition(Keys.LeftControl, true), new KeyCondition(Keys.LeftShift, true));
    private readonly KeyCondition toggleCells = new KeyCondition(Keys.C);
    private readonly KeyCondition toggleRects = new KeyCondition(Keys.V);
    private readonly KeyCondition togglePosition = new KeyCondition(Keys.B);
    private readonly KeyCondition toggleCellDetails = new KeyCondition(Keys.N);
    private readonly KeyCondition toggleDrawOverride = new KeyCondition(Keys.X);
    private readonly KeyCondition toggleVSync = new KeyCondition(Keys.F1);
    private readonly KeyCondition toggleTPSCap = new KeyCondition(Keys.F2);

    private Shapes shapes;

    public int selectedElement;

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
        ElementManager.FinishInitialize();

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
        AddDebugLabel(debugRoot, () => $"Selected Material: {ElementManager.internalName[selectedElement]}");
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
                    shapes.DrawBox(chunk.chunkX, chunk.chunkY, chunk.size, chunk.size, Color.DarkGray, 0.5f / MainCamera.Scale.X);
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
                        shapes.DrawBox(chunk.DirtyRect, Color.Red, 0.5f / MainCamera.Scale.X);
                }
            }
        }
        Vector2 mouse = InputManager.MouseWorldPosition(MainCamera);

        shapes.DrawBox(mouse.X - brushSize, mouse.Y - brushSize, brushSize * 2, brushSize * 2, ElementManager.colorCode[selectedElement], 0.5f);
        shapes.End();

        if (drawPosition && mouseVertical != null && !mouseVertical.IsDestroyed)
        {
            int x = Rubedo.Lib.Math.FloorToInt(mouse.X);
            int y = Rubedo.Lib.Math.FloorToInt(mouse.Y);
            WorldChunk chunk = this.world.GetChunk(x, y);
            string material = "???";
            int cellID = -1;
            int elementID = ElementManager.EMPTY;
            if (chunk != null)
            {
                cellID = chunk.GetCellIndex(in x, in y);
                elementID = chunk.element[cellID];
            }

            if (cellID != -1)
            {
                if (cellID == ElementManager.EMPTY)
                    material = "air";
                else
                    material = ElementManager.internalName[elementID];
            }

            Vector2 mouseScreen = InputManager.MouseScreenPosition(MainCamera);
            mouse = new Vector2(MathF.Floor(mouse.X), MathF.Floor(mouse.Y));
            mouseVertical.Offset = GUI.Root.ScreenToUI(mouseScreen + new Vector2(15, 0));

            Label world = mouseVertical.Children[0] as Label;
            world.Text = material + " - "+mouse.ToNiceString("0");

            if (drawCellDetails && elementID != ElementManager.EMPTY)
            {
                Velocity velocity = chunk.velocity[cellID];
                WorldChunk.Moving moving = chunk.moving[cellID];

                world.Text += $"\nVelocity: {velocity.ToNiceString("0.00")}" +
                    $"\nFreefalling: {moving.IsMoving}, {moving.MovingCount}";
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

        SpawnCell(leftClickCondition.Pressed(), leftClickCondition.Held(), selectedElement, ElementManager.liquid_isStatic[selectedElement] ? 100 : 35);
        SpawnCell(shiftLeftClickCondition.Pressed(), shiftLeftClickCondition.Held(), ElementManager.EMPTY, 101);

        if (middleClickCondition.Pressed())
        {
            Vector2 mouse = InputManager.MouseWorldPosition();
            int x = Rubedo.Lib.Math.FloorToInt(mouse.X);
            int y = Rubedo.Lib.Math.FloorToInt(mouse.Y);
            Entity entity = new Entity(new Vector2(x, y))
            {
                new Spout(world, selectedElement, brushSize)
            };
            Add(entity);
        }

        float rotateRate = 0.5f * Time.DeltaTime;
        float moveRate = 50 * Time.DeltaTime / MainCamera.Scale.X;

        if (cameraLeft.Pressed() || cameraLeft.Held())
            MainCamera.XY += new Vector2(-moveRate, 0);
        if (cameraRight.Pressed() || cameraRight.Held())
            MainCamera.XY += new Vector2(moveRate, 0);
        if (cameraUp.Pressed() || cameraUp.Held())
            MainCamera.XY += new Vector2(0, moveRate);
        if (cameraDown.Pressed() || cameraDown.Held())
            MainCamera.XY += new Vector2(0, -moveRate);
        if (cameraRotateCW.Pressed() || cameraRotateCW.Held())
            MainCamera.Rotation += rotateRate;
        if (cameraRotateCCW.Pressed() || cameraRotateCCW.Held())
            MainCamera.Rotation -= rotateRate;
        if (cameraScaleDown.Pressed() || cameraScaleDown.Held())
            MainCamera.Scale -= new Vector2(rotateRate, rotateRate);
        if (cameraScaleUp.Pressed() || cameraScaleUp.Held())
            MainCamera.Scale += new Vector2(rotateRate, rotateRate);
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
        if (toggleVSync.Pressed())
        {
            RubedoEngine.Graphics.SynchronizeWithVerticalRetrace = !RubedoEngine.Graphics.SynchronizeWithVerticalRetrace; //vsync
            RubedoEngine.Graphics.ApplyChanges();
        }
        if (toggleTPSCap.Pressed())
        {
            world.uncapUpdates = !world.uncapUpdates;
        }

        if (spawnCheckerboard.Pressed())
        {
            SpawnCheckerboard(false);
        }

        if (spawnRandomCheckerboard.Pressed())
        {
            SpawnCheckerboard(true);
        }
        if (spawnTopHalf.Pressed())
        {
            SpawnTopHalf(false);
        }
        if (spawnRandomTopHalf.Pressed())
        {
            SpawnTopHalf(true);
        }
        if (clearAll.Pressed())
        {
            ClearAll();
        }
    }

    private void SpawnCell(bool pressed, bool held, int type, int chance)
    {
        if (held || pressed)
        {
            if (held)
            {
                Vector2 mouse = InputManager.MouseWorldPosition();
                Point curPos = new Point(Rubedo.Lib.Math.FloorToInt(mouse.X), Rubedo.Lib.Math.FloorToInt(mouse.Y));
                Vector2 mousePrev = InputManager.PreviousMouseWorldPosition();
                Point prevPos = new Point(Rubedo.Lib.Math.FloorToInt(mousePrev.X), Rubedo.Lib.Math.FloorToInt(mousePrev.Y));
                world.IterateAndApplyBetweenPoints(prevPos, curPos, (x, y) => SpawnCellRun(x, y, type, chance, brushSize));
            }
            else
            {
                Vector2 mouse = InputManager.MouseWorldPosition();
                int x = Rubedo.Lib.Math.FloorToInt(mouse.X);
                int y = Rubedo.Lib.Math.FloorToInt(mouse.Y);
                SpawnCellRun(x, y, type, chance, brushSize);
            }
        }
    }

    private void SpawnCellRun(int m_x, int m_y, int type, int chance, int brushSize)
    {
        if (brushSize == 0)
        {
            if (type == ElementManager.EMPTY)
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
                    if (type == ElementManager.EMPTY)
                        world.ClearCell(m_x + x, m_y + y);
                    else
                        world.SpawnCell(type, m_x + x, m_y + y);
                }
            }
        }
    }

    public void SpawnCheckerboard(bool random)
    {
        int[] elements = new int[ElementManager.elementsByName.Values.Count];
        int b = 0;
        foreach(int el in ElementManager.elementsByName.Values)
        {
            elements[b++] = el;
        }
        int size = world.chunksPerRegion;
        for (int i = 0; i < world.regions.Count; i++)
        {
            WorldRegion region = world.regions[i];
            int xStart = 0;
            for (int y = 0; y < size; y++)
            {
                xStart = (xStart == 1 ? 0 : 1);
                for (int x = xStart; x < size; x+=2)
                {
                    WorldChunk chunk = region.GetChunk(region.RegionX + (x * world.chunkSize), region.RegionY + (y * world.chunkSize));
                    for (int z = 0; z < chunk.indexSize; z++)
                    {
                        world.SpawnCell(random ? elements[Rubedo.Lib.Random.Range(0, b)] : selectedElement, chunk, z);
                    }
                }
            }
        }
    }

    public void SpawnTopHalf(bool random)
    {
        int[] elements = new int[ElementManager.elementsByName.Values.Count];
        int b = 0;
        foreach (int el in ElementManager.elementsByName.Values)
        {
            elements[b++] = el;
        }
        int size = world.chunksPerRegion;
        int worldHalf = world.WorldRect.Bottom - (world.WorldRect.Height / 2);
        for (int i = 0; i < world.regions.Count; i++)
        {
            WorldRegion region = world.regions[i];
            for (int y = 0; y < size; y++)
            {
                int yLevel = region.RegionY + (y * world.chunkSize);
                if (yLevel < worldHalf)
                    continue;
                for (int x = 0; x < size; x++)
                {
                    WorldChunk chunk = region.GetChunk(region.RegionX + (x * world.chunkSize), yLevel);
                    for (int z = 0; z < chunk.indexSize; z++)
                    {
                        world.SpawnCell(random ? elements[Rubedo.Lib.Random.Range(0, b)] : selectedElement, chunk, z);
                    }
                }
            }
        }
    }

    public void ClearAll()
    {
        int size = world.chunksPerRegion;
        for (int i = 0; i < world.regions.Count; i++)
        {
            WorldRegion region = world.regions[i];
            for (int y = 0; y < size; y++)
            {
                int yLevel = region.RegionY + (y * world.chunkSize);
                for (int x = 0; x < size; x++)
                {
                    WorldChunk chunk = region.GetChunk(region.RegionX + (x * world.chunkSize), yLevel);
                    for (int z = 0; z < chunk.indexSize; z++)
                    {
                        world.ClearCell(chunk, z);
                    }
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