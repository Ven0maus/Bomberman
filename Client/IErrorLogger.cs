using System;
using System.Collections.Generic;
using System.Text;

namespace Bomberman.Client
{
    public interface IErrorLogger
    {
        void ShowError(string message);
        void ClearError();
    }
}
