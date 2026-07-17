targetScope = 'local'

extension foundry with {
  projectEndpoint: parProjectEndpoint
}

@description('Microsoft Foundry project endpoint.')
param parProjectEndpoint string

@description('Hosted agent name.')
param parAgentName string

@description('Full ACR image reference with an immutable tag or digest.')
param parImage string

@description('Protocol version implemented by the image.')
param parProtocolVersion string

@description('Environment variables passed to the hosted agent.')
param parEnvironmentVariables object

@description('Optional full ARM resource ID of an existing RAI policy.')
param parRaiPolicyResourceId string?

@minValue(1)
@description('CPU count allocated to the running container.')
param parCpuCount int = 1

@description('Memory allocated to the running container. Memory must use a Mi or Gi suffix, for example 2Gi.')
param parMemoryAmountInGb string = '2Gi'

resource resHelloWorldAgent 'HostedAgent' = {
  name: parAgentName
  image: parImage
  cpu: string(parCpuCount)
  memory: parMemoryAmountInGb
  protocols: [
    {
      protocol: 'responses'
      version: parProtocolVersion
    }
  ]
  environmentVariables: parEnvironmentVariables
  raiPolicy: empty(parRaiPolicyResourceId)
    ? null
    : {
        resourceId: parRaiPolicyResourceId
      }
}

output agentVersion string = resHelloWorldAgent.version
output agentStatus string = resHelloWorldAgent.status
output deploymentAction string = resHelloWorldAgent.deploymentAction
output responseEndpoint string = resHelloWorldAgent.endpoints.responses
output agentPrincipalId string? = resHelloWorldAgent.principalId
