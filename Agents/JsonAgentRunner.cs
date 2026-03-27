using System.Text.Json;
using Microsoft.Agents.AI;

namespace InterviewAssistant.Agents;

/// <summary>
/// Provides a utility method to run an AIAgent with a given prompt and parse its output as JSON into a 
/// specified type.
/// </summary>
public static class JsonAgentRunner
{
    // JsonSerializerOptions configured to be case-insensitive, allow comments, and trailing commas,
    // ensuring more robust parsing of the agent's output.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling        = JsonCommentHandling.Skip,
        AllowTrailingCommas        = true
    };

    /// <summary>
    /// Runs the specified AIAgent with the provided prompt, expecting a JSON response that can be deserialized
    /// into the specified type T. The method returns a tuple containing the deserialized value and 
    /// the raw JSON string. If the agent's output is not valid JSON or does not match the expected schema, 
    /// an exception is thrown.
    /// </summary>
    public static async Task<(T Value, string Raw)> RunJsonAsync<T>(
        AIAgent agent,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var result = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var raw    = result.Text.Trim();

        try
        {
            var value = JsonSerializer.Deserialize<T>(raw, JsonOptions)
                        ?? throw new JsonException("Deserialized null");
            return (value, raw);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Agent returned non-JSON or schema mismatch. Raw:\n{raw}", ex);
        }
    }
}