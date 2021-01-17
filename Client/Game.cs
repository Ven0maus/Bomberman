using Bomberman.Client.GameObjects;
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

        public static GridScreen GridScreen { get; private set; }
        public static MainMenuScreen MainMenuScreen { get; private set; }

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
            SadConsole.Game.Instance.Window.Title = "Bomberman";

            InitializeMenuScreen();
        }

        private static void InitializeMenuScreen()
        {
            Global.CurrentScreen = MainMenuScreen = new MainMenuScreen(GameWidth, GameHeight);
        }

        public static void InitializeGameScreen()
        {
            var fontMaster = new FontMaster(Texture2D.FromFile(SadConsole.Game.Instance.GraphicsDevice, "Graphics/Textures/Tileset.png"), 16, 16);
            Font = fontMaster.GetFont(Font.FontSizes.Three);
            Global.CurrentScreen = GridScreen = new GridScreen(GridWidth, GridHeight, Font);
        }

        public static void Update(GameTime gameTime)
        {
            // Will be null for single player games
            Client?.Update(gameTime);
        }

        public static void GameOver(Player player)
        {
            player.Alive = false;

            // TODO: Wait for end of game by other players or until everyone left
            // Then go back to the lobby to ready up for a new game
        }
    }
}
