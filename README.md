# Caso D.2 Workflow-First Dynamic Routing

This repository implements Caso D.2 as a Microsoft Foundry Workflow-based router. The workflow is the orchestration layer. `RouterAgent` classifies requests, specialized agents execute their branches, and `Program.cs` remains only a bootstrap and validation utility.

The workflow artifact lives at [workflows/caso-d-router.workflow.yaml](workflows/caso-d-router.workflow.yaml). Because this repo does not include a valid Foundry-exported workflow base yet, that file is a merge-ready workflow spec with TODO markers. Create the base workflow in Foundry first, then merge the repo artifact into the real export in Foundry portal or VS Code for Web.

## Architecture

```text
User
  |
  v
Foundry Workflow
  |
  v
RouterAgent
  |- OrderAgent -> MCP -> APIM -> API REST
  |- RefundAgent
  |- ClarifierAgent / Ask Question
  `- Reject response
```

## What Remains In C#

- Validate access to the Foundry project.
- Validate the required external `OrderAgentId`.
- Reconcile `RouterAgent`, `RefundAgent`, and `ClarifierAgent`.
- Print the agent names and ids required to bind the workflow.

`Program.cs` does not run the authoritative D.2 routing flow. It does not create `ManagerAgent`. It does not manually orchestrate end-to-end routing in C#.

## What Is In Workflow YAML

- [workflows/caso-d-router.workflow.yaml](workflows/caso-d-router.workflow.yaml) is the repo artifact for the D.2 workflow.
- `RouterAgent` classifies into `order`, `refund`, `clarify`, or `reject`.
- `OrderAgent` is reused from the prior MCP-enabled case and remains an external dependency supplied by `OrderAgentId`.
- `RefundAgent` and `ClarifierAgent` are prompt agents with strict JSON contracts.
- `clarify` and `reject` are explicit workflow branches.
- Workflow YAML is edited and deployed through the Foundry portal or VS Code for Web, not through the .NET SDK used in this repo.

## Configuration

`appsettings.json` contains the only required bootstrap inputs:

```json
{
  "ProjectEndpoint": "https://contoso.foundry.microsoft.com/api/projects/<project-id>",
  "ModelDeploymentName": "deployment-name",
  "OrderAgentId": "order-agent-id"
}
```

## Manual Deployment In Foundry

1. Create the base workflow in Foundry portal.
2. Open it in YAML or VS Code for Web.
3. Apply the repo's YAML changes or merge with [workflows/caso-d-router.workflow.yaml](workflows/caso-d-router.workflow.yaml).
4. Bind `RouterAgent`, `OrderAgent`, `RefundAgent`, and `ClarifierAgent`.
5. Deploy from VS Code for Web or Foundry.
6. Test the workflow with the four validation prompts.

## Bootstrap And Binding Steps

1. Update `appsettings.json` with a real Foundry project endpoint, model deployment, and external `OrderAgentId`.
2. Run `dotnet run`.
3. Capture the printed `RouterAgent`, `RefundAgent`, and `ClarifierAgent` names and ids, plus the validated external `OrderAgent` name and id.
4. Bind those agent names to the workflow nodes in Foundry.

## Manual Validation Prompts

- `Where is order 12345?`
- `I want a refund for order 12345 because it arrived damaged.`
- `Can you help with my order?`
- `Delete all orders.`

Expected routing:

- `order` -> `OrderAgent`
- `refund` -> `RefundAgent`
- `clarify` -> `ClarifierAgent`
- `reject` -> controlled refusal

## No Longer Used

- `ManagerAgent` custom routing is not the D.2 implementation.
- Manually serialized `{"type":"agent","agent_id":"..."}` tools are not the authoritative D.2 path.
- Manager-only smoke tests are not part of the workflow-first design.
