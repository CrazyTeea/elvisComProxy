using System.Net;
using System.Text;
using System.IO.Ports;
using System.Text.Json;

namespace ElvisComProxy
{
    public class Command
    {
        public string Name { get; set; }
        public int? Frequency { get; set; }
        public int? Length { get; set; }

        public string? Position;

        readonly Dictionary<string, string> _positionsLeds = new Dictionary<string, string>();

        Command()
        {
            Name = "feed";
            _thread = null;
            _positionsLeds["top-left"] = "led_3";
            _positionsLeds["top-right"] = "led_4";
            _positionsLeds["bottom-left"] = "led_5";
            _positionsLeds["bottom-right"] = "led_6";
        }

        public int GetLength()
        {
            return Length ?? 750;
        }

        private Thread? _thread;

        public string feedClose()
        {
            return "*addr: feed; cmd: close;#";
        }

        public string feedOpen()
        {
            return "*addr: feed; cmd: open;#";
        }

        public string lightOpen()
        {
            return "*addr: " + _positionsLeds[Position] + ";cmd: strobe; level: 5;" + "time:" + GetLength() * 1000 +
                   ";freq: " + Frequency + ";#";
        }

        public string lightClose()
        {
            return "*addr: " + _positionsLeds[Position] + ";cmd: strobe; level: 0;" + "time:" + 0 +
                   ";freq: " + Frequency + ";#";
        }

        public string GetComputedName()
        {
            return Name == "blink" ? "light" : Name;
        }

        public void RunCmd(SerialPort serialPort)
        {
            var methodOpen = GetType().GetMethod(GetComputedName() + "Open")?.Invoke(this, null);
            var methodClose = GetType().GetMethod(GetComputedName() + "Close")?.Invoke(this, null);

            serialPort.Open();


            serialPort.Write(methodOpen?.ToString());
            Console.WriteLine(methodOpen?.ToString());
            Thread.Sleep(GetLength());
            serialPort.Write(methodClose?.ToString());
            Console.WriteLine(methodClose?.ToString());


            serialPort.Close();
        }

        public Command CreateThread()
        {
            _thread = new Thread(() =>
            {
                var serialPort = new SerialPort("COM6", 11520, Parity.None, 8, StopBits.One);
                Console.WriteLine("starting thread");
                try
                {
                    RunCmd(serialPort);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    try
                    {
                        serialPort.Close();
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception.Message);
                    }
                }

                Console.WriteLine("stopping thread");
            });
            _thread.Start();
            return this;
        }

        public static Command Create(Stimul stimul)
        {
            var c = new Command();
            c.Name = stimul.name;
            c.Frequency = stimul.frequency;
            c.Length = stimul.length;
            c.Position = stimul.position;
            return c;
        }
    }

    public class Stimul
    {
        public string? name { get; set; }
        public int? experiment_id { get; set; }
        public int? frequency { get; set; }
        public string? id { get; set; }
        public int? length { get; set; }

        public string? position { get; set; }

        public override string ToString()
        {
            return name + "| exp_id" + experiment_id + "| freq: " + frequency + "| id: " + id + "| len: " + length;
        }
    }

    internal static class HttpServer
    {
        private static HttpListener? _listener;
        private const string Url = "http://localhost:8001/";

        private static async Task HandleIncomingConnections()
        {
            var runServer = true;

            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
            {
                // Will wait here until we hear from a connection
                var ctx = await _listener?.GetContextAsync()!;

                // Peel out the requests and response objects
                var req = ctx.Request;
                var resp = ctx.Response;
                var success = true;
                var message = "";

                // If `shutdown` url requested w/ POST, then shutdown the server after serving the page
                if (req is { HttpMethod: "POST", Url.AbsolutePath: "/shutdown" })
                {
                    Console.WriteLine("Shutdown requested");
                    runServer = false;
                }

                if (req.Url != null && req.Url.AbsolutePath.Contains("command"))
                {
                    var vls = req.Url.AbsolutePath.Split("/");
                    var command = vls.Last();


                    var body = req.InputStream;
                    var encoding = req.ContentEncoding;
                    var reader = new StreamReader(body, encoding);

                    var postData = await reader.ReadToEndAsync();

                    var json = JsonSerializer.Deserialize<Stimul>(postData);

                    Console.WriteLine(json);

                    try
                    {
                        Command.Create(json).CreateThread();
                        message = "success";
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("exception!");
                        Console.WriteLine(e.Message);
                        success = false;
                        message = e.Message;
                    }
                }

                // Write the response info
                var respCtx = new
                {
                    success,
                    message
                };

                var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(respCtx));
                resp.ContentType = "application/json";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                resp.StatusCode = success ? 200 : 500;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }


        public static void Main(string[] args)
        {
            // Create a Http server and start listening for incoming connections
            _listener = new HttpListener();
            _listener.Prefixes.Add(Url);
            _listener.Start();
            Console.WriteLine("Listening for connections on {0}", Url);

            // Handle requests
            var listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            // Close the listener
            _listener.Close();
        }
    }
}