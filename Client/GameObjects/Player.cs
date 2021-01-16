using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SadConsole.Entities;

namespace Bomberman.Client.GameObjects
{
    public class Player : Entity
    {
        public int Id;
        // Powerups
        public int MaxBombs = 1;
        public int BombStrength = 1;
        public int BombsPlaced { get; set; }

        public bool Alive;

        public bool RequestedMovement { get; set; }
        public bool RequestBombPlacement { get; set; }

        public bool _controllable;

        public Player(Point position, int id, bool controllable = true) : base(Color.White, Color.Transparent, 18)
        {
            Id = id;
            _controllable = controllable;
            Alive = true;
            Position = position;
            Font = Game.Font;
            Moved += Player_Moved;
            IsFocused = _controllable;
        }

        private void Player_Moved(object sender, EntityMovedEventArgs e)
        {
            var previous = e.FromPosition;
            var current = Position;

            int diffX = current.X - previous.X;
            int diffY = current.Y - previous.Y;

            // Right
            if (diffX == 1 && diffY == 0)
            {
                if (Animation[0].Glyph != 16)
                {
                    Animation[0].Glyph = 16;
                    Animation.IsDirty = true;
                }
            }
            // Left
            else if (diffX == -1 && diffY == 0)
            {
                if (Animation[0].Glyph != 17)
                {
                    Animation[0].Glyph = 17;
                    Animation.IsDirty = true;
                }
            }
            // Up
            else if (diffX == 0 && diffY == -1)
            {
                if (Animation[0].Glyph != 19)
                {
                    Animation[0].Glyph = 19;
                    Animation.IsDirty = true;
                }
            }
            // Down
            else if (diffX == 0 && diffY == 1)
            {
                if (Animation[0].Glyph != 18)
                {
                    Animation[0].Glyph = 18;
                    Animation.IsDirty = true;
                }
            }
        }

        public override bool ProcessKeyboard(SadConsole.Input.Keyboard info)
        {
            if (!_controllable) return base.ProcessKeyboard(info);
            if (!Alive)
            {
                IsFocused = false;
                return base.ProcessKeyboard(info);
            }

            if (info.IsKeyPressed(Keys.Z))
            {
                if (!RequestedMovement)
                {
                    RequestedMovement = true;
                    var targetPosition = Position + new Point(0, -1);
                    Game.Client.SendPacket(Game.Client.Client, new Packet("move", $"{targetPosition.X}:{targetPosition.Y}"));
                }
                return true;
            }
            else if (info.IsKeyPressed(Keys.S))
            {
                if (!RequestedMovement)
                {
                    RequestedMovement = true;
                    var targetPosition = Position + new Point(0, 1);
                    Game.Client.SendPacket(Game.Client.Client, new Packet("move", $"{targetPosition.X}:{targetPosition.Y}"));
                }
                return true;
            }
            else if (info.IsKeyPressed(Keys.Q))
            {
                if (!RequestedMovement)
                {
                    RequestedMovement = true;
                    var targetPosition = Position + new Point(-1, 0);
                    Game.Client.SendPacket(Game.Client.Client, new Packet("move", $"{targetPosition.X}:{targetPosition.Y}"));
                }
                return true;
            }
            else if (info.IsKeyPressed(Keys.D))
            {
                if (!RequestedMovement)
                {
                    RequestedMovement = true;
                    var targetPosition = Position + new Point(1, 0);
                    Game.Client.SendPacket(Game.Client.Client, new Packet("move", $"{targetPosition.X}:{targetPosition.Y}"));
                }
                return true;
            }
            else if (info.IsKeyPressed(Keys.Space))
            {
                if (!RequestBombPlacement)
                {
                    RequestBombPlacement = true;
                    Game.Client.SendPacket(Game.Client.Client, new Packet("placebomb"));
                }
                return true;
            }

            return base.ProcessKeyboard(info);
        }
    }
}
