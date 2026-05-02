using System;
using System.Collections.Generic;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Infrastructure.Networking
{
    /// <summary>
    /// Concrete implementation of <see cref="ICharacterDataBuffer"/>.
    /// Not thread-safe by design — all three handlers run on the same
    /// <see cref="PacketDispatcher"/> consumer thread.
    /// </summary>
    public sealed class CharacterDataBuffer : ICharacterDataBuffer
    {
        private readonly List<byte> _buffer = new List<byte>(65536);
        private bool _isBuffering;

        /// <inheritdoc/>
        public bool IsBuffering => _isBuffering;

        /// <inheritdoc/>
        public int ByteCount => _buffer.Count;

        /// <inheritdoc/>
        public void Reset()
        {
            _buffer.Clear();
            _isBuffering = true;
            BotLogger.Debug("CharacterDataBuffer", "Buffer reset — awaiting 0x3013 chunks.");
        }

        /// <inheritdoc/>
        public void Append(byte[] data)
        {
            if (!_isBuffering) return;
            _buffer.AddRange(data);
            BotLogger.Debug("CharacterDataBuffer",
                $"Appended {data.Length} bytes. Total: {_buffer.Count}");
        }

        /// <inheritdoc/>
        public byte[] FinalizeAndGet()
        {
            _isBuffering = false;
            var result = _buffer.ToArray();
            _buffer.Clear();
            BotLogger.Debug("CharacterDataBuffer",
                $"Finalized {result.Length} bytes — dispatching to handler.");
            return result;
        }
    }
}
