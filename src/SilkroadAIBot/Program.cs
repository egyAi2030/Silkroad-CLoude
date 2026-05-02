using System;
using System.Windows.Forms;
using SilkroadAIBot.UI;
using SilkroadAIBot.Core.Helpers;
using WinApp = System.Windows.Forms.Application;
using Microsoft.Extensions.DependencyInjection;
using SilkroadAIBot.Application.Interfaces;

namespace SilkroadAIBot
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Register CodePagesEncodingProvider for EUC-KR (949) support
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            // v1.1.15: Load configuration before initializing anything else
            SilkroadAIBot.Core.Configuration.ConfigManager.Load();

            // Check for headless mode
            bool headless = false;
            foreach (var arg in args)
            {
                if (arg.ToLower() == "--headless") headless = true;
            }

            if (headless)
            {
                // Config loaded globally at start
/*
                var bot = new HeadlessBot();
                bot.Start(SilkroadAIBot.Core.Configuration.ConfigManager.Config);
                
                // Graceful shutdown
                Console.CancelKeyPress += (s, e) => {
                    e.Cancel = true;
                    bot.Stop();
                    Environment.Exit(0);
                };

                bot.RunAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
*/
                return;
            }

            // Set up Global Crash Reporting
            WinApp.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            WinApp.ThreadException += (s, e) => CrashReporter.Report(e.Exception, "WinForms ThreadException");
            AppDomain.CurrentDomain.UnhandledException += (s, e) => CrashReporter.Report(e.ExceptionObject as Exception, "AppDomain UnhandledException");

            ApplicationConfiguration.Initialize();
            
            try 
            {
                var serviceProvider = Bootstrapper.BuildServiceProvider();
                var mainForm = serviceProvider.GetRequiredService<MainForm>();
                WinApp.Run(mainForm);
            }
            catch (Exception ex)
            {
                CrashReporter.Report(ex, "Main Application.Run Loop");
            }
        }
    }
}
