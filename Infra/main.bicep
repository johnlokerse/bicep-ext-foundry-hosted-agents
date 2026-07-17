targetScope = 'subscription'

@description('Resource group used for publishing the Bicep extension artifact.')
param resourceGroupName string

@description('Globally unique Azure Container Registry name.')
@minLength(5)
@maxLength(50)
param registryName string

@description('Azure region for the publishing registry.')
param location string = deployment().location

@description('Tags applied to publishing resources.')
param tags object

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module registry './registry.bicep' = {
  scope: resourceGroup
  params: {
    location: location
    registryName: registryName
    tags: tags
  }
}

output registryName string = registry.outputs.registryName
output loginServer string = registry.outputs.loginServer

