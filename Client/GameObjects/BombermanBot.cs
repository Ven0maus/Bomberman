using Microsoft.Xna.Framework;

namespace Bomberman.Client.GameObjects
{
    /// <summary>
    /// Class for the bomberman AI bots
    /// </summary>
    public class BombermanBot : Player
    {
        public BombermanBot(Point position, int id, Color color) : base(position, id, color, false)
        { }
    }
}
