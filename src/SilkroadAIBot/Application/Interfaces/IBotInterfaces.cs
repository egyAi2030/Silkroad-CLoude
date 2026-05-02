using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Domain.Events;

namespace SilkroadAIBot.Application.Interfaces
{
    /// <summary>
    /// A self-contained bot behaviour strategy.
    /// Bundles subscribe to WorldState events inside <see cref="StartAsync"/>
    /// and unsubscribe in <see cref="StopAsync"/>.
    /// <see cref="TickAsync"/> handles timer-based logic that has no natural event trigger
    /// (e.g., stuck detection, target re-evaluation timeouts).
    /// </summary>
    public interface IBotBundle
    {
        /// <summary>Human-readable name for logging and UI display.</summary>
        string Name { get; }

        /// <summary>
        /// Starts the bundle. Implementations subscribe to domain events here.
        /// Must return promptly — long-running work runs inside the event callbacks
        /// or within a <c>Task.Delay</c> loop started via <c>Task.Run</c>.
        /// </summary>
        /// <param name="ct">Cancellation token. Cancelled when the bot stops.</param>
        Task StartAsync(CancellationToken ct);

        /// <summary>
        /// Stops the bundle and unsubscribes all event handlers.
        /// Must complete within 2 seconds.
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// Called by <see cref="IBotController"/> on a configurable interval (default 100 ms).
        /// Use for timer-based logic: stuck detection, cooldown tracking, target re-evaluation.
        /// Bundles that have no tick logic should return <see cref="Task.CompletedTask"/> immediately.
        /// </summary>
        /// <param name="ct">Cancellation token. Cancelled when the bot stops.</param>
        Task TickAsync(CancellationToken ct);
    }

    /// <summary>
    /// Encapsulates a single bot action (packet) to be executed by <see cref="IBotController"/>.
    /// Part of the Command Pattern to allow prioritization and logging.
    /// </summary>
    public interface IBotCommand
    {
        /// <summary>
        /// Higher value means higher priority.
        /// Recovery (Heal) = 100, Attack = 50, Movement = 10.
        /// </summary>
        int Priority { get; }

        /// <summary>Human-readable name for logging.</summary>
        string Name { get; }

        /// <summary>
        /// Executes the command by calling the appropriate <see cref="IPacketSender"/> method.
        /// </summary>
        /// <param name="sender">The active packet sender for the agent connection.</param>
        void Execute(IPacketSender sender);
    }

    /// <summary>
    /// Orchestrates all active <see cref="IBotBundle"/> instances.
    /// Manages the tick loop and bundle lifecycle.
    /// </summary>
    public interface IBotController
    {
        /// <summary>The set of bundles currently registered with this controller.</summary>
        IReadOnlyList<IBotBundle> Bundles { get; }

        /// <summary>
        /// Registers a bundle. Must be called before <see cref="StartAsync"/>.
        /// </summary>
        /// <param name="bundle">The bundle to add.</param>
        void Register(IBotBundle bundle);

        /// <summary>
        /// Starts all registered bundles and begins the tick loop.
        /// </summary>
        /// <param name="ct">Cancellation token. Cancellation stops all bundles and the loop.</param>
        Task StartAsync(CancellationToken ct);

        /// <summary>Stops all bundles and the tick loop. Waits for graceful shutdown.</summary>
        Task StopAsync();

        /// <summary>Returns <c>true</c> if the controller is currently running.</summary>
        bool IsRunning { get; }

        /// <summary>Returns the total runtime of the current bot session.</summary>
        System.TimeSpan Runtime { get; }

        /// <summary>Returns the total kill count for this session.</summary>
        int KillsCount { get; }

        /// <summary>Returns the current XP/hour rate.</summary>
        double XpPerHour { get; }

        /// <summary>Starts the bot asynchronously (UI Compatibility).</summary>
        void Start();

        /// <summary>Stops the bot (UI Compatibility).</summary>
        void Stop();

        /// <summary>
        /// Enqueues a command for execution at the end of the current bot tick.
        /// If multiple commands of the same type are queued, the controller may 
        /// consolidate or drop them based on its execution strategy.
        /// </summary>
        /// <param name="command">The command to queue.</param>
        void Enqueue(IBotCommand command);
    }

    /// <summary>
    /// Observer interface for world state change notifications.
    /// Implement this interface to receive callbacks when entities spawn, despawn,
    /// or when the character's stats change.
    /// Prefer subscribing to <see cref="IWorldStateRepository"/> domain events
    /// via <c>WorldState.Subscribe&lt;TEvent&gt;</c> over implementing this interface
    /// when only a subset of events is needed.
    /// </summary>
    public interface IWorldStateObserver
    {
        /// <summary>Called when a new entity appears in the game world.</summary>
        /// <param name="ev">Immutable event record containing entity details.</param>
        void OnEntitySpawned(EntitySpawnedEvent ev);

        /// <summary>Called when an entity is removed from the game world.</summary>
        /// <param name="ev">Immutable event record containing the entity UID.</param>
        void OnEntityDespawned(EntityDespawnedEvent ev);

        /// <summary>Called when the character's HP or MP changes.</summary>
        /// <param name="ev">Immutable event record containing new HP/MP values.</param>
        void OnCharacterHpChanged(CharacterHpChangedEvent ev);

        /// <summary>Called when the bot's character kills an entity.</summary>
        /// <param name="ev">Immutable event record containing killer and victim UIDs.</param>
        void OnKillConfirmed(KillConfirmedEvent ev);

        /// <summary>Called when the character's world position changes.</summary>
        /// <param name="ev">Immutable event record containing new position.</param>
        void OnCharacterPositionChanged(CharacterPositionChangedEvent ev);
    }
}
