using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Infrastructure.Networking.Mcp
{
    public class McpServer
    {
        private HttpListener? _listener;
        private readonly IMcpToolProvider _toolProvider;
        private bool _isRunning;
        private readonly List<HttpListenerResponse> _sseClients = new();
        private readonly object _clientLock = new();

        public McpServer(IMcpToolProvider toolProvider)
        {
            _toolProvider = toolProvider;
        }

        public void Start(int port = 5001)
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                _isRunning = true;

                Task.Run(() => ListenAsync());
                BotLogger.Info("MCP", $"MCP Server started on http://localhost:{port}");
            }
            catch (Exception ex)
            {
                BotLogger.Error("MCP", $"Failed to start MCP Server: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            _listener?.Close();
            
            lock (_clientLock)
            {
                foreach (var client in _sseClients)
                {
                    try { client.Close(); } catch { }
                }
                _sseClients.Clear();
            }
        }

        private async Task ListenAsync()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context));
                }
                catch (HttpListenerException) { }
                catch (Exception ex)
                {
                    BotLogger.Error("MCP", $"Listener error: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            // CORS
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            try
            {
                if (req.HttpMethod == "GET" && req.Url?.AbsolutePath == "/mcp/v1")
                {
                    await HandleSseAsync(context);
                }
                else if (req.HttpMethod == "POST" && req.Url?.AbsolutePath == "/mcp/v1")
                {
                    await HandlePostAsync(context);
                }
                else
                {
                    res.StatusCode = 404;
                    res.Close();
                }
            }
            catch (Exception ex)
            {
                BotLogger.Error("MCP", $"Request error: {ex.Message}");
                res.StatusCode = 500;
                res.Close();
            }
        }

        private async Task HandleSseAsync(HttpListenerContext context)
        {
            var res = context.Response;
            res.ContentType = "text/event-stream";
            res.Headers.Add("Cache-Control", "no-cache");
            res.Headers.Add("Connection", "keep-alive");

            lock (_clientLock)
            {
                _sseClients.Add(res);
            }

            // Keep connection open
            // Send initial endpoint event
            byte[] endpointEvent = Encoding.UTF8.GetBytes("event: endpoint\ndata: /mcp/v1\n\n");
            await res.OutputStream.WriteAsync(endpointEvent, 0, endpointEvent.Length);
            await res.OutputStream.FlushAsync();

            BotLogger.Info("MCP", "New SSE client connected.");

            // We don't close the response here. The client stays connected until they close or we stop.
            // However, HttpListener needs to keep the thread alive or handle it.
            // For now, we'll just wait indefinitely.
            while (_isRunning && context.Response.OutputStream.CanWrite)
            {
                await Task.Delay(10000); // Heartbeat could be added here
            }
        }

        private async Task HandlePostAsync(HttpListenerContext context)
        {
            using var reader = new StreamReader(context.Request.InputStream);
            string body = await reader.ReadToEndAsync();
            
            try
            {
                var rpcRequest = JsonSerializer.Deserialize<JsonElement>(body);
                string method = rpcRequest.GetProperty("method").GetString() ?? "";
                var id = rpcRequest.GetProperty("id");

                object result;
                if (method == "list_tools")
                {
                    result = new { tools = _toolProvider.GetTools() };
                }
                else if (method == "call_tool")
                {
                    string toolName = rpcRequest.GetProperty("params").GetProperty("name").GetString() ?? "";
                    var args = JsonSerializer.Deserialize<Dictionary<string, object>>(rpcRequest.GetProperty("params").GetProperty("arguments").GetRawText());
                    result = await _toolProvider.CallToolAsync(toolName, args ?? new Dictionary<string, object>());
                }
                else
                {
                    throw new Exception($"Method {method} not implemented");
                }

                var response = new
                {
                    jsonrpc = "2.0",
                    id = id,
                    result = result
                };

                byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    jsonrpc = "2.0",
                    error = new { code = -32603, message = ex.Message }
                };
                byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorResponse));
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            finally
            {
                context.Response.Close();
            }
        }
    }
}
