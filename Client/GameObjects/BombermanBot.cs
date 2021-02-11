using Microsoft.Xna.Framework;
using System;

namespace Bomberman.Client.GameObjects
{
    /// <summary>
    /// Class for the bomberman AI bots
    /// </summary>
    public class BombermanBot : Player
    {
        public BombermanBot(Point position, int id, Color color) : base(position, id, color, false)
        { }

        public override void Update(TimeSpan timeElapsed)
        {
            base.Update(timeElapsed);

            // TODO: Add AI processing logic each frame
        }
    }
}
