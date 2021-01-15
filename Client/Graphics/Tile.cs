using Bomberman.Client.GameObjects;
using Microsoft.Xna.Framework;
using SadConsole;
using System.Collections.Generic;

namespace Bomberman.Client.Graphics
{
    public enum PowerUp
    {
        None,
        ExtraBomb,
        BombStrength
    }

    public class Tile : Cell
    {
        public readonly Point Position;

        private bool _explored;
        public bool Explored 
        { 
            get { return _explored; } 
            set 
            {
                if (!Destroyable) return;

                _explored = value;
                Glyph = value ? 0 : 2;
                Foreground = value ? Color.DarkBlue : Color.White;
            } 
        }

        public bool HasFire { get { return Glyph == 4; } }

        private List<Bomb> _containsFireFrom;
        public List<Bomb> ContainsFireFrom { get { return _containsFireFrom ??= new List<Bomb>(); } }

        public bool HasBomb;

        public PowerUp PowerUp;

        public bool Destroyable { get; private set; }

        public Tile(Point position, bool destroyable = true)
        {
            Position = position;
            Destroyable = destroyable;
            _explored = !destroyable;
            Glyph = destroyable ? 2 : 1;
            Background = Color.DarkBlue;

            // Small chance to contain a random powerup
            if (Game.Random.Next(0, 101) <= 15)
            {
                PowerUp = (PowerUp)Game.Random.Next(1, 3);
            }
        }

        public Tile(int x, int y, bool destroyable = true) : this(new Point(x, y), destroyable)
        { }
    }
}
