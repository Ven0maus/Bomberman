using Microsoft.Xna.Framework;
using SadConsole;
using SadConsole.Themes;
using System.Linq;
using SadConsole.Controls;
using System;

namespace Bomberman.Client.Graphics
{
    public class MainMenuScreen : ControlsConsole
    {
        public ServerConnectionScreen ServerConnectionScreen { get; private set; }
        public OptionsScreen KeybindingsScreen { get; private set; }

        private readonly int _width, _height;
        public MainMenuScreen(int width, int height) : base(width, height)
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

            // Add buttons
            InitializeButtons();
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
        }

        public void InitializeButtons()
        {
            var singlePlayerButton = new Button(20, 3)
            {
                Text = "Singleplayer",
                Position = new Point((_width / 2) - 10, (_height / 2) - 4),
                UseMouse = true,
                UseKeyboard = false,
            };
            singlePlayerButton.Click += SinglePlayerButton_Click;
            Add(singlePlayerButton);

            var multiPlayerButton = new Button(20, 3)
            {
                Text = "Multiplayer",
                Position = new Point((_width / 2) - 10, (_height / 2)),
                UseMouse = true,
                UseKeyboard = false,
            };
            multiPlayerButton.Click += MultiPlayerButton_Click;
            Add(multiPlayerButton);

            var keybindingsButton = new Button(20, 3)
            {
                Text = "Keybindings",
                Position = new Point((_width / 2) - 10, (_height / 2) + 4),
                UseMouse = true,
                UseKeyboard = false,
            };
            keybindingsButton.Click += KeybindingsButton_Click; ;
            Add(keybindingsButton);

            var exitButton = new Button(20, 3)
            {
                Text = "Exit",
                Position = new Point((_width / 2) - 10, (_height / 2) + 8),
                UseMouse = true,
                UseKeyboard = false,
            };
            exitButton.Click += ExitButton_Click;
            Add(exitButton);
        }

        private void KeybindingsButton_Click(object sender, EventArgs e)
        {
            if (KeybindingsScreen == null)
                KeybindingsScreen = new OptionsScreen(_width, _height);
            KeybindingsScreen.IsVisible = true;
            IsVisible = false;
            Global.CurrentScreen = KeybindingsScreen;
            KeybindingsScreen.IsFocused = true;
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void MultiPlayerButton_Click(object sender, EventArgs e)
        {
            // Show server connection screen
            if (ServerConnectionScreen == null)
                ServerConnectionScreen = new ServerConnectionScreen(_width, _height);
            ServerConnectionScreen.IsVisible = true;
            IsVisible = false;
            Global.CurrentScreen = ServerConnectionScreen;
            ServerConnectionScreen.IsFocused = true;
        }

        private void SinglePlayerButton_Click(object sender, EventArgs e)
        {
            // Not for alpha release
            throw new NotImplementedException();
        }
    }
}
