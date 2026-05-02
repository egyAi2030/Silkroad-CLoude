using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Infrastructure.Networking
{
    /// <summary>
    /// O(1) opcode → handler factory backed by a <see cref="Dictionary{TKey,TValue}"/>.
    /// Implements <see cref="IPacketHandlerFactory"/>.
    /// </summary>
    public sealed class PacketHandlerFactory : IPacketHandlerFactory
    {
        private readonly Dictionary<ushort, Func<IPacketHandler>> _registry
            = new Dictionary<ushort, Func<IPacketHandler>>();

        /// <inheritdoc/>
        public IReadOnlyCollection<ushort> RegisteredOpcodes
            => new ReadOnlyCollection<ushort>(new List<ushort>(_registry.Keys));

        /// <inheritdoc/>
        /// <remarks>
        /// Registering an already-registered opcode replaces the previous factory.
        /// This is intentional — it allows the same opcode to be handled by two
        /// aliases (e.g., 0x3057 and 0x3054 both pointing to the HP/MP handler).
        /// </remarks>
        public void Register(ushort opcode, Func<IPacketHandler> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _registry[opcode] = factory;
        }

        /// <inheritdoc/>
        /// <returns>
        /// A handler instance for the opcode, or <c>null</c> if unregistered.
        /// The factory delegate is invoked on every call — handlers are stateless
        /// by design; stateful handlers (e.g., character data buffer) capture
        /// shared state via closure when registered.
        /// </returns>
        public IPacketHandler? Resolve(ushort opcode)
        {
            return _registry.TryGetValue(opcode, out var factory) ? factory() : null;
        }
    }
}
