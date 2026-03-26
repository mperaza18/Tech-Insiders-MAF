# Interview Assistant — MAF + Azure OpenAI Demo

A C# (.NET 9) console application that demonstrates how to use the **Microsoft Agent Framework (MAF)** with **Azure OpenAI** to build an AI-powered interview preparation tool. The first demo shows a single-agent pipeline that reads a candidate's plain-text resume and extracts a structured `ResumeProfile` as JSON — covering name, experience, skills, roles, notable projects, and red flags.

---

## 0. Prerequisites

| Requirement | Version / Notes |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 9.0 or later |
| Azure OpenAI resource | A deployed chat model (e.g., `gpt-4o`) |
| Azure CLI *(optional)* | Required only if using `AzureCliCredential` instead of an API key |

---

## 1. Setup — Packages

From ```src/InterviewAssistant```:

```bash
# Core agent packages (from the Learn quick-start)
dotnet add package Azure.AI.OpenAI --version 2.8.0-beta.1
dotnet add package Azure.Identity --version 1.17.1
dotnet add package Microsoft.Agents.AI.OpenAI --version 1.0.0-preview.260212.1

# Workflows (to demonstrate orchestration graphs)
dotnet add package Microsoft.Agents.AI.Workflows --version 1.0.0-preview.260212.1

# Optional (nice-to-have)
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.EnvironmentVariables
```

NuGet packages used:

| Package | Version |
|---|---|
| `Azure.AI.OpenAI` | 2.8.0-beta.1 |
| `Azure.Identity` | 1.19.0 |
| `DotNetEnv` | 3.1.1 |
| `Microsoft.Agents.AI.OpenAI` | 1.0.0-rc1 |
| `Microsoft.Agents.AI.Workflows` | 1.0.0-rc1 |

---

## 2. Configure Environment

Create a `.env` file in the project root:

```env
AZURE_OPENAI_ENDPOINT=https://<your-resource>.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT=<your-deployment-name>

# Optional — omit to use Azure CLI credentials (az login) instead
AZURE_OPENAI_API_KEY=<your-api-key>
```

- If `AZURE_OPENAI_API_KEY` is **set**, the app authenticates with `AzureKeyCredential`.
- If it is **omitted**, the app falls back to `AzureCliCredential` (requires `az login`).

---

## 3. Run

```bash
# Uses the built-in sample resume (assets/resumes/jane_doe.txt)
dotnet run

# Pass your own resume file
dotnet run -- path/to/resume.txt
```

Expected output:

```
=== Demo 1: Single Agent ===

Candidate : Jane Doe
Experience: 6 years
Skills    : C#, .NET, ASP.NET Core, Azure, SQL, Cosmos DB, Service Bus, Redis
Red Flags :
```

---

## 4. What This Demo Shows

| Concept | Where |
|---|---|
| Connecting to Azure OpenAI from .NET | `Program.cs` — `AzureOpenAIClient` setup |
| Creating a MAF `AIAgent` with a system prompt | `Program.cs` — `.AsAIAgent(instructions: ...)` |
| Defining structured prompt templates | `Agents/AgentPrompts.cs` |
| Running an agent and parsing its JSON output | `Agents/JsonAgentRunner.cs` — `RunJsonAsync<T>` |
| Strongly-typed model bound to the agent's schema | `Models/ResumeProfile.cs` |

The demo illustrates the **single-agent pattern**: one prompt in → one structured JSON object out. This is the foundation for building more complex multi-agent workflows using `Microsoft.Agents.AI.Workflows`.

---

## Repo Layout

```
InterviewAssistant/
├── Program.cs                  # Entry point — wires client, agent, and output
├── Agents/
│   ├── AgentPrompts.cs         # All system prompt constants
│   └── JsonAgentRunner.cs      # Utility: run an agent and deserialize JSON response
├── Models/
│   └── ResumeProfile.cs        # POCO representing the extracted resume data
├── assets/
│   └── resumes/
│       └── jane_doe.txt        # Sample resume used as default input
├── InterviewAssistant.csproj
└── InterviewAssistant.sln
```
