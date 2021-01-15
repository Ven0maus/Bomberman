using Bomberman.Client.GameObjects;
using SadConsole;
using Console = SadConsole.Console;

namespace Bomberman.Client.Graphics
{
    public class GridScreen : Console
    {
        public readonly Grid Grid;

        public GridScreen(int width, int height, Font font) 
            : base(width, height, font)
        {
            Grid = new Grid(width, height);

            // Initiate player
            new Player(0, 0)
            {
                Parent = this
            };

            // Set screen surface to grid match cells
            SetSurface(Grid.Cells, width, height);
        }
    }
}
