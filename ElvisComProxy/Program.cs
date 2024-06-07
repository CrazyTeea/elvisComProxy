using System.Net;
using System.Text;

namespace ElvisComProxy
{
    class HttpServer
    {
        static HttpListener _listener;
        static string _url = "http://localhost:8001/";
        static int _pageViews = 0;
        static int _requestCount = 0;

        static string _pageData =
            "<!DOCTYPE>" +
            "<html>" +
            "  <head>" +
            "    <title>HttpListener Example</title>" +
            "  </head>" +
            "  <body>" +
            "    <p>Page Views: {0}</p>" +
            "    <form method=\"post\" action=\"shutdown\">" +
            "      <input type=\"submit\" value=\"Shutdown\" {1}>" +
            "    </form>" +
            "  </body>" +
            "</html>";


        private static async Task HandleIncomingConnections()
        {
            var runServer = true;

            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
            {
                // Will wait here until we hear from a connection
                var ctx = await _listener.GetContextAsync();

                // Peel out the requests and response objects
                var req = ctx.Request;
                var resp = ctx.Response;

                // Print out some info about the request
                Console.WriteLine("Request #: {0}", ++_requestCount);
                Console.WriteLine(req.Url?.ToString());
                Console.WriteLine(req.HttpMethod);
                Console.WriteLine(req.UserHostName);
                Console.WriteLine(req.UserAgent);
                Console.WriteLine();

                // If `shutdown` url requested w/ POST, then shutdown the server after serving the page
                if (req is { HttpMethod: "POST", Url.AbsolutePath: "/shutdown" })
                {
                    Console.WriteLine("Shutdown requested");
                    runServer = false;
                }
                
                if (req is { HttpMethod: "POST", Url.AbsolutePath: "/command" })
                {
                    Console.WriteLine(req.Url);
                }

                // Make sure we don't increment the page views counter if `favicon.ico` is requested
                if (req.Url?.AbsolutePath != "/favicon.ico")
                    _pageViews += 1;

                // Write the response info
                var disableSubmit = !runServer ? "disabled" : "";
                var data = Encoding.UTF8.GetBytes(String.Format(_pageData, _pageViews, disableSubmit));
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }


        public static void Main(string[] args)
        {
            // Create a Http server and start listening for incoming connections
            _listener = new HttpListener();
            _listener.Prefixes.Add(_url);
            _listener.Start();
            Console.WriteLine("Listening for connections on {0}", _url);

            // Handle requests
            var listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            // Close the listener
            _listener.Close();
        }
    }
}