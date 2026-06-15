using FallingSand.Game.Elements;
using FallingSand.Game.States;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rubedo;
using Rubedo.Resources;
using Rubedo.UI;
using Rubedo.UI.Graphics;
using Rubedo.UI.Input;
using Rubedo.UI.Layout;
using Rubedo.UI.Text;

namespace FallingSand.Game.UI;

/// <summary>
/// TODO: I am SideBar, and I don't have a summary yet.
/// </summary>
public class ElementSideBar
{
    public ElementSideBar(WorldState state)
    {
        Vertical vertical = new Vertical();
        vertical.SetAnchorAndOffset(Anchor.TopRight, new Vector2(5, 5));
        vertical.childPadding = 5;

        Element element1 = null;
        foreach (Element element in ElementManager.elementsByName.Values)
        {
            if (state.selectedElement == null)
                state.selectedElement = element;

            if (element1 == null)
            {
                element1 = element;
                continue;
            }
            else
            {
                Horizontal horizontal = new Horizontal();
                horizontal.childPadding = 10;
                CreateElementButton(horizontal, element1, state);
                CreateElementButton(horizontal, element, state);
                vertical.AddChild(horizontal);
                element1 = null;
            }
        }
        if (element1 != null)
        {
            Horizontal horizontal = new Horizontal();
            CreateElementButton(horizontal, element1, state);
            vertical.AddChild(horizontal);
        }

        GUI.Root.AddChild(vertical);
    }

    private void CreateElementButton(Horizontal horz, Element element, WorldState state)
    {
        Panel panel = new Panel(32, 48);
        Button button = new Button();

        button.OnReleased += (b) =>
        {
            state.selectedElement = element;
            state.leftClickCondition.Consume();
        };

        button.OnPressed += (b) =>
        {
            state.leftClickCondition.Consume();
        };
        button.OnHeld += (b) =>
        {
            state.leftClickCondition.Consume();
        };

        Image image = Image.CreateSolidColorImage(32, 32, element.color);
        button.AddChild(image);
        //button.AddChild(new SelectableTintSet(image, 1f));

        FontSystem font = Assets.GetFont("fs-default");
        Label text = new Label(font, element.internalName, Color.White, 12);
        text.MaxSize = new Vector2(64, -1);
        text.Anchor = Anchor.BottomLeft;
        text.Offset = new Vector2(0, 4);
        panel.AddChild(button);
        panel.AddChild(text);

        horz.AddChild(panel);
    }
}