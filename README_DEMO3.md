# Interview Assistant — Demo 3: Hierarchical Orchestration

A C# (.NET 9) console application that extends Demo 2's multi-agent pipeline into a **hierarchical orchestration** pattern using the **Microsoft Agent Framework (MAF)** with **Azure OpenAI**. A single LLM-driven `OrchestratorAgent` autonomously drives the full interview pipeline — ingestion, classification, planning, human review, and evaluation — by calling specialist agents registered as **AIFunction tools**.

---

## 0. Prerequisites

Same as Demo 1 and Demo 2:

| Requirement | Version / Notes |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0 or later |
| Azure OpenAI resource | A deployed chat model (e.g., `gpt-4o`) |
| Azure CLI *(optional)* | Required only if using `AzureCliCredential` instead of an API key |

---

## 1. Setup — Packages

No new NuGet packages are required beyond Demo 2. The same set applies:

| Package | Version |
|---|---|
| `Azure.AI.OpenAI` | 2.8.0-beta.1 |
| `Azure.Identity` | 1.19.0 |
| `DotNetEnv` | 3.1.1 |
| `Microsoft.Agents.AI.OpenAI` | 1.0.0-rc1 |
| `Microsoft.Agents.AI.Workflows` | 1.0.0-rc1 |

---

## 2. Configure Environment

Same `.env` file as Demo 1 and Demo 2:

```env
AZURE_OPENAI_ENDPOINT=https://<your-resource>.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT=<your-deployment-name>

# Optional — omit to use Azure CLI credentials (az login) instead
AZURE_OPENAI_API_KEY=<your-api-key>
```

---

## 3. Run

Demo 3 adds a third `--mode` value: `hierarchical`.

```bash
# Run with the hierarchical orchestrator (Demo 3)
dotnet run -- --mode hierarchical

# Specify role and resume explicitly
dotnet run -- --mode hierarchical --role "Staff Engineer" --resume path/to/resume.txt

# Demo 2 modes still work unchanged
dotnet run -- --mode simple
dotnet run -- --mode workflow
```

| Argument | Default | Description |
|---|---|---|
| `--mode` | `simple` | `simple`, `workflow` (Demo 2), or `hierarchical` (Demo 3) |
| `--role` | `Senior Software Engineer` | Target role passed to the orchestrator |
| `--resume` | `assets/resumes/jane_doe.txt` | Path to the plain-text resume file |

> **Note:** In `hierarchical` mode, interview notes are collected **upfront** (before any agent runs) so the orchestrator has the full context from the start. In `simple`/`workflow` modes, notes are collected interactively at step 4.

---

## 4. What Demo 3 Shows

Demo 3 introduces one new concept: **tool-calling orchestration**. Instead of `Program.cs` sequencing each agent step-by-step, a single `OrchestratorAgent` receives all context and autonomously decides when to call each specialist.

### 4.1 — `AgentFactory` updated to accept tools

`AgentFactory.Create` gained an optional `IList<AITool> tools` parameter, allowing an agent to be created with a set of callable tools:

```csharp
// Before (Demo 2):
public static AIAgent Create(string name, string instructions) =>
    _client.GetChatClient(_deployment)
           .AsAIAgent(instructions: instructions, name: name);

// After (Demo 3):
public static AIAgent Create(
    string name,
    string instructions,
    IList<AITool> tools = null) =>
    _client.GetChatClient(_deployment)
           .AsAIAgent(instructions: instructions, name: name, tools: tools);
```

### 4.2 — New `Orchestrator` system prompt

A new `AgentPrompts.Orchestrator` constant defines the LLM's role, the exact tool names it must call, and the required sequence:

```
Tools available:
- ingest_resume      : Extract a structured ResumeProfile JSON from raw resume text.
- classify_seniority : Classify the candidate seniority from a ResumeProfile JSON.
- plan_interview     : Produce an InterviewPlan JSON given role, profile, and seniority.
- human_review       : Show the InterviewPlan to the hiring manager; returns APPROVED or REJECTED:<feedback>.
- evaluate_candidate : Score the candidate given the profile, plan, and interview notes.

Required sequence:
1. Call ingest_resume with the full resume text.
2. Call classify_seniority with the ResumeProfile JSON returned in step 1.
3. Call plan_interview with: the target role string, the ResumeProfile JSON, and the SeniorityAssessment JSON.
4. Call human_review with the InterviewPlan JSON from step 3.
   - If APPROVED, proceed to step 5.
   - If REJECTED:<feedback>, revise the plan and call human_review again. Repeat until APPROVED.
5. Call evaluate_candidate with the ResumeProfile JSON, the approved InterviewPlan JSON, and the interview notes.
```

