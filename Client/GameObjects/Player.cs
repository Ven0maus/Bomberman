using Bomberman.Client.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SadConsole.Entities;
using System;

namespace Bomberman.Client.GameObjects
{
    public class Player : Entity
    {
        // Powerups
        public int MaxBombs = 1;
        public int BombStrength = 1;
        public int BombsPlaced { get; set; }

        public bool Alive;

        public static Player Instance { get; private set; }

        internal Player(Point position) : base(Color.White, Color.Transparent, 18)
        {
            Alive = true;
            Position = position;
            Font = Game.Font;

            IsFocused = true;

            if (Instance != null)
                Instance.Parent = null;
            Instance = this;
        }

        internal Player(int x, int y) : this(new Point(x, y))
        { }

        public override bool ProcessKeyboard(SadConsole.Input.Keyboard info)
        {
            if (!Alive)
            {
                IsFocused = false;
                return true;
            }

            if (info.IsKeyPressed(Keys.Z) && Game.GridScreen.Grid.CanMove(Position.X, Position.Y - 1))
            {
                Animation[0].Glyph = 19;
                Position += new Point(0, -1);
                Animation.IsDirty = true;
                CheckPowerup();
                return true;
            }
            else if (info.IsKeyPressed(Keys.S) && Game.GridScreen.Grid.CanMove(Position.X, Position.Y + 1))
            {
                Animation[0].Glyph = 18;
                Position += new Point(0, 1);
                Animation.IsDirty = true;
                CheckPowerup();
                return true;
            }
            else if (info.IsKeyPressed(Keys.Q) && Game.GridScreen.Grid.CanMove(Position.X - 1, Position.Y))
            {
                Animation[0].Glyph = 17;
                Position += new Point(-1, 0);
                Animation.IsDirty = true;
                CheckPowerup();
                return true;
            }
            else if (info.IsKeyPressed(Keys.D) && Game.GridScreen.Grid.CanMove(Position.X + 1, Position.Y))
            {
                Animation[0].Glyph = 16;
                Position += new Point(1, 0);
                Animation.IsDirty = true;
                CheckPowerup();
                return true;
            }
            else if (info.IsKeyPressed(Keys.Space) && BombsPlaced < MaxBombs)
            {
                if (Game.GridScreen.Grid.PlaceBomb(Position, BombStrength))
                    BombsPlaced += 1;
            }

            return base.ProcessKeyboard(info);
        }

        private void CheckPowerup()
        {
            var cell = Game.GridScreen.Grid.GetValue(Position.X, Position.Y);
            if (cell.HasFire)
            {
                Game.GameOver();
                return;
            }

            if (cell.PowerUp == PowerUp.None) return;

            switch (cell.PowerUp)
            {
                case PowerUp.ExtraBomb:
                    MaxBombs++;
                    break;
                case PowerUp.BombStrength:
                    BombStrength++;
                    break;
            }

            cell.PowerUp = PowerUp.None;
            cell.Explored = true;
            Game.GridScreen.IsDirty = true;
        }
    }
}
