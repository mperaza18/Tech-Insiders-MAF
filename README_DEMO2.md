# Interview Assistant — Demo 2: Multi-Agent Pipeline

A C# (.NET 9) console application that extends the Demo 1 single-agent pattern into a **multi-step, multi-agent pipeline** using the **Microsoft Agent Framework (MAF)** with **Azure OpenAI**. Demo 2 adds seniority classification, interview planning, a human-in-the-loop approval gate, candidate evaluation, and introduces two execution strategies: a simple sequential pipeline and a MAF `WorkflowBuilder`-based graph.

---

## 0. Prerequisites

Same as Demo 1:

| Requirement | Version / Notes |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0 or later |
| Azure OpenAI resource | A deployed chat model (e.g., `gpt-4o`) |
| Azure CLI *(optional)* | Required only if using `AzureCliCredential` instead of an API key |

---

## 1. Setup — Packages

No new NuGet packages are required beyond Demo 1. The same set applies:

| Package | Version |
|---|---|
| `Azure.AI.OpenAI` | 2.8.0-beta.1 |
| `Azure.Identity` | 1.19.0 |
| `DotNetEnv` | 3.1.1 |
| `Microsoft.Agents.AI.OpenAI` | 1.0.0-rc1 |
| `Microsoft.Agents.AI.Workflows` | 1.0.0-rc1 |

---

## 2. Configure Environment

Same `.env` file as Demo 1:

```env
AZURE_OPENAI_ENDPOINT=https://<your-resource>.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT=<your-deployment-name>

# Optional — omit to use Azure CLI credentials (az login) instead
AZURE_OPENAI_API_KEY=<your-api-key>
```

---

## 3. Run

Demo 2 introduces named command-line arguments:

```bash
# Defaults: simple pipeline, Senior Software Engineer role, jane_doe.txt resume
dotnet run

# Specify all options explicitly
dotnet run -- --mode simple --role "Staff Engineer" --resume assets/resumes/jane_doe.txt

# Run using the MAF WorkflowBuilder (graph-based) instead
dotnet run -- --mode workflow --role "Senior Software Engineer"
```

| Argument | Default | Description |
|---|---|---|
| `--mode` | `simple` | Execution strategy: `simple` (sequential pipeline) or `workflow` (MAF graph) |
| `--role` | `Senior Software Engineer` | Target role passed to the interview planner |
| `--resume` | `assets/resumes/jane_doe.txt` | Path to the plain-text resume file |

---

## 4. What Demo 2 Shows

Demo 2 extends Demo 1 with four major additions:

### 4.1 — `AgentFactory` (refactor)

Agent creation was extracted from `Program.cs` into a shared static factory. All agents are now created through a single entry point that handles Azure OpenAI client setup and credential resolution.

```csharp
// Before (Demo 1 inline):
AIAgent ingestionAgent = openAiClient
    .GetChatClient(deployment)
    .AsAIAgent(instructions: AgentPrompts.ResumeIngestion, name: "ResumeIngestion");

// After (Demo 2 factory):
AIAgent ingestionAgent = AgentFactory.Create("ResumeIngestion", AgentPrompts.ResumeIngestion);
```

### 4.2 — Three New Agents

Three new `AIAgent` instances are introduced, each backed by its own system prompt:

| Agent Name | Prompt Constant | Purpose |
|---|---|---|
| `SeniorityClassifier` | `AgentPrompts.SeniorityClassifier` | Classifies the candidate as Junior / Mid / Senior / Staff+ with a confidence score |
| `InterviewPlanner` | `AgentPrompts.InterviewPlanner` | Produces a 3-round, 45-minute interview plan grounded in the resume |
| `Evaluator` | `AgentPrompts.Evaluator` | Scores the candidate 1–10 and issues a Hire / Lean Hire / Lean No / No Hire recommendation |

### 4.3 — Four-Step Pipeline

The full end-to-end flow (steps 1–4):

