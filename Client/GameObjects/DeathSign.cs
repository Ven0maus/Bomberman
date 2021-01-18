using Microsoft.Xna.Framework;
using SadConsole.Entities;

namespace Bomberman.Client.GameObjects
{
    public class DeathSign : Entity
    {
        public DeathSign(Point position, Color color) : base(Color.White, Color.Transparent, 20)
        {
            Font = Game.Font;
            Position = position;
            Animation[0].Foreground = color;
            Animation.IsDirty = true;
        }
    }
}
