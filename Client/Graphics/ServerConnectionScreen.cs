using SadConsole;
using SadConsole.Controls;
using Microsoft.Xna.Framework;
using SadConsole.Themes;
using System.Linq;
using System;

namespace Bomberman.Client.Graphics
{
    public class ServerConnectionScreen : ControlsConsole, IErrorLogger
    {
        private readonly int _width, _height;
        private readonly TextBox _serverIpBox, _serverPortBox, _playerName;

        public bool Connecting = false;

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

            _playerName = new TextBox(20)
            {
                Position = new Point((_width / 2) - 5, (_height / 2) - 5),
                Name = "Player-name:",
                UseKeyboard = true,
                AllowDecimal = true,
                Text = "Test"
            };
            Add(_playerName);

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

            if (!string.IsNullOrWhiteSpace(_errorMessage))
                Print(_width / 2 - (_errorMessage.Length / 2), _playerName.Position.Y - 2, new ColoredString(_errorMessage, Color.Red, Color.Transparent));
        }

        private void WriteInputBoxNames()
        {
            if (_serverIpBox != null)
                Print(_serverIpBox.Position.X - 14, _serverIpBox.Position.Y, _serverIpBox.Name, Color.White);
            if (_serverPortBox != null)
                Print(_serverPortBox.Position.X - 14, _serverPortBox.Position.Y, _serverPortBox.Name, Color.White);
            if (_playerName != null)
                Print(_playerName.Position.X - 14, _playerName.Position.Y, _playerName.Name, Color.White);
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

        private void BackButton_Click(object sender, EventArgs e)
        {
            Game.MainMenuScreen.IsVisible = true;
            Global.CurrentScreen = Game.MainMenuScreen;
            IsVisible = false;
            IsFocused = false;
        }

        private double _timePassed = 0f;
        private double _timePassedMsg = 0f;
        private string _errorMessage;
        public override void Update(TimeSpan time)
        {
            base.Update(time);

            if (_errorMessage != null)
            {
                _timePassedMsg += time.Milliseconds;
                if (_timePassedMsg >= 5000)
                {
                    _timePassedMsg = 0;
                    _errorMessage = null;
                }
            }

            if (Connecting)
            {
                _timePassed += time.Milliseconds;
                if (_timePassed >= 5000)
                {
                    _timePassed = 0f;
                    Connecting = false;

                    // Show server connection screen again
                    IsVisible = true;
                    Global.CurrentScreen = this;
                    IsFocused = true;
                }
            }
        }

        public void ShowError(string message)
        {
            _errorMessage = message;
            _timePassedMsg = 0;
            Invalidate();
        }

        public void ClearError()
        {
            _errorMessage = null;
            _timePassedMsg = 0;
            Invalidate();
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(_serverPortBox.Text, out int port))
            {
                ShowError("Invalid port, should be numbers only.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_playerName.Text))
            {
                ShowError("Player name cannot be empty.");
                return;
            }

            Game.Client = new GameClient(_serverIpBox.Text, port, _playerName.Text);
            if (Game.Client.Connect())
            {
                // Server will send player to the client waiting lobby
                // A timer is automatically started, incase server packet isn't received by client
                Connecting = true;
            }
            else
            {
                ShowError("No response from the server.");
            }
        }
    }
}
