using System;
using Microsoft.Extensions.DependencyInjection;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Infrastructure.Persistence;
using SilkroadAIBot.Data;
using SilkroadAIBot.UI;
using SilkroadAIBot.Application.Bot;
using SilkroadAIBot.Infrastructure.Networking.Mcp;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Proxy;
using SilkroadAIBot.Core.Configuration;
using SilkroadAIBot.Networking;

namespace SilkroadAIBot
{
    public static class Bootstrapper
    {
        public static IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            // Persistence
            services.AddSingleton<SilkroadAIBot.Bot.WorldState>();
            services.AddSingleton<IWorldStateRepository, WorldStateRepository>();
            services.AddSingleton<IEntityRepository, EntityRepository>();

            // Infrastructure
            services.AddSingleton<DatabaseManager>();
            services.AddSingleton<DataManager>(sp => {
                var db = sp.GetRequiredService<DatabaseManager>();
                var dm = new DataManager();
                dm.SetDatabase(db);
                return dm;
            });
            services.AddSingleton<IPacketSender, PacketSender>();
            services.AddSingleton<IActionLogger, SilkroadAIBot.Infrastructure.Logging.ActionLogger>();
            
            services.AddSingleton<PacketParser>();
            services.AddSingleton<WorldStateAnalyzer>();
            services.AddSingleton<SkillController>();
            
            // Networking
            services.AddSingleton<ProxyManager>(sp => {
                var worldState = sp.GetRequiredService<SilkroadAIBot.Bot.WorldState>();
                return new ProxyManager(
                    ConfigManager.Config.LastServerIP, 
                    ConfigManager.Config.LastServerPort, 
                    worldState, 
                    ConfigManager.Config.ProxyPort);
            });

            services.AddSingleton<Func<ClientlessConnection?>>(sp => {
                var proxy = sp.GetRequiredService<ProxyManager>();
                return () => proxy.GetActiveServerConnection();
            });

            // Bot Controller
            services.AddSingleton<IBotController, BotController>();

            // MCP Server
            services.AddSingleton<IMcpToolProvider, McpToolProvider>();
            services.AddSingleton<McpServer>();

            // UI
            services.AddTransient<MainForm>();

            return services.BuildServiceProvider();
        }
    }
}
