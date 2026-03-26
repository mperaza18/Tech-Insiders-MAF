# 🤖 Interview Assistant — AI-Powered Interview Preparation

A C# (.NET 9) console application that automates the full interview preparation lifecycle using the **Microsoft Agent Framework (MAF)** and **Azure OpenAI**. Given a candidate's plain-text resume and a target role, the system produces a structured seniority assessment, a multi-round interview plan with evaluation rubric, a human review gate, and a scored hire/no-hire recommendation — all driven by cooperating AI agents.

The project is structured as three progressive demos, each building on the previous one to introduce more sophisticated AI orchestration patterns.

---

## 🏗️ Overall Architecture

```
  ┌─────────────────────────────────────────────────────────────────────┐
  │                        Interview Assistant                          │
  │                                                                     │
  │  Input: resume text + target role                                   │
  │                                                                     │
  │   ┌──────────────┐    ┌───────────────────┐    ┌────────────────┐  │
  │   │  ResumeAgent │───▶│ SeniorityAgent    │───▶│ PlannerAgent  │  │
  │   │  (ingest)    │    │ (classify)        │    │ (plan rounds) │  │
  │   └──────────────┘    └───────────────────┘    └───────┬────────┘  │
  │                                                         │           │
  │                                               ┌─────────▼────────┐ │
  │                                               │  Human Review    │ │
  │                                               │  (HITL gate)     │ │
  │                                               └─────────┬────────┘ │
  │                                                         │           │
  │                                               ┌─────────▼────────┐ │
  │                                               │ EvaluatorAgent   │ │
  │                                               │ (score + hire?)  │ │
  │                                               └──────────────────┘ │
  │                                                                     │
  │  Output: EvaluationResult (score, recommendation, risks, follow-ups)│
  └─────────────────────────────────────────────────────────────────────┘
```

---

## ⚙️ Common Prerequisites & Configuration

All three demos share the same runtime requirements and environment setup.

**Requirements**

| Requirement | Version |
|---|---|
| .NET SDK | 9.0 or later |
| Azure OpenAI resource | Deployed chat model (e.g., `gpt-4o`) |
| Azure CLI *(optional)* | Required only if using `az login` instead of an API key |

**Environment**

Create a `.env` file at the project root:

```env
AZURE_OPENAI_ENDPOINT=https://<your-resource>.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT=<your-deployment-name>
AZURE_OPENAI_API_KEY=<your-api-key>   # omit to use Azure CLI credentials
```

---

## 🗂️ Project Layout

```
InterviewAssistant/
├── Program.cs                           # Entry point — argument parsing, mode dispatch
├── Agents/
│   ├── AgentFactory.cs                  # Centralized AIAgent creation
│   ├── AgentPrompts.cs                  # All system prompt constants
│   └── JsonAgentRunner.cs               # Utility: run an agent and deserialize JSON
├── Models/
│   ├── ResumeProfile.cs
│   ├── SeniorityAssessment.cs
│   ├── InterviewPlan.cs
│   └── EvaluationResult.cs
├── Pipelines/
│   └── SimplePipelineRunner.cs          # Sequential step runner
├── Workflows/
│   ├── InterviewWorkflowRunner.cs       # MAF WorkflowBuilder graph runner
│   └── HierarchicalOrchestratorRunner.cs# Tool-calling orchestrator runner
└── assets/resumes/jane_doe.txt          # Default sample resume
```

---

## 🟢 Demo 1 — Single Agent

> 📄 Full details: [README_DEMO1.md](README_DEMO1.md)

### What it does

Demonstrates the foundational **single-agent pattern**: one LLM call, one structured output. A single `ResumeIngestion` agent reads a plain-text resume and returns a typed `ResumeProfile` JSON object covering name, years of experience, skills, target roles, notable projects, and red flags.

### Architecture

```
  resume.txt
      │
      ▼
  ┌─────────────────────┐
  │  ResumeIngestion    │   (single AIAgent with system prompt)
  │  Agent              │
  └──────────┬──────────┘
             │
             ▼
       ResumeProfile
       (JSON / POCO)
             │
             ▼
       Console output
```

### How to run

```bash
dotnet run                          # uses jane_doe.txt
dotnet run -- path/to/resume.txt    # custom resume
```

### Key concepts introduced

- Connecting to Azure OpenAI with `AzureOpenAIClient`
- Creating a MAF `AIAgent` with a system prompt
- Running an agent and deserializing its JSON output into a strongly-typed model

---

## 🟡 Demo 2 — Multi-Agent Pipeline

> 📄 Full details: [README_DEMO2.md](README_DEMO2.md)

### What changed from Demo 1

Demo 2 expands the single agent into a **four-step, multi-agent pipeline**. Agent creation is centralized behind a factory, three new specialist agents are introduced, a human-in-the-loop approval gate is added, and two execution strategies are offered: a simple sequential runner and a MAF `WorkflowBuilder` directed graph.

### Architecture

