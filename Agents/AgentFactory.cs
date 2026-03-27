using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI.Chat;

namespace InterviewAssistant.Agents;

public static class AgentFactory
{
    private static readonly AzureOpenAIClient _client;
    private static readonly string _deployment;

    static AgentFactory()
    {
        // Retrieve environment variables
        var endpoint   = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
                         ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
        _deployment    = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
                         ?? throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT is not set.");
        var apiKey     = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        _client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    ///<summary>
    /// Creates an AIAgent backed by Azure OpenAI using a shared client and deployment.
    ///</summary>
    public static AIAgent Create(string name, string instructions) =>
        _client
            .GetChatClient(_deployment)
            .AsAIAgent(instructions: instructions, name: name);
}