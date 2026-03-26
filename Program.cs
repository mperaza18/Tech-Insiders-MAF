using DotNetEnv;

Env.Load();

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

// Print the values to verify they are loaded correctly
Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine($"Deployment: {deployment}");
Console.WriteLine($"apiKey: {apiKey}");