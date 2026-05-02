using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Domain.Enums;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Application.Bot;

namespace SilkroadAIBot.Application.Bot.Bundles
{
    public class RecoveryBundle : IBotBundle
    {
        public string Name => "RecoveryBundle";
        public int Priority => 100; // Highest priority

        private readonly IWorldStateRepository _worldState;
        private readonly IBotController _controller;
        
        private DateTime _lastRecoveryTime = DateTime.MinValue;
        private const int GCD_MS_CHINESE = 500;
        private const int GCD_MS_EUROPEAN = 1000;

        public RecoveryBundle(IWorldStateRepository worldState, IBotController controller)
        {
            _worldState = worldState;
            _controller = controller;
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        public Task TickAsync(CancellationToken ct)
        {
            var character = _worldState.GetCharacter();

            if (character.HP <= 0) return Task.CompletedTask;

            int gcdMs = character.IsEuropean ? GCD_MS_EUROPEAN : GCD_MS_CHINESE;
            if ((DateTime.Now - _lastRecoveryTime).TotalMilliseconds < gcdMs)
                return Task.CompletedTask;

            // Simple recovery logic: If HP < 50%, use HP potion (slot 1 assumed for now)
            if (character.HPMax > 0 && character.HPPercent < 50)
            {
                BotLogger.Info(Name, $"HP low ({character.HPPercent:F1}%). Enqueuing recovery action.");
                _controller.Enqueue(new UseItemCommand(1)); // Use item in slot 1
                _lastRecoveryTime = DateTime.Now;
            }
            // If MP < 30%, use MP potion (slot 2 assumed)
            else if (character.MPMax > 0 && character.MPPercent < 30)
            {
                BotLogger.Info(Name, $"MP low ({character.MPPercent:F1}%). Enqueuing recovery action.");
                _controller.Enqueue(new UseItemCommand(2)); // Use item in slot 2
                _lastRecoveryTime = DateTime.Now;
            }

            return Task.CompletedTask;
        }
    }
}
