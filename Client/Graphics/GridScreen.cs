using Bomberman.Client.GameObjects;
using Microsoft.Xna.Framework;
using SadConsole;
using System.Collections.Generic;
using Console = SadConsole.Console;

namespace Bomberman.Client.Graphics
{
    public class GridScreen : Console
    {
        public readonly Grid Grid;

        private readonly Dictionary<int, Player> _stats = new Dictionary<int, Player>();
        private readonly Dictionary<int, int> _characterIndex = new Dictionary<int, int>();
        private readonly Console _textConsole;
        private readonly Console _charactersConsole;

        public GridScreen(int width, int height, Font font) 
            : base(width, height, font)
        {
            Grid = new Grid(width, height, Game.Singleplayer);

            _charactersConsole = new Console(6, Game.GridHeight)
            {
                Parent = this,
                Position = new Point(90, 0),
                Font = Game.Font,
            };

            for (int x = 0; x < _charactersConsole.Width; x++)
            {
                for (int y = 0; y < _charactersConsole.Height; y++)
                {
                    _charactersConsole.SetForeground(x, y, Color.Black);
                }
            }

            _textConsole = new Console(40, Game.GameHeight)
            {
                Parent = this,
                Position = new Point(102, 0),
            };

            // Set screen surface to grid match cells
            SetSurface(Grid.Cells, width, height);
        }

        private void UpdateStats()
        {
            int amount = 0;
            int count = 1;
            _textConsole.Clear();
            foreach (var stat in _stats)
            {
                var index = _characterIndex[stat.Key];
                _textConsole.Print(count <= 4 ? 1 : 25, index + 2 + (amount * 4), new ColoredString(stat.Value.Name));
                _textConsole.Print(count <= 4 ? 1 : 25, index + 3 + (amount * 4), new ColoredString("Kills: " + stat.Value.Kills, Color.Gray, Color.Black));
                _textConsole.Print(count <= 4 ? 1 : 25, index + 4 + (amount * 4), new ColoredString("Bombs: " + stat.Value.MaxBombs, Color.Gray, Color.Black));
                _textConsole.Print(count <= 4 ? 1 : 25, index + 5 + (amount * 4), new ColoredString("Strength: " + stat.Value.BombStrength, Color.Gray, Color.Black));
                count++;
                amount++;
                if (amount == 4)
                    amount = 0;
            }
        }

        public void PlayerKilled(Player player)
        {
            var index = _characterIndex[player.Id];
            _charactersConsole.SetBackground(_characterIndex.Count <= 4 ? 1 : 5, index, Color.Red);
            _charactersConsole.IsDirty = true;
        }

        private int _currentCharacterIndex = 1;
        private void AddCharacter(Player player)
        {
            _characterIndex.Add(player.Id, _currentCharacterIndex);
            _charactersConsole.SetGlyph(_characterIndex.Count <= 4 ? 1 : 5, _currentCharacterIndex, 18);
            _charactersConsole.SetForeground(_characterIndex.Count <= 4 ? 1 : 5, _currentCharacterIndex, player.Color);
            _charactersConsole.SetBackground(_characterIndex.Count <= 4 ? 1 : 5, _currentCharacterIndex, Color.White);
            _currentCharacterIndex += 2;
            if (_characterIndex.Count == 4)
                _currentCharacterIndex = 1;
        }

        public void ShowStats(int playerId, int kills = 0, int? bombs = null, int? strength = null)
        {
            if (_stats.TryGetValue(playerId, out Player player))
            {
                player.Kills = kills;
                if (bombs != null)
                    player.MaxBombs = bombs.Value;
                if (strength != null)
                    player.BombStrength = strength.Value;
            }
            else
            {
                player = Game.Client.GetPlayerById(playerId);
                if (player != null)
                {
                    _stats.Add(playerId, player);
                    player.Kills = kills;
                    if (bombs != null)
                        player.MaxBombs = bombs.Value;
                    if (strength != null)
                        player.BombStrength = strength.Value;

                    AddCharacter(player); 
                }
            }

            UpdateStats();
        }

        public void ClearStats()
        {
            _stats.Clear();
        }
    }
}
