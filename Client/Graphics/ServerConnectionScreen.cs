using SadConsole;
using SadConsole.Controls;
using Microsoft.Xna.Framework;
using SadConsole.Themes;
using System.Linq;

namespace Bomberman.Client.Graphics
{
    public class ServerConnectionScreen : ControlsConsole
    {
        private readonly int _width, _height;
        private readonly TextBox _serverIpBox, _serverPortBox;

        public ServerConnectionScreen(int width, int height) : base(width, height)
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

            _serverIpBox = new TextBox(20)
            {
                Position = new Point((_width / 2) - 5, (_height / 2) - 3),
                Name = "Server-ip:",
                UseKeyboard = true,
                AllowDecimal = true,
                Text = "127.0.0.1"
            };
            Add(_serverIpBox);

            _serverPortBox = new TextBox(20)
            {
                Position = new Point((_width / 2) - 5, (_height / 2) - 1),
                Name = "Server-port:",
                UseKeyboard = true,
                AllowDecimal = true,
                Text = "25565"
            };
            Add(_serverPortBox);

            IsFocused = true;

            AddButtons();
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

        protected override void OnInvalidate()
        {
            base.OnInvalidate();
            PrintTitle();
            WriteInputBoxNames();
        }

        private void WriteInputBoxNames()
        {
            if (_serverIpBox != null)
                Print(_serverIpBox.Position.X - 14, _serverIpBox.Position.Y, _serverIpBox.Name, Color.White);
            if (_serverPortBox != null)
                Print(_serverPortBox.Position.X - 14, _serverPortBox.Position.Y, _serverPortBox.Name, Color.White);
        }

        private void AddButtons()
        {
            var connectButton = new Button(12, 3)
            {
                Text = "Connect",
                Position = new Point((_width / 2) + 4, (_height / 2) + 1),
                UseMouse = true,
                UseKeyboard = false,
            };
            connectButton.Click += ConnectButton_Click;
            Add(connectButton);

            var backButton = new Button(10, 3)
            {
                Text = "Back",
                Position = new Point((_width / 2) - 6, (_height / 2) + 1),
                UseMouse = true,
                UseKeyboard = false,
            };
            backButton.Click += BackButton_Click;
            Add(backButton);
        }

        private void BackButton_Click(object sender, System.EventArgs e)
        {
            Game.MainMenuScreen.IsVisible = true;
            Global.CurrentScreen = Game.MainMenuScreen;
            IsVisible = false;
            IsFocused = false;
        }

        private void ConnectButton_Click(object sender, System.EventArgs e)
        {
            if (!int.TryParse(_serverPortBox.Text, out int port))
            {
                // TODO: Show invalid port error message
                return;
            }

            Game.Client = new GameClient(_serverIpBox.Text, port);
            if (Game.Client.Connect())
            {
                IsFocused = false;
                IsVisible = false;
                Game.InitializeGameScreen();
            }
            else
            {
                // TODO: Show message that connection was not possible
            }
        }
    }
}
