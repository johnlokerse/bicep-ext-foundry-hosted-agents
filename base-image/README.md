# Foundry Hosted Agents Python base image

This image runs a model-backed, general-purpose assistant with Python 3.12, the
Azure AI Projects/OpenAI SDK, and the Microsoft Foundry Responses protocol
server. It listens on port `8088`, exposes `/readiness`, and runs as a non-root
user.

The agent authenticates with `DefaultAzureCredential`. In Microsoft Foundry
Hosted Agents, that credential resolves the agent's managed identity. No API key
or embedded secret is required.

## Configuration

The container requires:

| Variable | Source |
| --- | --- |
| `FOUNDRY_PROJECT_ENDPOINT` | Injected automatically by Microsoft Foundry |
| `MODEL_DEPLOYMENT_NAME` | Set in the hosted-agent environment-variable map; for example, `gpt-5.6-luna` |

The model deployment must exist in the same Foundry project, and the hosted-agent
identity must have permission to call it.

Use the published image as a base and replace `/app/main.py`:

```dockerfile
FROM hostedagentsimages.azurecr.io/foundry-hosted-agents/python-responses-base:1.1.0

COPY --chown=app:app main.py /app/main.py
```

Declare the image with the Responses protocol when creating the hosted agent:

```bicep
image: 'hostedagentsimages.azurecr.io/foundry-hosted-agents/python-responses-base:1.1.0'
protocols: [
  {
    protocol: 'responses'
    version: '2.0.0'
  }
]
environmentVariables: {
  MODEL_DEPLOYMENT_NAME: 'gpt-5.6-luna'
}
```

For reproducible deployments, use the repository digest reported by ACR instead
of relying on a mutable tag.

The implementation follows the current
[`azure-ai-projects` 2.3.0 client guidance](https://learn.microsoft.com/python/api/overview/azure/ai-projects-readme?view=azure-python):
it creates an asynchronous `AIProjectClient`, gets its authenticated OpenAI
client, and calls `responses.create`.

## Tests

The focused tests mock all model responses and never call Azure:

```bash
python -m unittest discover -s tests -v
```
