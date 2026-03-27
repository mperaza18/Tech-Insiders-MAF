using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DotNetEnv;
using InterviewAssistant.Agents;
using InterviewAssistant.Models;
using Microsoft.Agents.AI;
using OpenAI.Chat;

Env.Load();

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

// Demo 1 - Single Agent: Resume Ingestion
var resumePath = args.Length > 0 ? args[0] : Path.Combine("assets", "resumes", "jane_doe.txt");

if (!File.Exists(resumePath))
{
    Console.Error.WriteLine($"Resume not found: {resumePath}");
    return;
}

var resumeText = await File.ReadAllTextAsync(resumePath);

AzureOpenAIClient openAiClient = string.IsNullOrWhiteSpace(apiKey)
    ? new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    : new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    
AIAgent ingestionAgent = openAiClient
    .GetChatClient(deployment)
    .AsAIAgent(instructions: AgentPrompts.ResumeIngestion, name: "ResumeIngestion");
    
Console.WriteLine("=== Demo 1: Single Agent ===\n");

var ingestPrompt = $"{AgentPrompts.ResumeIngestion}\n\nRESUME:\n{resumeText}";
var (profile, _) = await JsonAgentRunner.RunJsonAsync<ResumeProfile>(ingestionAgent, ingestPrompt);

Console.WriteLine($"Candidate : {profile.CandidateName}");
Console.WriteLine($"Experience: {profile.YearsExperience} years");
Console.WriteLine($"Skills    : {string.Join(", ", profile.CoreSkills.Take(8))}");
Console.WriteLine($"Red Flags : {string.Join(", ", profile.RedFlags)}");