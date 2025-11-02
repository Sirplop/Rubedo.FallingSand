using FallingSand.Game.States;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace FallingSand;

public class FallingSand : Rubedo.RubedoEngine
{
    public FallingSand() : base()
    {
        Graphics.SynchronizeWithVerticalRetrace = true; //vsync
    }

    protected override void LoadContent()
    {
        base.LoadContent();
        _stateManager.AddState(new WorldState(_stateManager));

        _stateManager.SwitchState("WorldState");
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        base.Draw(gameTime);
    }
}
