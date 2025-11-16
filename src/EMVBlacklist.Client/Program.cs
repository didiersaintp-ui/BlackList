using EMVBlacklist.Client;
using EMVBlacklist.Shared.Models;

var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:5000";
var filterTypeStr = Environment.GetEnvironmentVariable("FILTER_TYPE");

Console.WriteLine($"Connecting to API at: {apiUrl}");

var client = new BlacklistClient(apiUrl);

FilterType? filterType = null;
if (!string.IsNullOrEmpty(filterTypeStr))
{
    filterType = Enum.Parse<FilterType>(filterTypeStr);
}

await client.InitializeAsync(filterType);

// Keep running and let the timer handle delta syncs
Console.WriteLine("\nClient is running. Press Ctrl+C to exit.");
Console.WriteLine("===========================================\n");

// Wait indefinitely
await Task.Delay(Timeout.Infinite);
