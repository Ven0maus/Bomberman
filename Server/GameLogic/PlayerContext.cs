using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.GameLogic
{
    public class PlayerContext
    {
        public int Id;
        public int MaxBombs = 1;
        public int BombStrength = 1;
        public int BombsPlaced { get; set; }

        public Point Position { get; set; }

        public PlayerContext(Point position, int id)
        {
            Id = id;
            Position = position;
        }
    }
}
