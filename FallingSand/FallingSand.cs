using FallingSand.Game.States;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rubedo;

namespace FallingSand;

public class FallingSand : Rubedo.RubedoEngine
{
    public FallingSand() : base()
    {
        Graphics.SynchronizeWithVerticalRetrace = true; //vsync
        Time.SetFixedDeltaTime(1f / 60f);
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
