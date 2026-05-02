using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Domain.Network;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Infrastructure.Security;

namespace SilkroadAIBot.Networking
{
    public class ClientlessConnection : IDisposable
    {
        private TcpClient _client = null!;
        private NetworkStream _stream = null!;
        private ISecurityService _security = null!;
        private ConcurrentQueue<SRPacket> _receivedPackets = null!;
        private bool _running;
        
        public event Action<SRPacket>? OnPacketReceived;
        public event Action? OnDisconnected;
        
        public bool IsConnected => _client != null && _client.Connected;
        
        public ISecurityService Security => _security;

        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port);
            _stream = _client.GetStream();
            
            // Use the wrapper service
            _security = new SecurityService();
            
            _receivedPackets = new ConcurrentQueue<SRPacket>();
            _running = true;
            
            Console.WriteLine($"[Connection] Connected to {host}:{port}");
        }

        public NetworkStream GetStream() { return _stream; }

        public async Task PerformHandshakeAsync()
        {
            // Wait for handshake to complete
            int timeout = 5000;
            while (!_security.IsHandshakeComplete && timeout > 0)
            {
                await Task.Delay(100);
                timeout -= 100;
            }

            if (!_security.IsHandshakeComplete)
                throw new TimeoutException("Handshake timed out.");

            Console.WriteLine("[Connection] Handshake Successful."); 
        }

        public async Task ProcessAsync()
        {
            _running = true;
            var readTask = Task.Run(ReceiveLoop);
            var writeTask = Task.Run(SendLoop);
            
            await Task.WhenAny(readTask, writeTask);
            _running = false;
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (_running && IsConnected)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        _security.Recv(buffer, 0, bytesRead);
                        
                        var packets = _security.GetIncomingPackets();
                        foreach (var packet in packets)
                        {
                            _receivedPackets.Enqueue(packet);
                            OnPacketReceived?.Invoke(packet);
                        }
                    }
                    else
                    {
                        HandleDisconnect();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                BotLogger.Error("Connection", $"Read Error: {ex.Message}");
                HandleDisconnect();
            }
        }

        private async Task SendLoop()
        {
            try
            {
                while (_running && IsConnected)
                {
                    var outgoing = _security.GetOutgoingBytes();
                    bool sentAnything = false;
                    
                    foreach (var bytes in outgoing)
                    {
                        await _stream.WriteAsync(bytes, 0, bytes.Length);
                        sentAnything = true;
                    }
                    
                    if (!sentAnything)
                    {
                        await Task.Delay(10); // Check for outgoing every 10ms
                    }
                }
            }
            catch (Exception ex)
            {
                BotLogger.Error("Connection", $"Write Error: {ex.Message}");
                HandleDisconnect();
            }
        }
        
        public void SendPacket(SRPacket packet)
        {
            if (_security != null)
            {
                var wireBytes = _security.FormatPacket(packet);
                _stream.Write(wireBytes, 0, wireBytes.Length);
            }
        }

        public SRPacket? GetNextPacket()
        {
            if (_receivedPackets.TryDequeue(out var packet))
                return packet;
            return null;
        }
        
        private void HandleDisconnect()
        {
            if (_running)
            {
                _running = false;
                Console.WriteLine("[Connection] Disconnected.");
                OnDisconnected?.Invoke();
            }
        }
        
        public void Disconnect()
        {
            HandleDisconnect();
            Dispose();
        }

        public void Dispose()
        {
            _running = false;
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}

