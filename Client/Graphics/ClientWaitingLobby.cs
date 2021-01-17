using SadConsole;

namespace Bomberman.Client.Graphics
{
    public class ClientWaitingLobby : ControlsConsole
    {
        private readonly int _width, _height;
        public ClientWaitingLobby(int width, int height) : base(width, height) 
        {
            _width = width;
            _height = height;
        }
    }
}