### 4.3 — Specialist agents wrapped as `AIFunction` tools

Each specialist `AIAgent` is wrapped using `.AsAIFunction(AIFunctionFactoryOptions)`, turning it into a tool the orchestrator can invoke by name:

```csharp
var ingestTool = ingestionAgent.AsAIFunction(
    new AIFunctionFactoryOptions
    {
        Name        = "ingest_resume",
        Description = "Extract a structured ResumeProfile JSON from raw resume text."
    });

var seniorityTool = seniorityAgent.AsAIFunction(...);
var plannerTool   = plannerAgent.AsAIFunction(...);
var evaluatorTool = evaluatorAgent.AsAIFunction(...);
```

### 4.4 — Human review wired as an `AIFunction` console tool

The human-in-the-loop approval step is wrapped as a plain C# delegate using `AIFunctionFactory.Create`. The `[Description]` attribute on the parameter communicates to the LLM what value to pass:

```csharp
var humanReviewTool = AIFunctionFactory.Create(
    method: ([Description("The InterviewPlan JSON to review")] string planJson) =>
    {
        DisplayInterviewPlan(planJson);
        Console.Write("Approve this plan? (y/n): ");
        var answer = (Console.ReadLine() ?? "").Trim();
        if (answer.StartsWith("y", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult("APPROVED");
        Console.Write("Feedback (one sentence): ");
        var feedback = Console.ReadLine() ?? "";
        return Task.FromResult($"REJECTED: {feedback}");
    },
    name:        "human_review",
    description: "Show the draft InterviewPlan to the hiring manager and return APPROVED or REJECTED:<feedback>.");
```

### 4.5 — Single `RunAsync` call drives the entire pipeline

All five tools are bundled and passed to the orchestrator. MAF's built-in tool-call loop handles all intermediate LLM ↔ tool round-trips automatically:

```csharp
IList<AITool> tools = [ingestTool, seniorityTool, plannerTool, humanReviewTool, evaluatorTool];

var orchestrator = AgentFactory.Create(
    name:         "Orchestrator",
    instructions: AgentPrompts.Orchestrator,
    tools:        tools);

// One call — MAF resolves all tool calls internally
var result = await orchestrator.RunAsync(prompt, cancellationToken: cancellationToken);
```

---

## 5. Architecture: Hierarchical vs. Sequential

| Aspect | Demo 2 (`simple` / `workflow`) | Demo 3 (`hierarchical`) |
|---|---|---|
| Who sequences the steps | `Program.cs` / `SimplePipelineRunner` | `OrchestratorAgent` (LLM-driven) |
| How specialists are invoked | Direct `await agent.RunAsync(...)` calls | Tool calls dispatched by MAF |
| Human review wiring | Console code in `Program.cs` | `AIFunctionFactory.Create` delegate |
| HITL retry loop | Manual `if (!approved)` block | Orchestrator re-calls `plan_interview` + `human_review` until `APPROVED` |
| Number of top-level `await` calls | One per step | One total (`orchestrator.RunAsync`) |
| Notes collection timing | After step 3 (interactive) | Upfront, before any agent runs |

---

## 6. `HierarchicalOrchestratorRunner` — Internal Details

The runner lives in `Workflows/HierarchicalOrchestratorRunner.cs` and has three responsibilities beyond tool assembly:

#### `DisplayInterviewPlan(string planJson)`

A rich console renderer that parses the plan JSON and prints rounds, questions, and the evaluation rubric in a formatted layout. Used by the `human_review` tool.

#### `TryRepairJson(string json)`

A single-pass JSON repair utility that handles two common LLM output defects:
- **String-escaped payloads** — when the model returns `{\n  \"role\": ...}` instead of raw JSON. The method unescapes `\n`, `\r`, `\t`, `\"`, and `\\` before parsing.
- **Structural mismatches** — when closing characters don't match the open bracket on the stack (e.g., a `}` appearing while an array is open). The method inserts the expected closer(s) to produce valid JSON.

