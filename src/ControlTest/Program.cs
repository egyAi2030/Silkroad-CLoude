using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ControlTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int port = 6001;
            Console.WriteLine($"Starting Standalone Control API on port {port}...");
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            try 
            {
                listener.Start();
                Console.WriteLine($"API Listening on http://localhost:{port}/");
                Console.WriteLine("I HAVE FULL CONTROL OF THIS INTERFACE.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start: {ex.Message}");
                return;
            }

            while (true)
            {
                try 
                {
                    var context = await listener.GetContextAsync();
                    var req = context.Request;
                    var res = context.Response;

                    string path = req.Url?.AbsolutePath.ToLower() ?? "/";
                    Console.WriteLine($"[{DateTime.Now}] Received {req.HttpMethod} request for {req.Url}");

                    string responseString = "";
                    if (path == "/launch")
                    {
                        Console.WriteLine("SIMULATING CLIENT LAUNCH...");
                        responseString = "{\"status\":\"Launching\", \"message\":\"SRO Client launch sequence initiated (SIMULATED)\", \"client\":\"sro_client.exe\"}";
                    }
                    else
                    {
                        responseString = "{\"status\":\"active\", \"control\":\"full\", \"message\":\"CLI Agent has taken control of the bot interface.\"}";
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    res.ContentLength64 = buffer.Length;
                    res.ContentType = "application/json";
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    res.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling request: {ex.Message}");
                }
            }
        }
    }
}
