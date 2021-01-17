using Bomberman.Client.Graphics;
using Microsoft.Xna.Framework;
using SadConsole.Entities;
using System;
using System.Collections.Generic;

namespace Bomberman.Client.GameObjects
{
    public class Bomb : Entity
    {
        public int Id;
        protected readonly int _strength;
        private readonly Grid _grid;

        protected readonly Player _placedBy;

        public Bomb(Player placedBy, Point position, int strength, int id) : base(Color.White, Color.Transparent, 3)
        {
            Id = id;
            Position = position;
            Font = Game.Font;
            _placedBy = placedBy;
            _strength = strength;
            _grid = Game.GridScreen.Grid;
            _grid.GetValue(position.X, position.Y).HasBomb = true;
        }

        public void CleanupFireAfter(List<Point> points)
        {
            // Remove fire cells
            foreach (var pos in points)
            {
                var cell = _grid.GetValue(pos.X, pos.Y);
                _grid.Explore(cell.Position.X, cell.Position.Y);
            }

            Game.GridScreen.IsDirty = true;
            Parent = null;
        }

        public void Detonate()
        {
            Animation[0].Foreground = Color.Transparent;
            Animation.IsDirty = true;
            Parent = null;
            Game.GridScreen.IsDirty = true;
        }
    }
}
