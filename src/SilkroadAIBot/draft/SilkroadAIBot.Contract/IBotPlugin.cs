using System;
using System.Windows.Forms;

namespace SilkroadAIBot.Contract
{
    public interface IBotPlugin
    {
        string Name { get; }
        void Initialize(IBotContext context);
        Control GetUI();
    }
}
