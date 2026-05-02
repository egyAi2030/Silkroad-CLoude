using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Bot
{
    /// <summary>
    /// Implementation of the Bot Controller.
    /// Orchestrates bot bundles and manages the main execution loop.
    /// </summary>
    public class BotController : IBotController
    {
        private readonly List<IBotBundle> _bundles = new();
        private readonly List<IBotCommand> _commandQueue = new();
        private readonly object _queueLock = new();
        private readonly IPacketSender _sender;
        private readonly IWorldStateRepository _worldState;
        private CancellationTokenSource? _cts;
        private Task? _tickTask;
        private readonly Stopwatch _runtimeStopwatch = new();

        public IReadOnlyList<IBotBundle> Bundles => _bundles.AsReadOnly();
        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        // UI Compatibility Properties
        public TimeSpan Runtime => _runtimeStopwatch.Elapsed;
        public int KillsCount { get; set; } = 0;
        public double XpPerHour { get; set; } = 0;

        public BotController(IWorldStateRepository worldState, IPacketSender sender)
        {
            _worldState = worldState ?? throw new ArgumentNullException(nameof(worldState));
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public void Register(IBotBundle bundle)
        {
            if (IsRunning) throw new InvalidOperationException("Cannot register bundles while running.");
            _bundles.Add(bundle);
        }

        // UI Compatibility
        public void AddBundle(IBotBundle bundle) => Register(bundle);
        public void Start() => _ = StartAsync(CancellationToken.None);
        public void Stop() => _cts?.Cancel();

        public async Task StartAsync(CancellationToken ct)
        {
            if (IsRunning) return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _runtimeStopwatch.Start();

            // Start all bundles
            foreach (var bundle in _bundles)
            {
                await bundle.StartAsync(_cts.Token);
            }

            // Start tick loop
            _tickTask = Task.Run(() => RunTickLoopAsync(_cts.Token), _cts.Token);
            BotLogger.Info("BotController", "AI Tick Loop started.");
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;

            _cts.Cancel();
            _runtimeStopwatch.Stop();

            if (_tickTask != null)
            {
                try { await _tickTask; } catch (OperationCanceledException) { }
            }

            // Stop all bundles
            foreach (var bundle in _bundles)
            {
                await bundle.StopAsync();
            }

            _cts.Dispose();
            _cts = null;
            BotLogger.Info("BotController", "AI Tick Loop stopped.");
        }


        public void Enqueue(IBotCommand command)
        {
            lock (_queueLock)
            {
                _commandQueue.Add(command);
            }
        }

        private async Task RunTickLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var startTime = DateTime.UtcNow;

                try
                {
                    // 1. Tick all bundles
                    foreach (var bundle in _bundles)
                    {
                        await bundle.TickAsync(ct);
                    }

                    // 2. Execute highest priority command
                    ExecuteGatedCommand();
                }
                catch (Exception ex)
                {
                    BotLogger.Error("BotController", $"Error in tick loop: {ex.Message}");
                }

                // 3. Wait for next tick (100ms total)
                var elapsed = DateTime.UtcNow - startTime;
                int delay = Math.Max(1, 100 - (int)elapsed.TotalMilliseconds);
                await Task.Delay(delay, ct);
            }
        }

        private void ExecuteGatedCommand()
        {
            IBotCommand? bestCommand = null;

            lock (_queueLock)
            {
                if (_commandQueue.Count == 0) return;

                // Sort by priority descending
                bestCommand = _commandQueue
                    .OrderByDescending(c => c.Priority)
                    .FirstOrDefault();

                _commandQueue.Clear();
            }

            if (bestCommand != null && _sender.IsConnected)
            {
                try
                {
                    // BotLogger.Debug("BotController", $"[Exec] {bestCommand.Name} (Priority: {bestCommand.Priority})");
                    bestCommand.Execute(_sender);
                }
                catch (Exception ex)
                {
                    BotLogger.Error("BotController", $"Command execution failed: {ex.Message}");
                }
            }
        }
    }
}
