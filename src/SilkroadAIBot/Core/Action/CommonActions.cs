using SilkroadAIBot.Networking;
using SilkroadAIBot.Core.Action;

namespace SilkroadAIBot.Core.Action
{
    public class NullAction : BotAction
    {
        public NullAction() : base("Null Action") { }

        public override void Execute(ClientlessConnection connection)
        {
            Complete();
        }
    }

    public class UseItemAction : BotAction
    {
        private byte _slot;
        
        public UseItemAction(byte inventorySlot) : base($"Use Item (Slot: {inventorySlot})")
        {
            _slot = inventorySlot;
        }

        public override void Execute(ClientlessConnection connection)
        {
            using var writer = new Domain.Network.SRPacketWriter(Opcode.CLIENT_INVENTORY_ITEM_USE);
            writer.WriteByte(_slot);
            writer.WriteByte(0x00); // OptionalData — always 0
            connection.SendPacket(writer.Build());
            
            Complete();
        }
    }
}
