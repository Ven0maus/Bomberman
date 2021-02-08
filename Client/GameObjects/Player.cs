using Bomberman.Client.ServerSide;
using Microsoft.Xna.Framework;
using SadConsole.Entities;
using System;

namespace Bomberman.Client.GameObjects
{
    public class Player : Entity
    {
        public int Id;
        // Powerups
        public int MaxBombs = 1;
        public int BombStrength = 1;
        public bool Alive { get; private set; }
        public int BombsPlaced { get; set; }
        public bool RequestedMovement { get; set; }
        public bool RequestBombPlacement { get; set; }
        public readonly Color Color;

        public bool _controllable;

        public int Kills = 0;

        private int _bombCounter = 0;

        public Player(Point position, int id, Color color, bool controllable = true) : base(Color.White, Color.Transparent, 18)
        {
            Alive = true;
            Id = id;
            _controllable = controllable;
            Position = position;
            Font = Game.Font;
            Moved += Player_Moved;
            IsFocused = _controllable;
            Color = color;
            Animation[0].Foreground = Color;
        }

        private int _currentGlyph;
        private void Player_Moved(object sender, EntityMovedEventArgs e)
        {
            var previous = e.FromPosition;
            var current = Position;

            int diffX = current.X - previous.X;
            int diffY = current.Y - previous.Y;

            int changed = _currentGlyph;

            // Right
            if (diffX == 1 && diffY == 0)
            {
                _currentGlyph = 16;
            }
            // Left
            else if (diffX == -1 && diffY == 0)
            {
                _currentGlyph = 17;
            }
            // Up
            else if (diffX == 0 && diffY == -1)
            {
                _currentGlyph = 19;
            }
            // Down
            else if (diffX == 0 && diffY == 1)
            {
                _currentGlyph = 18;
            }

            if (changed != _currentGlyph)
            {
                Animation[0].Glyph = _currentGlyph;
                Animation.IsDirty = true;
            }
        }

        public void StartDeadAnimation()
        {
            Alive = false;

            new DeathSign(Position, Color)
            {
                Parent = Game.GridScreen
            };

            // Also show it in the player overview
            Game.GridScreen.PlayerKilled(this);

            IsVisible = false;
            IsFocused = false;
        }

        private bool _isBlinking = false;
        public void StartBlinkingAnimation()
        {
            _isBlinking = true;
        }

        public void StopBlinkingAnimation()
        {
            _isBlinking = false;
            Animation[0].Foreground = Color;
            Animation.IsDirty = true;
            _timeSinceLastBlink = 0;
        }

        private const double _blinkInterval = 0.5d * 1000;
        private double _timeSinceLastBlink = 0d;
        public override void Update(TimeSpan timeElapsed)
        {
            base.Update(timeElapsed);
            if (_isBlinking)
            {
                _timeSinceLastBlink += timeElapsed.Milliseconds;
                if (_timeSinceLastBlink >= _blinkInterval)
                {
                    _timeSinceLastBlink = 0;
                    if (Animation[0].Foreground == Color)
                        Animation[0].Foreground = Color.Lerp(Color, Color.Transparent, 0.3f);
                    else
                        Animation[0].Foreground = Color;
                    Animation.IsDirty = true;
                }
            }
        }

        private bool _walkedFirstTime = false;

        private void Move(Point position)
        {
            if (!Game.Singleplayer) return;
            if (Game.GridScreen.Grid.CanMove(position.X, position.Y))
            {
                Game.GridScreen.Grid.CheckPowerup(position);
                Position = position;
            }
        }

