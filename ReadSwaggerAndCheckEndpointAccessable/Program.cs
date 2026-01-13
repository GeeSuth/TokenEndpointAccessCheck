using System.Text.Json;
using System.Net.Http.Headers;
using System.Text;
using Argx.Parsing; // Required for Console.OutputEncoding

public class Program
{
    // --- Model Classes (Keep these unchanged) ---
    public class SwaggerDefinition
    {
        public Dictionary<string, PathItem> paths { get; set; }
        public List<Server> servers { get; set; }
        public string host { get; set; }
        public List<string> schemes { get; set; }
    }

    public class Server { public string url { get; set; } }
    public class PathItem
    {
        public Operation get { get; set; }
        public Operation post { get; set; }
        public Operation put { get; set; }
        public Operation delete { get; set; }
    }
    public class Operation { }

    // --- Main Logic ---

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // 1. Awesome Banner and Configuration Display
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(@"




 _____                                                                                                    _____ 
( ___ )                                                                                                  ( ___ )
 |   |~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~|   | 
 |   |   _____     _              _____           _             _       _      _                          |   | 
 |   |  |_   _|__ | | _____ _ __ | ____|_ __   __| |_ __   ___ (_)_ __ | |_   / \   ___ ___ ___  ___ ___  |   | 
 |   |    | |/ _ \| |/ / _ \ '_ \|  _| | '_ \ / _` | '_ \ / _ \| | '_ \| __| / _ \ / __/ __/ _ \/ __/ __| |   | 
 |   |    | | (_) |   <  __/ | | | |___| | | | (_| | |_) | (_) | | | | | |_ / ___ \ (_| (_|  __/\__ \__ \ |   | 
 |   |    |_|\___/|_|\_\___|_| |_|_____|_| |_|\__,_| .__/ \___/|_|_| |_|\__/_/   \_\___\___\___||___/___/ |   | 
 |   |   / ___| |__   ___  ___| | __               |_|                                                    |   | 
 |   |  | |   | '_ \ / _ \/ __| |/ /                                                                      |   | 
 |   |  | |___| | | |  __/ (__|   <                                                                       |   | 
 |   |   \____|_| |_|\___|\___|_|\_\                                                                      |   | 
 |   |                                                                                                    |   | 
 |___|~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~|___| 
(_____)                                                                                                  (_____)



                        
                       ");
        Console.ResetColor();
        Console.WriteLine("    ⚡ API Token Verification Tester ⚡\n");


        var parser = new ArgumentParser(
              app: "TokenCheck",
              description: " Checking Permission Limititon ");

        parser.AddOption<string>("--swaggerUrl", usage: "Swagger URL");
        parser.AddOption<string>("--baseUrl", usage: "Endpoints URL");
        parser.AddOption<string>("--token", usage: " Session Token ");


        // Parse the arguments

        var pargs = parser.Parse(args);

        var baseUrl = pargs.GetValue<string>("baseUrl");
        var swaggerUrl = pargs.GetValue<string>("swaggerUrl");
        var token = pargs.GetValue<string>("token") ?? "";


        baseUrl = baseUrl.TrimEnd('/');

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[Config] API Base URL: {baseUrl}");
        Console.WriteLine($"[Config] Swagger URL: {swaggerUrl}");
        Console.WriteLine($"[Config] Token: {token.Substring(0, 10)}... (Hidden)");
        Console.ResetColor();
        Console.WriteLine("\n--------------------------------------------------------------------------------\n");


        try
        {
            // Read and Parse the Swagger JSON from URL
            using var jsonStream = await ReadJsonSwaggerRequest(swaggerUrl);
            using var reader = new StreamReader(jsonStream);
            var jsonString = await reader.ReadToEndAsync();
            
            var swagger = JsonSerializer.Deserialize<SwaggerDefinition>(jsonString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (swagger == null || swagger.paths == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n❌ ERROR: Failed to parse Swagger file or 'paths' section is missing.");
                Console.ResetColor();
                return;
            }

            Console.WriteLine("--- RESULTS TABLE ---");
            
            // Print the table header
            PrintResultsHeader();

            // Start Requesting Endpoints
            await MakeRequests(swagger, baseUrl, token);


            Console.WriteLine($"Found EndPoint: {swagger.paths.Count}");


            Console.WriteLine("--------------------------------------------------------------------------------");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ A fatal error occurred: {ex.Message}");
            Console.ResetColor();
        }

        Console.WriteLine("\n✅ Check Complete. Press any key to exit.");
        Console.ReadKey();
    }

    private static void PrintResultsHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        // Adjust column widths as needed (e.g., Method: 6, Status: 10, Path: 60)
        Console.WriteLine($"{"METHOD",-6} | {"STATUS",-15} | {"PATH",-65}");
        Console.WriteLine("--------------------------------------------------------------------------------");
        Console.ResetColor();
    }
    
    // --- Helper Method to Read Swagger from URL (Unchanged) ---
    private static async Task<Stream> ReadJsonSwaggerRequest(string swaggerUrl)
    {
        using var client = new HttpClient(); 
        
        try
        {
            Console.WriteLine($"Attempting to fetch Swagger from: {swaggerUrl}");
            
            // Skip certificate validation for localhost (common for ASP.NET dev servers)
            // NOTE: Only use this for development/testing, NEVER for production code!
            if (swaggerUrl.Contains("localhost"))
            {
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = 
                    (message, cert, chain, sslPolicyErrors) => true;
                return await new HttpClient(handler).GetStreamAsync(swaggerUrl);
            }
            
            var response = await client.GetAsync(swaggerUrl);
            response.EnsureSuccessStatusCode();

            Console.WriteLine("✅ Successfully fetched Swagger content.");
            return await response.Content.ReadAsStreamAsync();
        }
        catch (HttpRequestException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ ERROR fetching Swagger from URL: {swaggerUrl}");
            Console.WriteLine($"   Details: {ex.Message}");
            Console.ResetColor();
            throw;
        }
    }

    // --- Helper method to determine status code color and icon ---
    private static void SetConsoleColorAndIcon(int statusCode, out string icon, out ConsoleColor color)
    {
        if (statusCode >= 200 && statusCode <= 299)
        {
            color = ConsoleColor.Green;
            icon = "✅ SUCCESS";
        }
        else if (statusCode == 401 || statusCode == 403)
        {
            color = ConsoleColor.Red;
            icon = "🔒 DENIED";
        }
        else if (statusCode >= 400 && statusCode <= 499)
        {
            color = ConsoleColor.DarkYellow;
            icon = "⚠️ CLIENT ERR";
        }
        else if (statusCode >= 500)
        {
            color = ConsoleColor.Red;
            icon = "🔥 SERVER ERR";
        }
        else
        {
            color = ConsoleColor.Gray;
            icon = "❓ UNKNOWN";
        }
    }

    // --- Method to predict parameter types (by trying common types) and test endpoint ---
    private static async Task TryRequestWithPredictedParameters(HttpClient client, string httpMethod, string fullUrl, string path)
    {
        // Without parameter metadata, try common types in order: Guid, number, string.
        // This increases the chance of matching the expected type instead of guessing by name only.
        var paramReplacer = new System.Text.RegularExpressions.Regex(@"\{(.*?)\}");
        var attempts = new List<(string label, Func<string> valueFactory)>
        {
            ("guid", () => Guid.NewGuid().ToString()),
            ("number", () => "1"),
            ("string", () => "test")
        };

        foreach (var attempt in attempts)
        {
            var predictedUrl = paramReplacer.Replace(fullUrl, _ => attempt.valueFactory());

            try
            {
                using var request = new HttpRequestMessage(new HttpMethod(httpMethod), predictedUrl);

                if (httpMethod == "POST" || httpMethod == "PUT")
                {
                    request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                }

                var response = await client.SendAsync(request);

                SetConsoleColorAndIcon((int)response.StatusCode, out string icon, out ConsoleColor color);

                Console.ForegroundColor = color;
                string statusText = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                string statusOutput = $"{icon} ({statusText})";
                Console.WriteLine($"{httpMethod,-6} | {statusOutput,-15} | {path} (param:{attempt.label})");
                Console.ResetColor();

                // Stop after first non-404 response to avoid unnecessary calls
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{httpMethod,-6} | {"ERROR",-15} | {path} (param:{attempt.label})");
                Console.WriteLine($"   Exception: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    // --- MakeRequests Method (Updated for Table Output) ---
    private static async Task MakeRequests(SwaggerDefinition swagger, string baseUrl, string token)
    {
        using var client = new HttpClient();
        
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }


        foreach (var pathEntry in swagger.paths)
        {
            var path = pathEntry.Key;
            var pathItem = pathEntry.Value;

            var methods = new Dictionary<string, Operation>
            {
                { "GET", pathItem.get },
                { "POST", pathItem.post },
                { "PUT", pathItem.put },
                { "DELETE", pathItem.delete }
            };

            foreach (var methodEntry in methods)
            {
                var httpMethod = methodEntry.Key;
                var operation = methodEntry.Value;

                if (operation != null)
                {
                    var fullUrl = $"{baseUrl}{path}";

                    if (fullUrl.Contains('{'))
                    {
                        await TryRequestWithPredictedParameters(client, httpMethod, fullUrl, path);
                        continue;
                    }
                    
                    try
                    {
                        using var request = new HttpRequestMessage(new HttpMethod(httpMethod), fullUrl);
                        
                        if (httpMethod == "POST" || httpMethod == "PUT")
                        {
                            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                        }

                        var response = await client.SendAsync(request);

                        SetConsoleColorAndIcon((int)response.StatusCode, out string icon, out ConsoleColor color);
                        
                        // Print the result row using the defined formatting
                        Console.ForegroundColor = color;
                        string statusText = $"{(int)response.StatusCode} {response.ReasonPhrase}";
                        string statusOutput = $"{icon} ({statusText})";
                        
                        Console.WriteLine($"{httpMethod,-6} | {statusOutput,-15} | {path}");
                        Console.ResetColor();

                        await response.Content.ReadAsStringAsync(); 
                    }
                    catch (HttpRequestException)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{httpMethod,-6}] | {"CONNECTION ERR",-15} | {path}");
                        Console.ResetColor();
                    }
                }
            }
        }
    }
}