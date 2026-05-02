using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Data;
using SilkroadAIBot.Infrastructure.Networking.Handlers;
using SilkroadAIBot.Networking;

namespace SilkroadAIBot.Infrastructure.Networking
{
    /// <summary>
    /// Wires all <see cref="IPacketHandler"/> implementations into a
    /// <see cref="IPacketHandlerFactory"/>.
    /// Call <see cref="RegisterAll"/> once at startup before starting
    /// <see cref="PacketDispatcher"/>.
    /// </summary>
    public static class HandlerRegistration
    {
        /// <summary>
        /// Registers every known opcode handler into <paramref name="factory"/>.
        /// Uses closures to capture shared dependencies — each opcode's factory
        /// delegate returns a pre-constructed singleton-style handler.
        /// </summary>
        public static void RegisterAll(
            IPacketHandlerFactory factory,
            ICharacterDataBuffer buffer,
            WorldStateAnalyzer analyzer,
            PacketParser parser,
            WorldState worldState,
            DataManager dataManager,
            IPacketSender sender)
        {
            // Pre-construct stateful handlers (shared state via ICharacterDataBuffer)
            var beginHandler   = new CharacterDataBeginHandler(buffer);
            var chunkHandler   = new CharacterDataChunkHandler(buffer);
            var endHandler     = new CharacterDataEndHandler(buffer, analyzer, worldState, dataManager);

            // Pre-construct stateless analyzer handlers
            var selActionHandler = new CharacterSelectionActionHandler(analyzer);
            var selJoinHandler   = new CharacterSelectionJoinHandler(analyzer);
            var targetHandler    = new TargetSelectionResponseHandler(analyzer);
            var statsHandler     = new CharacterStatsHandler(analyzer);
            var expHandler       = new ExpUpdateHandler(analyzer);
            var hpMpHandler      = new HpMpUpdateHandler(analyzer);
            var maxHpMpHandler   = new MaxHpMpHandler(analyzer);
            var dieHandler       = new EntityDieHandler(analyzer);
            var skillListHandler = new SkillListHandler(analyzer);
            var spawnHandler     = new SpawnHandler(analyzer);
            var despawnHandler   = new DespawnHandler(analyzer);
            var locUpdateHandler = new LocationUpdateHandler(analyzer);
            var movHandler       = new MovementHandler(analyzer);

            // Pre-construct stateless parser handlers
            var inventoryHandler  = new InventoryHandler(analyzer);
            var hotbarHandler     = new HotbarHandler(analyzer);
            var itemUseHandler    = new ItemUseResponseHandler(analyzer);
            var groupSpawnHandler = new GroupSpawnHandler(parser);
            var nearbyHandler     = new NearbyEntitiesHandler(parser);
            var namedHandler      = new EntityWithNameHandler(parser);
            var posHandler        = new EntityPositionHandler(parser);
            var altDespawnHandler = new EntityDespawnHandler(parser);
            var killHandler       = new KillConfirmedHandler(parser);
            var entMovHandler     = new EntityMovementHandler(parser);
            var skillUsedHandler  = new SkillUsedHandler(parser);
            var skillHitHandler   = new SkillHitHandler(parser);
            var charIdHandler     = new CharacterIdHandler(parser);
            var xpHandler         = new XpGainHandler(parser);
            var partyHandler      = new PartyGuildHandler(parser);
            var guildHandler      = new GuildInfoHandler(parser);
            var aggroHandler      = new MonsterAggroHandler(parser);
            var guardHandler      = new GuardChallengeHandler(sender);
            var identHandler      = new IdentityHandler();

            // ── Character Data (3-packet state machine) ──────────────────────
            factory.Register(0x34A5, () => beginHandler);
            factory.Register(0x3013, () => chunkHandler);
            factory.Register(0x34A6, () => endHandler);

            // ── Character Selection ───────────────────────────────────────────
            factory.Register(0xB007, () => selActionHandler);
            factory.Register(0xB001, () => selJoinHandler);
            factory.Register(0xB045, () => targetHandler);

            // ── Character Stats ───────────────────────────────────────────────
            factory.Register(0x303D, () => statsHandler);
            factory.Register(0x3056, () => expHandler);

            // ── HP / MP / Life ────────────────────────────────────────────────
            factory.Register(0x3057, () => hpMpHandler);
            factory.Register(0x3054, () => hpMpHandler);   // alias
            factory.Register(0xAA0F, () => maxHpMpHandler);
            factory.Register(0x30BF, () => dieHandler);

            // ── Skills / Inventory ────────────────────────────────────────────
            factory.Register(0xAA17, () => skillListHandler);
            factory.Register(0xAA7F, () => inventoryHandler);
            factory.Register(0xAA78, () => hotbarHandler);
            factory.Register(0xB04C, () => itemUseHandler);

            // ── Entity Spawns ─────────────────────────────────────────────────
            factory.Register(0x3015, () => spawnHandler);
            factory.Register(0x3019, () => groupSpawnHandler);
            factory.Register(0xAA12, () => nearbyHandler);
            factory.Register(0xAA11, () => namedHandler);
            factory.Register(0xAA14, () => posHandler);

            // ── Despawn / Death ───────────────────────────────────────────────
            factory.Register(0x3016, () => despawnHandler);
            factory.Register(0x300C, () => altDespawnHandler);
            factory.Register(0x30C9, () => killHandler);

            // ── Movement ─────────────────────────────────────────────────────
            factory.Register(0xB023, () => locUpdateHandler);
            factory.Register(0xB021, () => entMovHandler);
            factory.Register(0x30D2, () => movHandler);

            // ── XP / Identity ─────────────────────────────────────────────────
            factory.Register(0x3020, () => charIdHandler);
            factory.Register(0x305C, () => xpHandler);

            // ── Combat ────────────────────────────────────────────────────────
            factory.Register(0xB070, () => skillUsedHandler);
            factory.Register(0xB071, () => skillHitHandler);

            // ── Social / Guild ────────────────────────────────────────────────
            factory.Register(0x3101, () => partyHandler);
            factory.Register(0x3305, () => guildHandler);
            factory.Register(0x30D0, () => aggroHandler);

            // ── Security Challenges (Instant Flush) ───────────────────────────
            factory.Register(Opcode.SERVER_GUARD_CHALLENGE_1, () => guardHandler);
            factory.Register(Opcode.SERVER_GUARD_CHALLENGE_2, () => guardHandler);
            factory.Register(Opcode.SERVER_VPLUS_SEC_SYNC, () => guardHandler);
            factory.Register(0x2001, () => identHandler);
        }
    }
}
