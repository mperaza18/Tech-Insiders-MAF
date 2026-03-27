using System.Text.Json.Serialization;

namespace InterviewAssistant.Models;

public sealed class EvaluationResult
{
    [JsonPropertyName("overallScore")]    public int    OverallScore    { get; set; }
    [JsonPropertyName("recommendation")] public string Recommendation  { get; set; } = "";
    [JsonPropertyName("summary")]        public string Summary         { get; set; } = "";
    [JsonPropertyName("strengths")]      public List<string> Strengths  { get; set; } = new();
    [JsonPropertyName("risks")]          public List<string> Risks      { get; set; } = new();
    [JsonPropertyName("followUps")]      public List<string> FollowUps  { get; set; } = new();
}