```csharp
// Handles LLM-emitted string-escaped JSON:
if (json.StartsWith("{\\") || json.Contains("\\n"))
    json = json.Replace("\\n", "\n").Replace("\\\"", "\"") /* ... */;

// Stack-based structural repair:
if (c == '}')
{
    while (stack.Count > 0 && stack.Peek() == '[') { result.Append(']'); stack.Pop(); }
    if (stack.Count > 0 && stack.Peek() == '{') stack.Pop();
}
```

---

## 7. End-to-End Flow

```
dotnet run -- --mode hierarchical
    │
    ├── Collect interview notes (upfront, before agents run)
    │
    └── orchestrator.RunAsync(prompt)   ← single await
            │
            ├─ tool call: ingest_resume(resumeText)
            │       └── ingestionAgent.RunAsync(...)  →  ResumeProfile JSON
            │
            ├─ tool call: classify_seniority(profileJson)
            │       └── seniorityAgent.RunAsync(...)  →  SeniorityAssessment JSON
            │
            ├─ tool call: plan_interview(role, profileJson, seniorityJson)
            │       └── plannerAgent.RunAsync(...)    →  InterviewPlan JSON
            │
            ├─ tool call: human_review(planJson)
            │       └── console prompt → "APPROVED" or "REJECTED: <feedback>"
            │               └── if REJECTED: loop back to plan_interview
            │
            ├─ tool call: evaluate_candidate(profileJson, planJson, notes)
            │       └── evaluatorAgent.RunAsync(...)  →  EvaluationResult JSON
            │
            └── result.Text  →  deserialize EvaluationResult  →  print to console
```

---

## 8. Expected Output

```
Enter interview notes now (one bullet per line, blank line to finish),
or press Enter immediately to skip:

> Strong system design instincts
> Solid on async patterns

--- OrchestratorAgent running (ingest → classify → plan → review → evaluate) ---

[OrchestratorAgent] Starting end-to-end pipeline...

=== [Orchestrator] Draft Interview Plan for Review ===

------------------------------------------------------------
  Role   : Senior Software Engineer
  Level  : Senior
  Summary: A 45-minute interview focused on distributed systems and .NET depth.
------------------------------------------------------------

  Round 1 -- Experience Deep Dive  (15 min)
  ..................................................
    Q1. Walk me through your most complex distributed system at Acme Corp.
    Q2. How did you handle data consistency across Cosmos DB and SQL?
    Q3. Describe a time you had to redesign a service under production pressure.

  Round 2 -- System Design  (20 min)
  ..................................................
    Q1. Design a rate-limited API gateway that handles 50k RPS.
    Q2. How would you approach sharding a multi-tenant Cosmos DB container?

  Round 3 -- Values & Role Fit  (10 min)
  ..................................................
    Q1. Tell me about a time you disagreed with an architectural decision.

  ------------------------------------------------------------
  Evaluation Rubric
  ------------------------------------------------------------

  > Technical Depth
      - Demonstrates ownership of complex .NET/Azure systems
      - Can reason about trade-offs in distributed design

  > Communication
      - Articulates decisions clearly without prompting

  ------------------------------------------------------------

Approve this plan? (y/n): y

=== Result (via OrchestratorAgent) ===

Score          : 8/10
Recommendation : Lean Hire
Summary        : Jane demonstrates strong senior-level competencies with deep Azure platform knowledge ...

Strengths:
  + Excellent async and distributed systems knowledge
  + Strong ownership across the stack

Risks:
  - Limited evidence of leading cross-team architecture decisions

Follow-ups:
  ? Ask about experience driving RFC/design doc processes at scale
```

---

## 9. Repo Layout — Demo 3 Additions