        public override bool ProcessKeyboard(SadConsole.Input.Keyboard info)
        {
            if (!_controllable) return base.ProcessKeyboard(info);
            if (!Alive)
            {
                IsFocused = false;
                return base.ProcessKeyboard(info);
            }

            if (info.IsKeyPressed(KeybindingsManager.GetKeybinding(Keybindings.Move_Up)))
            {
                if (!RequestedMovement)
                {
                    RequestedMovement = true;
                    if (!Game.Singleplayer)
                    {
                        Game.Client.SendPacket(Game.Client.Client, new Packet("moveup"));
                    }
                    else
                    {
                        var targetPos = Position + new Point(0, -1);
                        Move(targetPos);

                        RequestedMovement = false;

                        if (!_walkedFirstTime)
                        {
                            _walkedFirstTime = true;

                            // Uncover the entire grid from darkness
                            Game.GridScreen.Grid.UncoverFromDarkness(!Game.Singleplayer);
                        }
                    }
                }
            }
            else if (info.IsKeyPressed(KeybindingsManager.GetKeybinding(Keybindings.Move_Down)))
            {
                if (!RequestedMovement)
                {
                    RequestedMovement = true;
                    if (!Game.Singleplayer)
                    {
                        Game.Client.SendPacket(Game.Client.Client, new Packet("movedown"));
                    }
                    else
                    {
                        var targetPos = Position + new Point(0, 1);
                        Move(targetPos);
                        RequestedMovement = false;

                        if (!_walkedFirstTime)
                        {
                            _walkedFirstTime = true;

                            // Uncover the entire grid from darkness
                            Game.GridScreen.Grid.UncoverFromDarkness(!Game.Singleplayer);
                        }
                    }
                }
            }
            else if (info.IsKeyPressed(KeybindingsManager.GetKeybinding(Keybindings.Move_Left)))
            {
                if (!RequestedMovement)
                {
                    RequestedMovement = true;
                    if (!Game.Singleplayer)
                    {
                        Game.Client.SendPacket(Game.Client.Client, new Packet("moveleft"));
                    }
                    else
                    {
                        var targetPos = Position + new Point(-1, 0);
                        Move(targetPos);
                        RequestedMovement = false;

                        if (!_walkedFirstTime)
                        {
                            _walkedFirstTime = true;

                            // Uncover the entire grid from darkness
                            Game.GridScreen.Grid.UncoverFromDarkness(!Game.Singleplayer);
                        }
                    }
                }
            }
            else if (info.IsKeyPressed(KeybindingsManager.GetKeybinding(Keybindings.Move_Right)))
            {
                if (!RequestedMovement)
                {
                    RequestedMovement = true;
                    if (!Game.Singleplayer)
                    {
                        Game.Client.SendPacket(Game.Client.Client, new Packet("moveright"));
                    }
                    else
                    {
                        var targetPos = Position + new Point(1, 0);
                        Move(targetPos);
                        RequestedMovement = false;

                        if (!_walkedFirstTime)
                        {
                            _walkedFirstTime = true;

                            // Uncover the entire grid from darkness
                            Game.GridScreen.Grid.UncoverFromDarkness(!Game.Singleplayer);
                        }
                    }
                }
            }
            else if (info.IsKeyPressed(KeybindingsManager.GetKeybinding(Keybindings.Place_Bombs)))
            {
                if (!RequestBombPlacement)
                {
                    RequestBombPlacement = true;
                    if (!Game.Singleplayer)
                    {
                        Game.Client.SendPacket(Game.Client.Client, new Packet("placebomb"));
                    }
                    else
                    {
                        if (Game.Player.BombsPlaced == Game.Player.MaxBombs)
                        {
                            RequestBombPlacement = false;
                            return true;
                        }
                        var bomb = new Bomb(this, Position, BombStrength, _bombCounter++)
                        {
                            Parent = Game.GridScreen
                        };
                        Game.Player.BombsPlaced++;
                        Game.GridScreen.Grid.Bombs.Add(Position, bomb);
                        bomb.StartDetonationPhase();

                        if (!_walkedFirstTime)
                        {
                            _walkedFirstTime = true;

                            // Uncover the entire grid from darkness
                            Game.GridScreen.Grid.UncoverFromDarkness(!Game.Singleplayer);
                        }
                        RequestBombPlacement = false;
                    }
                }
            }

            if (RequestedMovement || RequestBombPlacement)
            {
                if (!_walkedFirstTime)
                {
                    _walkedFirstTime = true;

                    // Uncover the entire grid from darkness
                    Game.GridScreen.Grid.UncoverFromDarkness(!Game.Singleplayer);
                }
                return true;
            }

            return base.ProcessKeyboard(info);
        }
    }
}
