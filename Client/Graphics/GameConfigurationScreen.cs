using Microsoft.Xna.Framework;
using SadConsole;
using SadConsole.Themes;
using SadConsole.Controls;
using System.Linq;

namespace Bomberman.Client.Graphics
{
    public class GameConfigurationScreen : ControlsConsole
    {
        private readonly int _width, _height;

        public GameConfigurationScreen(int width, int height) : base(width, height)
        {
            _width = width;
            _height = height;

            var colors = Colors.CreateDefault();
            colors.ControlBack = Color.Black;
            colors.Text = Color.White;
            Library.Default.SetControlTheme(typeof(Button), new ButtonLinesTheme());
            colors.RebuildAppearances();

            // Set the new theme colors         
            ThemeColors = colors;

            AddButtons();
        }

        protected override void OnInvalidate()
        {
            base.OnInvalidate();

            PrintTitle();
        }

        public void PrintTitle()
        {
            string[] titleFragments = @"
__________              ___.                                       
\______   \ ____   _____\_ |__   ___________  _____ _____    ____  
 |    |  _//  _ \ /     \| __ \_/ __ \_  __ \/     \\__  \  /    \ 
 |    |   (  <_> )  Y Y  \ \_\ \  ___/|  | \/  Y Y  \/ __ \|   |  \
 |______  /\____/|__|_|  /___  /\___  >__|  |__|_|  (____  /___|  /
        \/             \/    \/     \/            \/     \/     \/".Replace("\r", string.Empty).Split('\n'); ;

            int startPosX = (_width / 2) - (titleFragments.OrderByDescending(a => a.Length).First().Length / 2);
            int startPosY = 4;

            // Print title fragments
            for (int y = 0; y < titleFragments.Length; y++)
            {
                for (int x = 0; x < titleFragments[y].Length; x++)
                {
                    Print(startPosX + x, startPosY + y, new ColoredGlyph(titleFragments[y][x], Color.White, Color.Transparent));
                }
            }

            Print((_width / 2) - ("Singleplayer game setup.".Length / 2), startPosY + titleFragments.Length + 1, "Singleplayer game setup.");
        }

        public void AddButtons()
        {
            var startGameButton = new Button(20, 3)
            {
                Text = "Start Game",
                Position = new Point((_width / 2) - 10, (_height / 2) + 8),
                UseMouse = true,
                UseKeyboard = false,
            };
            startGameButton.Click += StartGameButton_Click;
            Add(startGameButton);

            var exitButton = new Button(20, 3)
            {
                Text = "Back",
                Position = new Point((_width / 2) - 10, (_height / 2) + 12),
                UseMouse = true,
                UseKeyboard = false,
            };
            exitButton.Click += BackButton_Click;
            Add(exitButton);
        }

        private void StartGameButton_Click(object sender, System.EventArgs e)
        {
            Game.InitializeGameScreen(false);
        }

        private void BackButton_Click(object sender, System.EventArgs e)
        {
            IsVisible = false;
            IsFocused = false;
            Game.MainMenuScreen.IsVisible = true;
            Game.MainMenuScreen.IsFocused = true;
            Global.CurrentScreen = Game.MainMenuScreen;
        }
    }
}
