using System;
using SilkroadAIBot.Core.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SilkroadAIBot.Proxy
{
    public class ProxyManager
    {
        private ProxyListener _listener;
        private List<ProxyContext> _sessions;
        public List<ProxyContext> Sessions => _sessions;
        public ProxyContext? ActiveSession => _sessions.Count > 0 ? _sessions[0] : null;
        private string _targetGatewayIP;
        private int _targetGatewayPort;
        private SilkroadAIBot.Data.DataManager _dataManager = null!;
        private SilkroadAIBot.Bot.WorldState _worldState;
        
        public static ProxyManager Instance { get; private set; } = null!;
        private string? _nextTargetIP;
        private int? _nextTargetPort;
        public int LocalPort { get; private set; }
        public string TargetGatewayIP => _targetGatewayIP;
        public int TargetGatewayPort => _targetGatewayPort;
        // v1.1.10: Shared vSRO Plus 0xF000 payload captured during Gateway session for re-use on Agent leg
        public byte[]? CapturedF000Payload { get; set; } = null;

        public ProxyManager(string targetIP, int targetPort, SilkroadAIBot.Bot.WorldState worldState, int localPort = 15778)
        {
            Instance = this;
            _targetGatewayIP = targetIP;
            _targetGatewayPort = targetPort;
            _worldState = worldState;
            LocalPort = localPort;
            _sessions = new List<ProxyContext>();
            _listener = new ProxyListener(localPort); // Listen on configurable Proxy Port
            _listener.OnClientConnected += Listener_OnClientConnected;
        }

        public void SetNextTarget(string ip, int port)
        {
            _nextTargetIP = ip;
            _nextTargetPort = port;
            BotLogger.Info("ProxyManager", $"Next connection will be routed to Agent: {ip}:{port}");
        }

        public void ResetGateway(string ip, int port)
        {
            _targetGatewayIP = ip;
            _targetGatewayPort = port;
            BotLogger.Info("ProxyManager", $"Gateway target reset to {ip}:{port}");
        }

        public void SetDataManager(SilkroadAIBot.Data.DataManager dataManager)
        {
            _dataManager = dataManager;
            // Apply to existing sessions if any
            foreach (var session in _sessions)
            {
                session.Initialize(dataManager);
            }
        }

        public void Start()
        {
            _listener.Start();
        }

        public void Stop()
        {
            _listener.Stop();
        }

        // v1.1.14: Return the server-side connection of the active Agent session for bot bundles
        public SilkroadAIBot.Networking.ClientlessConnection? GetActiveServerConnection()
        {
            foreach (var s in _sessions)
                if (s.ServerConnection != null) return s.ServerConnection;
            return null;
        }

        private async void Listener_OnClientConnected(object? sender, System.Net.Sockets.TcpClient client)
        {
            BotLogger.Info("Proxy", "A client has just knocked on the door! (Connection Attempt)");
            BotLogger.Info("ProxyManager", "New Client Connection.");
            
            // Prune dead sessions first
            _sessions.RemoveAll(s => !s.IsActive);
            
            // Bug Fix v1.1.6: Stop and clear any previous Gateway/Agent sessions to prevent IO conflicts during handover.
            if (_sessions.Count > 0)
            {
                BotLogger.Info("ProxyManager", $"Closing {_sessions.Count} existing session(s) for clean handover...");
                foreach (var session in _sessions)
                {
                    session.Stop();
                }
                _sessions.Clear();
            }

            var context = new ProxyContext(client, _worldState);
            if (_dataManager != null) context.Initialize(_dataManager);
            _sessions.Add(context);
            
            string targetIP = _nextTargetIP ?? _targetGatewayIP;
            int targetPort = _nextTargetPort ?? _targetGatewayPort;
            
            BotLogger.Info("ProxyManager", $"Handing over new client connection. Session Target -> {targetIP}:{targetPort}");
            
            // If it was an agent redirection, clear it for the next one (unless we want to preserve it)
            // Usually, one connection = one gateway, then one agent.
            _nextTargetIP = null;
            _nextTargetPort = null;

            try
            {
                BotLogger.Info("ProxyManager", $"Routing connection to {targetIP}:{targetPort}...");
                await context.StartProxyAsync(targetIP, targetPort);
                BotLogger.Info("ProxyManager", "Proxy session fully established.");
            }
            catch (Exception ex)
            {
                BotLogger.Error("ProxyManager", $"Session failed to establish: {ex.Message}");
                _sessions.Remove(context);
            }
        }
    }
}


