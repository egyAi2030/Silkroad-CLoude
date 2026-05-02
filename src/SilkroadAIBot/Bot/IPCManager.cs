using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Bot
{
    /// <summary>
    /// Manages the Named Pipe IPC bridge between the Bot (C#) and the RedirectorDLL (C++).
    /// This allows the bot to "talk" to the game client, enabling interaction features.
    /// </summary>
    public class IPCManager
    {
        public static IPCManager? Instance { get; private set; }
        
        // v1.2.5: Dynamic Pipe Name to prevent conflicts with zombie processes
        private string PipeName => $"SilkroadAIBot_IPC_{System.Diagnostics.Process.GetCurrentProcess().Id}";
        
        private NamedPipeServerStream? _server;
        private CancellationTokenSource? _cts;
        private bool _isLinked;

        public IPCManager()
        {
            Instance = this;
        }

        public event Action<bool>? OnLinkStatusChanged;
        public bool IsLinked => _isLinked;

        public void Start()
        {
            if (_server != null) return;
            
            _cts = new CancellationTokenSource();
            Task.Run(() => ServerLoop(_cts.Token));
            BotLogger.Info("IPC", "IPC Server started. Waiting for RedirectorDLL to connect...");
        }

        public void Stop()
        {
            _cts?.Cancel();
            _server?.Close();
            _server = null;
            _isLinked = false;
            OnLinkStatusChanged?.Invoke(false);
        }

        private async Task ServerLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _server = new NamedPipeServerStream(
                        PipeName, 
                        PipeDirection.InOut, 
                        10, 
                        PipeTransmissionMode.Byte, 
                        PipeOptions.Asynchronous);

                    await _server.WaitForConnectionAsync(token);
                    
                    _isLinked = true;
                    OnLinkStatusChanged?.Invoke(true);
                    BotLogger.Info("IPC", "Client Linked Successfully (RedirectorDLL connected).");

                    using (var reader = new StreamReader(_server))
                    using (var writer = new StreamWriter(_server) { AutoFlush = true })
                    {
                        while (_server.IsConnected && !token.IsCancellationRequested)
                        {
                            string? line = await reader.ReadLineAsync();
                            if (line == null) break; // Client disconnected

                            ProcessCommand(line, writer);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    BotLogger.Error("IPC", $"Error in IPC loop: {ex.Message}");
                }
                finally
                {
                    _isLinked = false;
                    OnLinkStatusChanged?.Invoke(false);
                    _server?.Dispose();
                    _server = null;
                }

                await Task.Delay(1000, token); // Wait before retrying
            }
        }

        private void ProcessCommand(string cmd, StreamWriter writer)
        {
            // Simple command protocol
            if (cmd.StartsWith("PING"))
            {
                writer.WriteLine("PONG");
            }
            else if (cmd.StartsWith("GET_STATE"))
            {
                writer.WriteLine("STATE:READY");
            }
            else
            {
                BotLogger.Debug("IPC", $"Received unknown command: {cmd}");
            }
        }

        public async Task SendCommand(string cmd)
        {
            if (_server != null && _server.IsConnected)
            {
                try {
                    using (var writer = new StreamWriter(_server, leaveOpen: true) { AutoFlush = true })
                    {
                        await writer.WriteLineAsync(cmd);
                    }
                } catch { }
            }
        }
    }
}
