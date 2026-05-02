using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Proxy;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Domain.Network;

namespace SilkroadAIBot.Test
{
    public class MockConnection : ClientlessConnection
    {
        public new void SendPacket(SRPacket packet)
        {
            Console.WriteLine($"\n[Simulated Server TX] Opcode: 0x{packet.Opcode:X4}");
            Console.WriteLine($"Payload Length: {packet.Data.Length}");
            Console.WriteLine($"Hex: {BitConverter.ToString(packet.Data)}");
        }
    }
 
    public class PacketTestHarness
    {
        public static void MainTest(string[] args)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            Console.WriteLine("=== SilkroadAIBot 100% Action Packet Tester ===");
            var worldState = new SilkroadAIBot.Bot.WorldState();
            
            // Populate mock world state to satisfy packet builders
            worldState.Character = new SRCharacter { Name = "TestChar", UniqueID = 12345, ModelID = 1907 };
            // Note: worldState.Character is the internal CharacterState, not SRCharacter
            
            // Spin up a dummy listener so the connection appears valid to PacketSender
            var dummyListener = new TcpListener(IPAddress.Loopback, 9999);
            dummyListener.Start();
            
            var mockConn = new MockConnection();
            var connectTask = mockConn.ConnectAsync("127.0.0.1", 9999);
            dummyListener.AcceptTcpClient(); // Accept to complete connection
            connectTask.Wait();
            
            var sender = new PacketSender(worldState, () => mockConn);

            var testCoord = new SRCoord(24103, 120.0f, -180.0f, 50.0f);

            // Execute the action suite to dump hex streams
            Console.WriteLine("\nTesting Action: Movement");
            sender.SendMovement(testCoord);

            Console.WriteLine("\nTesting Action: Attack");
            sender.SendBasicAttack(98765);

            Console.WriteLine("\nTesting Action: Cast Skill");
            sender.SendCastSkill(10293, 98765);

            Console.WriteLine("\nTesting Action: Use Item");
            sender.SendUseItem(13);

            Console.WriteLine("\nTesting Action: Resurrection");
            sender.SendResurrection(1);
            
            Console.WriteLine("\nTesting Action: Party Create");
            sender.SendPartyCreate(98765, 0x01);
            
            Console.WriteLine("\nTesting Action: Exchange Start");
            sender.SendExchangeStart(54321);

            Console.WriteLine("\nTesting Action: Item Move (Inventory Mgmt)");
            sender.SendItemMove(0, 13, 14, 1);

            Console.WriteLine("\nTesting Action: Teleport Use");
            sender.SendTeleportUse(4444, 2, 0, 0);

            Console.WriteLine("\nTesting Action: Stall Create");
            sender.SendStallCreate("WTS Nova Items");

            Console.WriteLine("\n=== Packet Validation Complete ===");
            
            mockConn.Dispose();
            dummyListener.Stop();
        }
    }
}
