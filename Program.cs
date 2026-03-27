using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DotNetEnv;
using InterviewAssistant.Agents;
using InterviewAssistant.Models;
using Microsoft.Agents.AI;
using OpenAI.Chat;

Env.Load();

// Demo 1 - Single Agent: Resume Ingestion
var resumePath = args.Length > 0 ? args[0] : Path.Combine("assets", "resumes", "jane_doe.txt");

if (!File.Exists(resumePath))
{
    Console.Error.WriteLine($"Resume not found: {resumePath}");
    return;
}

var resumeText = await File.ReadAllTextAsync(resumePath);
    
AIAgent ingestionAgent = AgentFactory.Create("ResumeIngestion", AgentPrompts.ResumeIngestion);
AIAgent seniorityAgent = AgentFactory.Create("SeniorityClassifier", AgentPrompts.SeniorityClassifier);
AIAgent plannerAgent = AgentFactory.Create("InterviewPlanner", AgentPrompts.InterviewPlanner);
AIAgent evaluatorAgent = AgentFactory.Create("Evaluator", AgentPrompts.Evaluator);
    
Console.WriteLine("=== Demo 1: Single Agent ===\n");

var ingestPrompt = $"{AgentPrompts.ResumeIngestion}\n\nRESUME:\n{resumeText}";
var (profile, _) = await JsonAgentRunner.RunJsonAsync<ResumeProfile>(ingestionAgent, ingestPrompt);

Console.WriteLine($"Candidate : {profile.CandidateName}");
Console.WriteLine($"Experience: {profile.YearsExperience} years");
Console.WriteLine($"Skills    : {string.Join(", ", profile.CoreSkills.Take(8))}");
Console.WriteLine($"Red Flags : {string.Join(", ", profile.RedFlags)}");

// ── Demo 2 ──────────────────────────────────────────────────────────────────
// Step 2 — Seniority Classification
Console.WriteLine("\n--- Step 2: Seniority Classification ---\n");

var seniorityPrompt =
    $"{AgentPrompts.SeniorityClassifier}\n\nRESUME_PROFILE:\n{JsonSerializer.Serialize(profile)}";

var (seniority, _) = await JsonAgentRunner.RunJsonAsync<SeniorityAssessment>(
    seniorityAgent, seniorityPrompt);

Console.WriteLine($"Level     : {seniority.Level}  (confidence {seniority.Confidence:0.00})");
Console.WriteLine($"Rationale : {seniority.Rationale}");

// Step 3 — Interview Planning
Console.WriteLine("\n--- Step 3: Interview Planning ---\n");

var role = "Senior Software Engineer";   // or prompt the user: Console.ReadLine()

var planPrompt = new StringBuilder()
    .AppendLine(AgentPrompts.InterviewPlanner)
    .AppendLine("ROLE:").AppendLine(role)
    .AppendLine("RESUME_PROFILE:").AppendLine(JsonSerializer.Serialize(profile))
    .AppendLine("SENIORITY:").AppendLine(JsonSerializer.Serialize(seniority))
    .ToString();

var (plan, _) = await JsonAgentRunner.RunJsonAsync<InterviewPlan>(plannerAgent, planPrompt);

Console.WriteLine("\n=== Draft Interview Plan ===\n");
Console.WriteLine($"Plan summary : {plan.Summary}");
Console.WriteLine($"Role       : {plan.Role} | Level: {plan.Level}");
Console.WriteLine();

foreach (var round in plan.Rounds)
{
    Console.WriteLine($"--> [{round.Name} — {round.DurationMinutes} min]");
    foreach (var q in round.Questions) 
        Console.WriteLine($"  - {q}");

    if (round.Questions.Count > 4) Console.WriteLine("  ...");
    Console.WriteLine();
}

// ── Human-in-the-Loop Checkpoint ──────────────────────────────────────────
Console.Write("Approve this plan? (y/n): ");
var approved = (Console.ReadLine() ?? "")
    .Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);

if (!approved)
{
    Console.Write("Give feedback (e.g., 'more system design, fewer trivia'): ");
    var feedback = Console.ReadLine() ?? "";

    var revisePrompt = $@"Revise the InterviewPlan JSON below based on this feedback.
Feedback: {feedback}

Return ONLY valid InterviewPlan JSON.

{JsonSerializer.Serialize(plan)}";

    (plan, _) = await JsonAgentRunner.RunJsonAsync<InterviewPlan>(plannerAgent, revisePrompt);
    Console.WriteLine($"\n=== Revised plan ===\n: {plan.Summary}");
}

// Step 4 — Evaluation
Console.WriteLine("\n--- Step 4: Evaluation ---");
Console.WriteLine("Enter interview notes (one bullet per line, blank line to finish):\n");

var notesSb = new StringBuilder();
while (true)
{
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line)) break;
    notesSb.AppendLine($"- {line}");
}
var notes = notesSb.Length > 0 ? notesSb.ToString() : "(no notes provided; evaluate based on resume + plan only)";

var evalPrompt = new StringBuilder()
    .AppendLine(AgentPrompts.Evaluator)
    .AppendLine("RESUME_PROFILE:").AppendLine(JsonSerializer.Serialize(profile))
    .AppendLine("INTERVIEW_PLAN:").AppendLine(JsonSerializer.Serialize(plan))
    .AppendLine("INTERVIEW_NOTES:").AppendLine(notes)
    .ToString();

var (evaluation, _) = await JsonAgentRunner.RunJsonAsync<EvaluationResult>(
    evaluatorAgent, evalPrompt);

Console.WriteLine($"\nScore          : {evaluation.OverallScore}/10");
Console.WriteLine($"Recommendation : {evaluation.Recommendation}");
Console.WriteLine($"Summary        : {evaluation.Summary}");
Console.WriteLine($"\nStrengths:");
foreach (var s in evaluation.Strengths) Console.WriteLine($"  + {s}");
Console.WriteLine($"\nRisks:");
foreach (var r in evaluation.Risks) Console.WriteLine($"  - {r}");
Console.WriteLine($"\nFollow-ups:");
foreach (var f in evaluation.FollowUps) Console.WriteLine($"  ? {f}");