```
Resume (text)
    │
    ▼
[Step 1] ResumeIngestion agent  →  ResumeProfile JSON
    │
    ▼
[Step 2] SeniorityClassifier agent  →  SeniorityAssessment JSON
    │                                   (level + confidence + rationale)
    ▼
[Step 3] InterviewPlanner agent  →  InterviewPlan JSON
    │                                (rounds, questions, rubric)
    │
    ├── Human-in-the-Loop checkpoint ──▶  Approve? (y/n)
    │                                         │ no → free-text feedback
    │                                         │      → planner revises plan
    ▼
[Step 4] Evaluator agent  →  EvaluationResult JSON
                              (score, recommendation, strengths, risks, follow-ups)
```

### 4.4 — Human-in-the-Loop Approval

After the draft interview plan is printed, the user is prompted to approve or revise it before proceeding to evaluation:

```
Approve this plan? (y/n): n
Give feedback in one sentence (e.g., 'more system design, fewer trivia'): focus more on distributed systems
```

If rejected, the planner agent is called again with the original plan JSON and the feedback, producing a revised plan.

---

## 5. Two Execution Strategies

### `simple` — `SimplePipelineRunner`

A straightforward sequential runner that calls `JsonAgentRunner.RunJsonAsync<T>` for each step in order. The typed output of each step is explicitly passed as input to the next step.

```csharp
// Pipelines/SimplePipelineRunner.cs
var (profile, seniority, plan) = await SimplePipelineRunner.RunAsync(
    ingestionAgent, seniorityAgent, plannerAgent, role, resumeText);
```

### `workflow` — `InterviewWorkflowRunner`

Uses MAF's `WorkflowBuilder` to wire agents into a directed graph. Agents are connected with `AddEdge` and executed via `InProcessExecution.RunStreamingAsync`. Streaming events (`AgentResponseUpdateEvent`) are collected per executor and the final planner output is reformatted into a typed `InterviewPlan`.

```csharp
// Workflows/InterviewWorkflowRunner.cs
var workflow = new WorkflowBuilder(ingestionAgent)
    .AddEdge(ingestionAgent, seniorityAgent)
    .AddEdge(seniorityAgent, plannerAgent)
    .Build();

await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is AgentResponseUpdateEvent update) { /* buffer per executor */ }
}
```

> **Note:** Steps 3 (human-in-the-loop approval) and 4 (evaluation) run after either strategy completes, using the returned `InterviewPlan`.

---

### 5.2 — `Simple Mode` vs `Workflow Mode`

Both modes/ runners execute the **same 3 steps** (Ingestion → Seniority → Planning) and return the same `InterviewPlan`. The difference is *how* they orchestrate the agents:

| | `SimplePipelineRunner` | `InterviewWorkflowRunner` |
|---|---|---|
| **Orchestration** | You write the sequence manually in code | MAF's `WorkflowBuilder` owns the graph |
| **Data passing** | Explicit — you take the output of step N and pass it to step N+1 | Implicit — the framework pipes outputs through edges |
| **Execution** | Sequential, awaited one at a time | Graph-based, streamed via `RunStreamingAsync` |
| **Output collection** | Each call returns a typed object immediately | You consume `WorkflowEvent` stream and buffer per executor |
| **Control flow** | Full — you can branch, retry, or short-circuit between steps | Declarative — topology is fixed at `Build()` time |
| **Transparency** | High — every step is visible in your code | Lower — framework drives the execution |

#### When to use `SimplePipelineRunner`

- The pipeline is **linear and unlikely to change**
- You need **fine-grained control** between steps (logging, validation, conditional branching)
- You want **simplicity** — easier to read, debug, and explain
- You're **prototyping** or building something small

#### When to use `InterviewWorkflowRunner` (MAF `WorkflowBuilder`)

- You want the **framework to manage routing** between agents
- The graph may **grow in complexity** (fan-out, parallel branches, cycles)
- You need **streaming events** for real-time UI updates or monitoring
- You're building toward a **production multi-agent system** where the topology is a configuration concern, not a code concern

> **Mental model:** `SimplePipelineRunner` is like writing a **script** — you call each actor in order.  
> `InterviewWorkflowRunner` is like setting up a **stage with directions** — you define who follows who, then say "action" and watch events flow.

---


## 6. New Models

