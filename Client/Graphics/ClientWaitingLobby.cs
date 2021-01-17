using Microsoft.Xna.Framework;
using SadConsole;
using SadConsole.Themes;
using System.Collections.Generic;
using SadConsole.Controls;
using System.Linq;

namespace Bomberman.Client.Graphics
{
    public class ClientWaitingLobby : ControlsConsole
    {
        private readonly int _width, _height;

        private Dictionary<int, string> _playerSlots;

        private bool _ready;
        private Button _readyUpButton;

        public ClientWaitingLobby(int width, int height) : base(width, height) 
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

            _playerSlots = new Dictionary<int, string>();
            for (int i = 0; i < 8; i++)
            {
                _playerSlots.Add(i, null);
            }

            AddButtons();
        }

        public void AddButtons()
        {
            var yCoord = (_height / 2) + 14;
            var leaveLobbyButton = new Button(15, 3)
            {
                Text = "Leave lobby",
                Position = new Point((_width / 2) - 14, yCoord),
                UseMouse = true,
                UseKeyboard = false,
            };
            leaveLobbyButton.Click += LeaveLobbyButton_Click;
            Add(leaveLobbyButton);

            _readyUpButton = new Button(12, 3)
            {
                Text = "Ready up",
                Position = new Point((_width / 2) + 2, yCoord),
                UseMouse = true,
                UseKeyboard = false,
            };
            _readyUpButton.Click += ReadyUpButton_Click;
            Add(_readyUpButton);
        }

        private void ReadyUpButton_Click(object sender, System.EventArgs e)
        {
            _ready = !_ready;
            _readyUpButton.Text = _ready ? "Unready" : "Ready up";

            // Let server know that we readied or unreadied
            Game.Client.SendPacket(Game.Client.Client, new ServerSide.Packet("ready", _ready ? "1" : "0"));
        }

        private void LeaveLobbyButton_Click(object sender, System.EventArgs e)
        {
            Game.Client.Disconnect();
            Game.MainMenuScreen.IsVisible = true;
            Game.MainMenuScreen.IsFocused = true;
            Global.CurrentScreen = Game.MainMenuScreen;
            RemovePlayer(Game.Client.PlayerName);
            IsVisible = false;
            IsFocused = false;
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

            Print((_width / 2) - ("Waiting lobby.".Length / 2), startPosY + titleFragments.Length + 1, "Waiting lobby.");
        }

        protected override void Invalidate()
        {
            base.Invalidate();
            PrintTitle();

            if (_playerSlots != null)
                ShowPlayerSlots();
        }

        public void ShowPlayerSlots()
        {
            int yCoord = (_height / 2) - 4;
            foreach (var slot in _playerSlots)
            {
                if (slot.Value != null)
                {
                    Print((_width / 2) - 10, yCoord, $"Slot [{slot.Key + 1}]: {slot.Value}", Color.White);
                }
                else
                {
                    Print((_width / 2) - 10, yCoord, $"Slot [{slot.Key + 1}]: Available", Color.White);
                }
                yCoord += 2;
            }
        }

        public void RemovePlayer(string playerName)
        {
            var slot = _playerSlots.FirstOrDefault(a => a.Value.Equals(playerName, System.StringComparison.OrdinalIgnoreCase));
            _playerSlots[slot.Key] = null;
            Invalidate();
        }

        public void AddPlayer(string playerName)
        {
            foreach (var slot in _playerSlots)
            {
                if (slot.Value == null)
                {
                    _playerSlots[slot.Key] = playerName;
                    Invalidate();
                    break;
                }
            }
        }
    }
}
