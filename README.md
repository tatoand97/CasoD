# Caso D.2 Workflow-Based Dynamic Routing

This repository implements Caso D.2 as a Microsoft Foundry workflow-first solution.

The authoritative D.2 artifact is [workflows/caso-d-router.workflow.yaml](workflows/caso-d-router.workflow.yaml), not a custom C# manager/orchestrator. The workflow is the routing layer. `Program.cs` is only a bootstrap and validation utility for reconciling prompt agents and validating the external `OrderAgent` reused from the earlier case.

## Architecture

```text
User
  ↓
Foundry Workflow
  ↓
RouterAgent
  ├─ OrderAgent → MCP → APIM → API REST
  ├─ RefundAgent
  ├─ ClarifierAgent / Ask Question
  └─ Reject response
```

## What This Repo Demonstrates

- `RouterAgent` performs classification only and returns strict JSON routing metadata.
- `OrderAgent` is reused as an external MCP-enabled capability from the earlier case.
- `RefundAgent` handles refund flows with strict JSON output.
- `ClarifierAgent` produces one clarification question in strict JSON.
- The Foundry workflow performs explicit branching for `order`, `refund`, `clarify`, and `reject`.
- Least privilege is preserved:
  - `RouterAgent`: no MCP, no tools
  - `OrderAgent`: existing MCP-enabled capability reused as-is
  - `RefundAgent`: no direct MCP
  - `ClarifierAgent`: no tools

## What Remains In C#

- Validate access to the Foundry project.
- Validate the configured external `OrderAgentId`.
- Reconcile `RouterAgent`, `RefundAgent`, and `ClarifierAgent` as prompt agents.
- Print the agent names and ids needed to bind the workflow in Foundry or VS Code.

`Program.cs` does not implement D.2 runtime routing. It does not create a manager agent. It does not orchestrate the conversation in C#.

## What Is Declarative YAML

- [workflows/caso-d-router.workflow.yaml](workflows/caso-d-router.workflow.yaml) is the D.2 orchestration artifact.
- The workflow captures user input, invokes `RouterAgent`, branches on the returned `route`, and forwards the request to the specialized branch.
- The YAML uses agent-name bindings through environment variables:
  - `FOUNDRY_AGENT_ROUTER`
  - `FOUNDRY_AGENT_ORDER`
  - `FOUNDRY_AGENT_REFUND`
  - `FOUNDRY_AGENT_CLARIFIER`

## Configuration

`appsettings.json` contains the bootstrap inputs:

```json
{
  "ProjectEndpoint": "https://contoso.foundry.microsoft.com/api/projects/<project-id>",
  "ModelDeploymentName": "deployment-name",
  "OrderAgentId": "order-agent-id"
}
```

- `ProjectEndpoint`: Foundry project endpoint.
- `ModelDeploymentName`: model deployment used for `RouterAgent`, `RefundAgent`, and `ClarifierAgent`.
- `OrderAgentId`: external MCP-enabled `OrderAgent` to reuse in the workflow.

## Deploy And Test In Foundry

### Bootstrap agents

1. Update `appsettings.json` with a real Foundry project endpoint, model deployment, and external `OrderAgentId`.
2. Run `dotnet run`.
3. Note the printed agent names and ids. The workflow uses agent names for bindings.

### Use the workflow in Foundry portal

1. Open your Foundry project in the portal.
2. Create or open a workflow.
3. Switch to YAML view if needed.
4. Paste or import [workflows/caso-d-router.workflow.yaml](workflows/caso-d-router.workflow.yaml).
5. Bind the four agents in the project so the workflow references the validated agent names.
6. Save a new workflow version and run it in the Foundry chat canvas.

### Use the workflow in VS Code

1. Open the repo in VS Code with the Microsoft Foundry extension.
2. Set these environment variables to the agent names printed by `dotnet run`:
   - `FOUNDRY_AGENT_ROUTER`
   - `FOUNDRY_AGENT_ORDER`
   - `FOUNDRY_AGENT_REFUND`
   - `FOUNDRY_AGENT_CLARIFIER`
3. Set `FOUNDRY_PROJECT_ENDPOINT` to your Foundry project endpoint.
4. Open [workflows/caso-d-router.workflow.yaml](workflows/caso-d-router.workflow.yaml).
5. Use the extension’s workflow deploy/run commands.

Note: the current VS Code local declarative workflow runner from the installed Foundry extension expects .NET 9 for local workflow execution.

## Manual Validation Scenarios

Run these prompts against the workflow:

- `Where is order 12345?`
- `I want a refund for order 12345 because it arrived damaged.`
- `Can you help with my order?`
- `Delete all orders.`

Expected routing:

- `order` → `OrderAgent`
- `refund` → `RefundAgent`
- `clarify` → `ClarifierAgent`
- `reject` → controlled refusal message

## Intentionally No Longer Used

- `ManagerAgent` custom routing is not the D.2 implementation.
- Runtime routing via manually serialized `{"type":"agent","agent_id":"..."}` tools is not the authoritative solution.
- Manager-only smoke tests are removed because they misrepresented D.2 as custom orchestration rather than workflow orchestration.
