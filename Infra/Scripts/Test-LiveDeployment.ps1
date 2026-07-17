#!/usr/bin/env pwsh
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $ProjectEndpoint,

  [Parameter(Mandatory = $true)]
  [string] $AgentName,

  [Parameter(Mandatory = $true)]
  [string] $Image,

  [Parameter(Mandatory = $true)]
  [string] $ModelDeploymentName,

  [string] $RaiPolicyResourceId = '',

  [bool] $Cleanup = $true
)

$ErrorActionPreference = 'Stop'

function Get-BicepCommand {
  $command = Get-Command bicep -ErrorAction SilentlyContinue
  if ($null -ne $command) {
    return $command.Source
  }

  $azureCliBicep = Join-Path $HOME '.azure/bin/bicep'
  if (Test-Path $azureCliBicep) {
    return $azureCliBicep
  }

  throw 'Bicep CLI was not found. Install Bicep 0.45.15 or later.'
}

function ConvertTo-BicepString {
  param([string] $Value)
  return $Value.Replace("'", "''")
}

function Invoke-LocalDeployment {
  param(
    [string] $Bicep,
    [string] $ParametersFile,
    [string] $Revision
  )

  $content = @"
using './main.bicep'

param parProjectEndpoint = '$(ConvertTo-BicepString $ProjectEndpoint)'
param parAgentName = '$(ConvertTo-BicepString $AgentName)'
param parImage = '$(ConvertTo-BicepString $Image)'
param parProtocolVersion = '2.0.0'
param parEnvironmentVariables = {
  MODEL_DEPLOYMENT_NAME: '$(ConvertTo-BicepString $ModelDeploymentName)'
  SMOKE_REVISION: '$(ConvertTo-BicepString $Revision)'
}
param parRaiPolicyResourceId = '$(ConvertTo-BicepString $RaiPolicyResourceId)'
"@
  Set-Content -Path $ParametersFile -Value $content -Encoding utf8NoBOM

  $raw = (& $Bicep local-deploy $ParametersFile --format Json | Out-String).Trim()
  if ($LASTEXITCODE -ne 0) {
    throw "Bicep local deployment failed with exit code $LASTEXITCODE."
  }

  Set-Content -Path (Join-Path (Split-Path $ParametersFile) 'smoke-output.json') `
    -Value $raw `
    -Encoding utf8NoBOM
  return $raw | ConvertFrom-Json
}

function Get-OutputValue {
  param(
    [object] $Deployment,
    [string] $Name
  )

  $output = $Deployment.outputs.$Name
  if ($null -ne $output.value) {
    return $output.value
  }

  return $output
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sample = Join-Path $root 'Sample'
$parametersFile = Join-Path $sample 'main.bicepparam'
$extensionTarget = Join-Path $root 'bin/foundry-extension'
$publishScript = Join-Path $PSScriptRoot 'Publish-Extension.ps1'
$bicep = Get-BicepCommand
$baseUrl = $ProjectEndpoint.TrimEnd('/')
$escapedAgentName = [Uri]::EscapeDataString($AgentName)

& $publishScript -Target $extensionTarget

try {
  $first = Invoke-LocalDeployment $bicep $parametersFile '1'
  $firstStatus = Get-OutputValue $first 'agentStatus'
  if ($firstStatus -ne 'active') {
    throw "First deployment returned status '$firstStatus' instead of 'active'."
  }

  $firstVersion = [string](Get-OutputValue $first 'agentVersion')
  $second = Invoke-LocalDeployment $bicep $parametersFile '1'
  $secondVersion = [string](Get-OutputValue $second 'agentVersion')
  if ($secondVersion -ne $firstVersion) {
    throw "Idempotency check failed: identical deployment created version '$secondVersion' after '$firstVersion'."
  }

  $updated = Invoke-LocalDeployment $bicep $parametersFile '2'
  $updatedVersion = [string](Get-OutputValue $updated 'agentVersion')
  if ($updatedVersion -eq $firstVersion) {
    throw 'Update check failed: changing SMOKE_REVISION did not create a new version.'
  }

  $token = az account get-access-token `
    --resource https://ai.azure.com `
    --query accessToken `
    --output tsv
  if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
    throw 'Unable to acquire a Microsoft Foundry access token.'
  }

  $headers = @{
    Authorization = "Bearer $token"
  }
  $response = Invoke-RestMethod `
    -Method Post `
    -Uri "$baseUrl/agents/$escapedAgentName/endpoint/protocols/openai/responses?api-version=v1" `
    -Headers $headers `
    -ContentType 'application/json' `
    -Body (@{
      input = 'smoke test'
      store = $true
    } | ConvertTo-Json)

  if ($null -eq $response.output -or $response.output.Count -eq 0) {
    throw 'The hosted agent response did not contain model output.'
  }

  if (-not [string]::IsNullOrWhiteSpace($RaiPolicyResourceId)) {
    $version = Invoke-RestMethod `
      -Method Get `
      -Uri "$baseUrl/agents/$escapedAgentName/versions/$updatedVersion`?api-version=v1" `
      -Headers $headers
    if ($version.definition.rai_config.rai_policy_name -ne $RaiPolicyResourceId) {
      throw 'The deployed version did not return the requested RAI policy resource ID.'
    }
  }

  Write-Host "Smoke test succeeded for agent '$AgentName' version '$updatedVersion'."
}
finally {
  if ($Cleanup) {
    if ([string]::IsNullOrWhiteSpace($token)) {
      $token = az account get-access-token `
        --resource https://ai.azure.com `
        --query accessToken `
        --output tsv
    }

    if (-not [string]::IsNullOrWhiteSpace($token)) {
      $deleteResponse = Invoke-WebRequest `
        -Method Delete `
        -Uri "$baseUrl/agents/$escapedAgentName`?api-version=v1" `
        -Headers @{ Authorization = "Bearer $token" } `
        -SkipHttpErrorCheck
      if ($deleteResponse.StatusCode -notin @(200, 202, 204, 404)) {
        throw "Agent cleanup failed with HTTP $($deleteResponse.StatusCode)."
      }
    }
  }
}
