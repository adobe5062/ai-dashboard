using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run -- <client-id> <client-secret>");
    Console.WriteLine("\nGet these from Google Cloud Console:");
    Console.WriteLine("  1. APIs & Services → Credentials → Create OAuth 2.0 Client ID");
    Console.WriteLine("  2. Application type: Desktop app");
    Console.WriteLine("  3. Enable the Google Calendar API for your project");
    return;
}

var clientId     = args[0];
var clientSecret = args[1];
const int    Port        = 8080;
var redirectUri  = $"http://localhost:{Port}/";
const string Scope       = "https://www.googleapis.com/auth/calendar.readonly";

var authUrl = "https://accounts.google.com/o/oauth2/v2/auth" +
    $"?client_id={Uri.EscapeDataString(clientId)}" +
    $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
    "&response_type=code" +
    $"&scope={Uri.EscapeDataString(Scope)}" +
    "&access_type=offline" +
    "&prompt=consent";

Console.WriteLine("Opening Google authorization in your browser...");
Console.WriteLine($"If it doesn't open, navigate to:\n{authUrl}\n");

try { Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true }); }
catch { /* user will copy the URL manually */ }

var listener = new HttpListener();
listener.Prefixes.Add(redirectUri);
listener.Start();
Console.WriteLine($"Waiting for Google to redirect to localhost:{Port}...");

var context = await listener.GetContextAsync();
var code    = context.Request.QueryString["code"];
var error   = context.Request.QueryString["error"];

var html  = string.IsNullOrEmpty(error)
    ? "<html><body style='font-family:sans-serif;padding:40px'><h2>✓ Authorized</h2><p>You can close this tab and return to the terminal.</p></body></html>"
    : $"<html><body style='font-family:sans-serif;padding:40px'><h2>✗ Error: {error}</h2></body></html>";
var bytes = Encoding.UTF8.GetBytes(html);
context.Response.ContentLength64 = bytes.Length;
await context.Response.OutputStream.WriteAsync(bytes);
context.Response.Close();
listener.Stop();

if (!string.IsNullOrEmpty(error))
{
    Console.WriteLine($"Authorization failed: {error}");
    return;
}

Console.WriteLine("Exchanging authorization code for tokens...");

using var http      = new HttpClient();
var tokenResp = await http.PostAsync("https://oauth2.googleapis.com/token",
    new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["code"]          = code!,
        ["client_id"]     = clientId,
        ["client_secret"] = clientSecret,
        ["redirect_uri"]  = redirectUri,
        ["grant_type"]    = "authorization_code",
    }));

var tokenJson = JsonNode.Parse(await tokenResp.Content.ReadAsStringAsync())!;

if (!tokenResp.IsSuccessStatusCode)
{
    Console.WriteLine($"Token exchange failed:\n{tokenJson}");
    return;
}

var refreshToken = tokenJson["refresh_token"]?.GetValue<string>();

if (string.IsNullOrEmpty(refreshToken))
{
    Console.WriteLine("No refresh_token in response — try running again (prompt=consent should force it).");
    return;
}

Console.WriteLine("\n========================================");
Console.WriteLine("  SUCCESS — run these AWS CLI commands:");
Console.WriteLine("========================================\n");
Console.WriteLine($"aws ssm put-parameter --name /dashboard/google/client-id     --value \"{clientId}\"     --type SecureString --overwrite");
Console.WriteLine($"aws ssm put-parameter --name /dashboard/google/client-secret --value \"{clientSecret}\" --type SecureString --overwrite");
Console.WriteLine($"aws ssm put-parameter --name /dashboard/google/refresh-token --value \"{refreshToken}\" --type SecureString --overwrite");
