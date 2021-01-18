using Bomberman.Client.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SadConsole;
using System;

namespace Bomberman.Client
{
    public class Game
    {
        public static GameClient Client { get; set; }

        public const int GameWidth = 90;
        public const int GameHeight = 45;
        public const int GridWidth = 15;
        public const int GridHeight = 15;

        public static Random Random { get; private set; } = new Random();

        public static Font Font { get; private set; }

        public static GridScreen GridScreen { get; set; }
        public static MainMenuScreen MainMenuScreen { get; private set; }
        public static ClientWaitingLobby ClientWaitingLobby { get; set; }

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
            var fontMaster = new FontMaster(Texture2D.FromFile(SadConsole.Game.Instance.GraphicsDevice, "Graphics/Textures/Tileset.png"), 16, 16);
            Font = fontMaster.GetFont(Font.FontSizes.Three);

            SadConsole.Game.Instance.Window.Title = "Bomberman";
            InitializeMenuScreen();
        }

        private static void InitializeMenuScreen()
        {
            Global.CurrentScreen = MainMenuScreen = new MainMenuScreen(GameWidth, GameHeight);
        }

        public static void InitializeGameScreen(bool multiplayer)
        {
            Global.CurrentScreen = GridScreen = new GridScreen(GridWidth, GridHeight, Font);
            GridScreen.IsFocused = true;

            if (multiplayer)
            {
                ClientWaitingLobby.IsVisible = false;
                ClientWaitingLobby.IsFocused = false;
            }
        }

        public static void Update(GameTime gameTime)
        {
            // Will be null for single player games
            Client?.Update(gameTime);
        }
    }
}