```
  resume.txt  +  role
       │
       ▼
  ┌────────────────┐
  │ ResumeIngest   │ ──▶ ResumeProfile JSON
  └────────────────┘
       │
       ▼
  ┌────────────────────┐
  │ SeniorityClassifier│ ──▶ SeniorityAssessment JSON
  └────────────────────┘     (level + confidence + rationale)
       │
       ▼
  ┌────────────────┐
  │ InterviewPlanner│ ──▶ InterviewPlan JSON
  └────────────────┘     (rounds + rubric)
       │
       ▼
  ┌──────────────────────┐
  │  Human-in-the-Loop   │ ◀── console prompt (approve / revise)
  │  Approval Gate       │
  └──────────────────────┘
       │ approved
       ▼
  ┌────────────────┐
  │  Evaluator     │ ──▶ EvaluationResult JSON
  └────────────────┘     (score + recommendation + strengths + risks)
       │
       ▼
  Console output

  ─────────────────────────────────────────────────────
  Execution strategies (steps 1–3):
  ┌──────────────────────┐   ┌─────────────────────────┐
  │  SimplePipelineRunner│   │  InterviewWorkflowRunner │
  │  (sequential calls)  │   │  (MAF WorkflowBuilder    │
  │                      │   │   directed graph)         │
  └──────────────────────┘   └─────────────────────────┘
```

### How to run

```bash
dotnet run                                                # simple mode, defaults
dotnet run -- --mode simple --role "Staff Engineer"
dotnet run -- --mode workflow --role "Senior Software Engineer"
```

| Argument | Default | Values |
|---|---|---|
| `--mode` | `simple` | `simple`, `workflow` |
| `--role` | `Senior Software Engineer` | any role string |
| `--resume` | `assets/resumes/jane_doe.txt` | path to .txt file |

### Key concepts introduced

- `AgentFactory` centralizing `AIAgent` creation
- Passing typed JSON output from one agent as input to the next
- MAF `WorkflowBuilder` wiring agents into a directed graph with streaming events
- Human-in-the-loop checkpoint with iterative plan revision

---

## 🔴 Demo 3 — Hierarchical Orchestration

> 📄 Full details: [README_DEMO3.md](README_DEMO3.md)

### What changed from Demo 2

Demo 3 replaces the hand-coded step sequencer with a **single LLM-driven `OrchestratorAgent`** that autonomously decides when to invoke each specialist. The specialist agents and the human review gate are each wrapped as `AIFunction` tools; MAF's built-in tool-call loop resolves all intermediate LLM ↔ tool round-trips transparently from a single top-level `await`.

### Architecture

```
  resume.txt  +  role  +  interview notes (collected upfront)
       │
       ▼
  ┌─────────────────────────────────────────────────────┐
  │                  OrchestratorAgent                  │
  │            (LLM-driven, tool-calling loop)          │
  │                                                     │
  │   tool calls dispatched autonomously by the LLM:   │
  │                                                     │
  │   ingest_resume ──────▶ ResumeIngestion Agent       │
  │   classify_seniority ─▶ SeniorityClassifier Agent  │
  │   plan_interview ─────▶ InterviewPlanner Agent      │
  │   human_review ───────▶ C# delegate (console HITL) │
  │        └── if REJECTED: loop back ◀────────────────┘│
  │   evaluate_candidate ─▶ Evaluator Agent             │
  └─────────────────────────────────────────────────────┘
       │
       ▼
  EvaluationResult  ──▶  Console output
```

### How to run

```bash
dotnet run -- --mode hierarchical                           # defaults
dotnet run -- --mode hierarchical --role "Staff Engineer" --resume assets/resumes/jane_doe.txt
```

All Demo 2 modes still work unchanged:

```bash
dotnet run -- --mode simple
dotnet run -- --mode workflow
```

| Argument | Default | Values |
|---|---|---|
| `--mode` | `simple` | `simple`, `workflow`, `hierarchical` |
| `--role` | `Senior Software Engineer` | any role string |
| `--resume` | `assets/resumes/jane_doe.txt` | path to .txt file |

> **Note:** In `hierarchical` mode, interview notes are collected upfront before any agent runs, so the orchestrator has full context from the start.

### Key concepts introduced

- Wrapping an `AIAgent` as an `AIFunction` tool via `.AsAIFunction()`
- Wrapping a C# delegate as an `AIFunction` tool via `AIFunctionFactory.Create`
- Using `[Description]` attributes to guide the LLM on parameter values
- LLM-driven conditional retry loop (plan → review → revise until approved)
- MAF auto-resolving a complete tool-call chain from a single `RunAsync` call

---

## 📊 Demo Progression at a Glance

| Capability | Demo 1 | Demo 2 | Demo 3 |
|---|:---:|:---:|:---:|
| Resume ingestion | ✅ | ✅ | ✅ |
| Seniority classification | — | ✅ | ✅ |
| Interview planning | — | ✅ | ✅ |
| Human-in-the-loop approval | — | ✅ | ✅ |
| Candidate evaluation | — | ✅ | ✅ |
| Centralized agent factory | — | ✅ | ✅ |
| MAF `WorkflowBuilder` graph | — | ✅ | ✅ |
| Tool-calling orchestration | — | — | ✅ |
| Specialist agents as `AIFunction` tools | — | — | ✅ |
| Single top-level `await` drives full pipeline | — | — | ✅ |

---

## 📦 NuGet Packages

| Package | Version |
|---|---|
| `Azure.AI.OpenAI` | 2.8.0-beta.1 |
| `Azure.Identity` | 1.19.0 |
| `DotNetEnv` | 3.1.1 |
| `Microsoft.Agents.AI.OpenAI` | 1.0.0-rc1 |
| `Microsoft.Agents.AI.Workflows` | 1.0.0-rc1 |

```bash
dotnet restore
dotnet build
```
