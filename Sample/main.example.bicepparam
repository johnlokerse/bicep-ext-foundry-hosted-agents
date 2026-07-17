using './main.bicep'

param parProjectEndpoint = 'https://<foundry account>.services.ai.azure.com/api/projects/<project name>'
param parAgentName = 'hello-world-agent'
param parImage = '<registry>.azurecr.io/foundry-hosted-agents/python-responses-base:1.1.0'
param parProtocolVersion = '2.0.0'
param parCpuCount = 1
param parMemoryAmountInGb = '2Gi'

param parEnvironmentVariables = {
  MODEL_DEPLOYMENT_NAME: 'gpt-5.6-luna'
  HELLO: 'World!'
}
