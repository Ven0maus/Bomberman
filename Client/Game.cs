using Bomberman.Client.GameObjects;
using Bomberman.Client.Graphics;
using Microsoft.Xna.Framework;
using SadConsole;
using System;

namespace Bomberman.Client
{
    public class Game
    {
        public static GameClient Client { get; set; }

        public const int GameWidth = 145;
        public const int GameHeight = 45;
        public const int GridWidth = 15;
        public const int GridHeight = 15;

        public static bool Singleplayer { get; set; }

        public static Random Random { get; private set; } = new Random();

        public static Font Font { get; private set; }

        public static GridScreen GridScreen { get; set; }
        public static MainMenuScreen MainMenuScreen { get; private set; }
        public static ClientWaitingLobby ClientWaitingLobby { get; set; }
        /// <summary>
        /// Can only be accessed in a singleplayer game
        /// </summary>
        public static Player Player { get; set; }

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

        public static void InitializeGameScreen()
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

                // TODO: Initialize AI bots

                // Uncover tiles around the player only to show where he has spawned
                GridScreen.Grid.UncoverTilesFromDarkness(Player.Position);
            }
        }

        internal static void Reset()
        {
            GridScreen.IsVisible = false;
            GridScreen.IsFocused = false;
            MainMenuScreen.IsVisible = true;
            MainMenuScreen.IsFocused = true;
            Global.CurrentScreen = MainMenuScreen;
        }

        public static void Update(GameTime gameTime)
        {
            // Will be null for single player games
            Client?.Update(gameTime);
        }
    }
}