| Model | File | Fields |
|---|---|---|
| `SeniorityAssessment` | `Models/SeniorityAssessment.cs` | `Level` (string), `Confidence` (double), `Rationale` (string) |
| `InterviewPlan` | `Models/InterviewPlan.cs` | `Role`, `Level`, `Summary`, `Rounds` (list of `InterviewRound`), `Rubric` (list of `RubricDimension`) |
| `InterviewRound` | `Models/InterviewPlan.cs` | `Name`, `DurationMinutes`, `Questions` (list) |
| `RubricDimension` | `Models/InterviewPlan.cs` | `Dimension`, `Signals` (list) |
| `EvaluationResult` | `Models/EvaluationResult.cs` | `OverallScore`, `Recommendation`, `Summary`, `Strengths`, `Risks`, `FollowUps` |

---

## 7. Expected Output (simple mode)

```
=== Interview Assistant — Demo 2 ===

Mode  : simple
Role  : Senior Software Engineer
Resume: assets/resumes/jane_doe.txt

--- Step 1: Resume Ingestion ---

Candidate : Jane Doe
Experience: 6 years
Skills    : C#, .NET, ASP.NET Core, Azure, SQL, Cosmos DB, Service Bus, Redis
Red Flags :

--- Step 2: Seniority Classification ---

Level     : Senior  (confidence 0.87)
Rationale : 6 years of hands-on .NET/Azure experience with ownership of ...

--- Step 3: Interview Planning ---

=== Draft Interview Plan ===

Plan summary : A 45-minute interview targeting a Senior Software Engineer ...
Role       : Senior Software Engineer | Level: Senior

--> [Experience Deep Dive — 15 min]
  - Walk me through your most complex distributed system at Acme Corp.
  - How did you handle data consistency across Cosmos DB and SQL?
  ...

Approve this plan? (y/n): y

--- Step 4: Evaluation ---
Enter interview notes (one bullet per line, blank line to finish):

> Strong system design answers
> Hesitant on concurrency

Score          : 8/10
Recommendation : Lean Hire
Summary        : Jane demonstrates strong senior-level competencies ...

Strengths:
  + Deep Azure/cloud experience
  + Clear ownership mindset

Risks:
  - Some gaps in concurrency / threading

Follow-ups:
  ? Deep-dive on thread-safety patterns in high-throughput services
```

---

## 8. Repo Layout — Demo 2 Additions

```
InterviewAssistant/
├── Program.cs                          # Entry point — arg parsing, agent creation, mode dispatch
├── Agents/
│   ├── AgentFactory.cs                 # NEW — shared factory for AIAgent creation
│   ├── AgentPrompts.cs                 # UPDATED — adds SeniorityClassifier, InterviewPlanner, Evaluator prompts
│   └── JsonAgentRunner.cs              # Unchanged
├── Models/
│   ├── ResumeProfile.cs                # Unchanged
│   ├── SeniorityAssessment.cs          # NEW — level, confidence, rationale
│   ├── InterviewPlan.cs                # NEW — rounds, rubric
│   └── EvaluationResult.cs             # NEW — score, recommendation, strengths, risks, follow-ups
├── Pipelines/
│   └── SimplePipelineRunner.cs        # NEW — sequential steps 1-3
├── Workflows/
│   └── InterviewWorkflowRunner.cs     # NEW — MAF WorkflowBuilder graph for steps 1-3
├── assets/
│   └── resumes/
│       └── jane_doe.txt
├── InterviewAssistant.csproj
└── InterviewAssistant.sln
```

---

## 9. Key Concepts Illustrated

| Concept | Where |
|---|---|
| Centralizing agent creation behind a factory | `Agents/AgentFactory.cs` |
| Passing typed JSON output from one agent as input to the next | `Pipelines/SimplePipelineRunner.cs` |
| Wiring agents into a directed graph with `WorkflowBuilder` | `Workflows/InterviewWorkflowRunner.cs` |
| Streaming workflow events with `WorkflowEvent` / `AgentResponseUpdateEvent` | `Workflows/InterviewWorkflowRunner.cs` |
| Human-in-the-loop checkpoint with plan revision | `Program.cs` — post-pipeline approval block |
| Multiple execution strategies selected at runtime via CLI args | `Program.cs` — `--mode simple\|workflow` |
| Strongly-typed models bound to each agent's JSON schema | `Models/SeniorityAssessment.cs`, `InterviewPlan.cs`, `EvaluationResult.cs` |
