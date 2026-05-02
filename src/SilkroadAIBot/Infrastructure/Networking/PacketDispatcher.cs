using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Infrastructure.Networking
{
    /// <summary>
    /// Implements <see cref="IPacketDispatcher"/> using a bounded
    /// <see cref="Channel{T}"/> with a single consumer loop.
    /// Producers call <see cref="Enqueue"/> from the proxy relay thread.
    /// The consumer loop runs on a dedicated background task started by <see cref="StartAsync"/>.
    /// </summary>
    public sealed class PacketDispatcher : IPacketDispatcher
    {
        private const int ChannelCapacity = 1000;

        private readonly IPacketHandlerFactory _factory;
        private readonly Channel<SRPacket> _channel;
        private Task? _consumerTask;

        /// <summary>
        /// Fired for every packet that passes through the dispatcher — both
        /// direction (S→C) and the simulated global sniffer event.
        /// Replaces the legacy <c>PacketHandler.OnGlobalPacketReceived</c> static event.
        /// </summary>
        public static event Action<SRPacket, bool>? OnGlobalPacketReceived;

        /// <summary>Triggers the global packet sniffer event.</summary>
        public static void TriggerGlobalPacket(SRPacket packet, bool isSent)
            => OnGlobalPacketReceived?.Invoke(packet, isSent);

        /// <param name="factory">
        /// The handler factory used to resolve handlers per opcode. Must be
        /// pre-populated with all registrations before <see cref="StartAsync"/> is called.
        /// </param>
        public PacketDispatcher(IPacketHandlerFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _channel = Channel.CreateBounded<SRPacket>(new BoundedChannelOptions(ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        /// <inheritdoc/>
        public bool Enqueue(SRPacket packet)
        {
            return _channel.Writer.TryWrite(packet);
        }

        /// <inheritdoc/>
        public Task StartAsync(CancellationToken ct)
        {
            _consumerTask = Task.Run(() => ConsumeAsync(ct), ct);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task StopAsync()
        {
            _channel.Writer.TryComplete();
            if (_consumerTask != null)
                await _consumerTask.ConfigureAwait(false);
        }

        private async Task ConsumeAsync(CancellationToken ct)
        {
            BotLogger.Info("PacketDispatcher", "Consumer loop started.");
            try
            {
                await foreach (var packet in _channel.Reader.ReadAllAsync(ct))
                {
                    DispatchOne(packet);
                }
            }
            catch (OperationCanceledException) { /* clean shutdown */ }
            catch (Exception ex)
            {
                BotLogger.Error("PacketDispatcher", $"Consumer loop fatal: {ex.Message}");
            }
            BotLogger.Info("PacketDispatcher", "Consumer loop stopped.");
        }

        private void DispatchOne(SRPacket packet)
        {
            try
            {
                OnGlobalPacketReceived?.Invoke(packet, false);

                var handler = _factory.Resolve(packet.Opcode);
                if (handler == null)
                {
                    if (packet.Opcode != 0x2002) // suppress keepalive log noise
                        BotLogger.Debug("PacketDispatcher",
                            $"Unhandled opcode 0x{packet.Opcode:X4} ({packet.Payload.Length} bytes)");
                    return;
                }

                BotLogger.Debug("PacketDispatcher",
                    $"→ 0x{packet.Opcode:X4} [{packet.Payload.Length}b]");

                handler.Handle(packet);
            }
            catch (Exception ex)
            {
                BotLogger.Error("PacketDispatcher",
                    $"Handler threw for opcode 0x{packet.Opcode:X4}: {ex.Message}");
            }
        }
    }
}
