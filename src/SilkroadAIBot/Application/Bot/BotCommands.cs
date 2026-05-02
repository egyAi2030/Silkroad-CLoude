using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Entities;

namespace SilkroadAIBot.Application.Bot
{
    public abstract class BotCommandBase : IBotCommand
    {
        public abstract int Priority { get; }
        public abstract string Name { get; }
        public abstract void Execute(IPacketSender sender);
    }

    public sealed class UseItemCommand : BotCommandBase
    {
        private readonly byte _slot;
        public UseItemCommand(byte slot) => _slot = slot;

        public override int Priority => 100; // Recovery is highest priority
        public override string Name => $"Use Item (Slot {_slot})";
        public override void Execute(IPacketSender sender) => sender.SendUseItem(_slot);
    }

    public sealed class CastSkillCommand : BotCommandBase
    {
        private readonly uint _skillID;
        private readonly uint _targetUID;
        public CastSkillCommand(uint skillID, uint targetUID = 0)
        {
            _skillID = skillID;
            _targetUID = targetUID;
        }

        public override int Priority => 50; // Combat
        public override string Name => $"Cast Skill {_skillID} on {_targetUID}";
        public override void Execute(IPacketSender sender) => sender.SendCastSkill(_skillID, _targetUID);
    }

    public sealed class MoveCommand : BotCommandBase
    {
        private readonly SRCoord _destination;
        public MoveCommand(SRCoord destination) => _destination = destination;

        public override int Priority => 10; // Navigation
        public override string Name => $"Move to {_destination}";
        public override void Execute(IPacketSender sender) => sender.SendMovement(_destination);
    }

    public sealed class SelectTargetCommand : BotCommandBase
    {
        private readonly uint _targetUID;
        public SelectTargetCommand(uint targetUID) => _targetUID = targetUID;

        public override int Priority => 60; // Target selection slightly above attack
        public override string Name => $"Select Target {_targetUID}";
        public override void Execute(IPacketSender sender) => sender.SendSelectTarget(_targetUID);
    }

    public sealed class PickupCommand : BotCommandBase
    {
        public override int Priority => 20; // Looting
        public override string Name => "Pickup Item";
        public override void Execute(IPacketSender sender) => sender.SendPickup();
    }

    public sealed class BasicAttackCommand : BotCommandBase
    {
        private readonly uint _targetUID;
        public BasicAttackCommand(uint targetUID) => _targetUID = targetUID;

        public override int Priority => 40; // Basic attack is lower priority than skills (50)
        public override string Name => $"Basic Attack on {_targetUID}";
        public override void Execute(IPacketSender sender) => sender.SendBasicAttack(_targetUID);
    }
}
