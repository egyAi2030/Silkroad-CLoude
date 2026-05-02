using System;
using SilkroadAIBot.Networking;

namespace SilkroadAIBot.Core.Action
{
    public interface IAction
    {
        string Name { get; }
        
        // Execute the action over the network connection or bot context
        void Execute(ClientlessConnection connection);
        
        // Tells if the action has completed or is still running
        bool IsCompleted { get; }
    }

    public abstract class BotAction : IAction
    {
        public string Name { get; protected set; }
        public bool IsCompleted { get; protected set; }

        protected BotAction(string name)
        {
            Name = name;
            IsCompleted = false;
        }

        public abstract void Execute(ClientlessConnection connection);

        protected void Complete()
        {
            IsCompleted = true;
        }
    }
}
