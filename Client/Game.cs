using Bomberman.Client.GameObjects;
using Bomberman.Client.Graphics;
using Microsoft.Xna.Framework;
using SadConsole;
using System;
using System.Collections.Generic;

namespace Bomberman.Client
{
    public class Game
    {
        internal static GameClient Client { get; set; }

        public const int GameWidth = 145;
        public const int GameHeight = 45;
        public const int GridWidth = 15;
        public const int GridHeight = 15;

        internal static bool Singleplayer { get; set; }

        internal static Random Random { get; private set; } = new Random();

        internal static Font Font { get; private set; }

        internal static GridScreen GridScreen { get; set; }
        internal static MainMenuScreen MainMenuScreen { get; private set; }
        internal static ClientWaitingLobby ClientWaitingLobby { get; set; }
        /// <summary>
        /// Can only be accessed in a singleplayer game
        /// </summary>
        internal static Player Player { get; set; }
        internal static List<BombermanBot> Bots { get; set; }

        private static void Main()
        {
            // Setup the engine and create the main window.
            SadConsole.Game.Create(GameWidth, GameHeight);

            // Hook the start event so we can add consoles to the system.
            SadConsole.Game.OnInitialize = Init;
            SadConsole.Game.OnUpdate = Update;

            // Start the game.
            SadConsole.Game.Instance.Run();
            SadConsole.Game.Instance.Dispose();
        }

        private static void Init()
        {
            // Initialize default keybindings
            KeybindingsManager.InitializeDefaultKeybindings();

            //Settings.AllowWindowResize = false;
            Settings.ResizeMode = Settings.WindowResizeOptions.Fit;

            var master = Global.LoadFont("Graphics/Textures/Tileset.font");
            Font = master.GetFont(Font.FontSizes.Three);

            SadConsole.Game.Instance.Window.Title = "Bomberman";
            InitializeMenuScreen();
        }

        private static void InitializeMenuScreen()
        {
            Global.CurrentScreen = MainMenuScreen = new MainMenuScreen(GameWidth, GameHeight);
        }

        internal static void InitializeGameScreen()
        {
            Global.CurrentScreen = GridScreen = new GridScreen(GridWidth, GridHeight, Font);
            GridScreen.IsFocused = true;

            if (!Singleplayer)
            {
                ClientWaitingLobby.IsVisible = false;
                ClientWaitingLobby.IsFocused = false;
            }
            else
            {
                // Initialize player & uncover starter tiles
                Player = new Player(GridScreen.Grid.GetAvailableSpawnPosition(), 0, GridScreen.Grid.GetAvailableColor(), true)
                {
                    Parent = GridScreen
                };

                // TODO: Initialize AI bots from screen interface options
                Bots = new List<BombermanBot>();
                for (int i=0; i < 3; i++)
                {
                    // 3 bots for testing
                    Bots.Add(new BombermanBot(GridScreen.Grid.GetAvailableSpawnPosition(), i + 1, GridScreen.Grid.GetAvailableColor()) { Parent = GridScreen });
                }

                // Uncover tiles around the player only to show where he has spawned
                GridScreen.Grid.UncoverTilesFromDarkness(Player.Position);
            }
        }

        internal static void Reset()
        {
            foreach (var bot in Bots)
                bot.Parent = null; // This undraws them from the screen
            // Reset variables and let them be cleared by gc
            Bots = null;
            Player = null;

            // Move screen to main menu
            GridScreen.IsVisible = false;
            GridScreen.IsFocused = false;
            MainMenuScreen.IsVisible = true;
            MainMenuScreen.IsFocused = true;
            Global.CurrentScreen = MainMenuScreen;
        }

        internal static void Update(GameTime gameTime)
        {
            // Will be null for single player games
            Client?.Update(gameTime);
        }
    }
}
