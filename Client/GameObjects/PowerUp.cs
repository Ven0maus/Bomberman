using Bomberman.Client.Graphics;
using Microsoft.Xna.Framework;
using SadConsole.Entities;

namespace Bomberman.Client.GameObjects
{
    public class PowerUpVisual : Entity
    {
        public PowerUp PowerUp { get; private set; }

        public PowerUpVisual(Point position, PowerUp powerUp) : base(Color.White, Color.Transparent, 0)
        {
            Font = Game.Font;
            Position = position;
            PowerUp = powerUp;
            switch (powerUp)
            {
                case PowerUp.ExtraBomb:
                    Animation[0].Glyph = 5;
                    break;
                case PowerUp.BombStrength:
                    Animation[0].Glyph = 6;
                    break;
                case PowerUp.Invicibility:
                    Animation[0].Glyph = 7;
                    break;
            }
            Animation.IsDirty = true;
        }
    }
}