```
InterviewAssistant/
├── Program.cs                              # UPDATED — hierarchical mode dispatch + upfront notes collection
├── Agents/
│   ├── AgentFactory.cs                     # UPDATED — Create() gains optional IList<AITool> tools parameter
│   ├── AgentPrompts.cs                     # UPDATED — adds Orchestrator prompt constant
│   └── JsonAgentRunner.cs                  # Unchanged
├── Models/                                 # Unchanged
├── Pipelines/
│   └── SimplePipelineRunner.cs             # Unchanged
├── Workflows/
│   ├── InterviewWorkflowRunner.cs          # Unchanged
│   └── HierarchicalOrchestratorRunner.cs  # NEW — tool assembly, orchestrator creation, JSON repair
├── assets/
│   └── resumes/
│       └── jane_doe.txt
├── InterviewAssistant.csproj
└── InterviewAssistant.sln
```
## 9.a What MAF does automatically inside orchestrator.RunAsync
When `orchestrator.RunAsync(prompt)` is called, MAF takes care of the entire tool-call loop internally. The orchestrator agent's response is monitored for any tool call patterns (e.g., `{"tool": "ingest_resume", "arguments": {...}}`). When a tool call is detected:
1. MAF extracts the tool name and arguments from the LLM's response.
2. MAF looks up the corresponding `AITool` in the orchestrator's registered tools
3. MAF invokes the tool's function (e.g., calls `ingestionAgent.RunAsync(...)` for `ingest_resume`) and awaits the result.
4. MAF takes the tool's output and feeds it back into the orchestrator agent as context for the next turn.
5. This loop continues automatically until the orchestrator agent produces a final response that doesn't contain any more tool calls, at which point `RunAsync` returns that final output to the caller.
6. This means that the entire sequence of calling multiple agents, handling human review loops, and passing intermediate results is managed seamlessly by MAF without any additional code needed in `Program.cs` or the runner.
7. The orchestrator agent can call tools in any order, loop back to previous steps, and conditionally decide which tools to call based on the evolving context, giving it full autonomy to drive the interview process end-to-end.

```
RunAsync(prompt)
  │
  ├─► LLM emits tool_call "ingest_resume"(resumeText)
  │       MAF invokes ingestionAgent.RunAsync(resumeText)
  │       Result (ResumeProfile JSON) appended as tool-role message
  │
  ├─► LLM emits tool_call "classify_seniority"(profileJson)
  │       MAF invokes seniorityAgent.RunAsync(profileJson)
  │       Result (SeniorityAssessment JSON) appended
  │
  ├─► LLM emits tool_call "plan_interview"(role, profileJson, seniorityJson)
  │       MAF invokes plannerAgent.RunAsync(combined input)
  │       Result (InterviewPlan JSON) appended
  │
  ├─► LLM emits tool_call "human_review"(planJson)
  │       MAF invokes humanReviewTool → console I/O → "APPROVED" or "REJECTED:..."
  │       [If REJECTED: LLM calls plan_interview again with feedback, then human_review again]
  │
  ├─► LLM emits tool_call "evaluate_candidate"(profileJson, planJson, notes)
  │       MAF invokes evaluatorAgent.RunAsync(combined input)
  │       Result (EvaluationResult JSON) appended
  │
  └─► LLM emits final text response → EvaluationResult JSON string
        RunAsync returns AgentResult with .Text = that JSON
```

---

## 10. Key Concepts Illustrated

| Concept | Where |
|---|---|
| Wrapping an `AIAgent` as an `AIFunction` tool | `HierarchicalOrchestratorRunner.cs` — `.AsAIFunction(AIFunctionFactoryOptions)` |
| Wrapping a C# delegate as an `AIFunction` tool | `HierarchicalOrchestratorRunner.cs` — `AIFunctionFactory.Create` |
| Using `[Description]` to guide the LLM on tool parameter values | `HierarchicalOrchestratorRunner.cs` — `human_review` delegate parameter |
| Passing a tool list to `AgentFactory.Create` | `AgentFactory.cs` — `IList<AITool> tools` parameter on `AsAIAgent` |
| LLM-driven conditional looping (plan → review → revise) | `AgentPrompts.Orchestrator` — step 4 retry instruction |
| MAF auto-resolving a full tool-call loop in one `RunAsync` | `HierarchicalOrchestratorRunner.cs` — `orchestrator.RunAsync(prompt)` |
| Defensive JSON repair for LLM output | `HierarchicalOrchestratorRunner.cs` — `TryRepairJson` |
| Formatted console rendering from raw JSON | `HierarchicalOrchestratorRunner.cs` — `DisplayInterviewPlan` |
