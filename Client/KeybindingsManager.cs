using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;

namespace Bomberman.Client
{
    public static class KeybindingsManager
    {
        private static readonly Dictionary<Keybindings, Keys> _keybindings = new Dictionary<Keybindings, Keys>();

        public static void InitializeDefaultKeybindings()
        {
            (Keybindings, Keys)[] bindings = new (Keybindings, Keys)[]
            {
                (Keybindings.Move_Up, Keys.Z),
                (Keybindings.Move_Down, Keys.S),
                (Keybindings.Move_Left, Keys.Q),
                (Keybindings.Move_Right, Keys.D),
                (Keybindings.Place_Bombs, Keys.Space),
            };

            foreach (var binding in bindings)
            {
                _keybindings.Add(binding.Item1, binding.Item2);
            }
        }

        public static void EditKeybinding(Keybindings binding, Keys newKey)
        {
            if (_keybindings.ContainsKey(binding))
                _keybindings[binding] = newKey;
        }

        public static Keys GetKeybinding(Keybindings binding)
        {
            if (_keybindings.TryGetValue(binding, out Keys value)) return value;
            throw new System.Exception("No keybinding defined with name: " + binding);
        }

        public static KeyValuePair<Keybindings, Keys>[] GetKeybindings()
        {
            return _keybindings.ToArray();
        }
    }

    public enum Keybindings
    {
        Move_Up,
        Move_Down,
        Move_Left,
        Move_Right,
        Place_Bombs
    }
}
