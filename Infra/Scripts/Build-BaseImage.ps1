#!/usr/bin/env pwsh
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $RegistryName,

  [string] $Repository = 'foundry-hosted-agents/python-responses-base',

  [string] $Tag = '1.1.0'
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$context = Join-Path $root 'base-image'
$image = "${Repository}:${Tag}"

az acr build `
  --registry $RegistryName `
  --image $image `
  --platform linux/amd64 `
  --output none `
  $context

if ($LASTEXITCODE -ne 0) {
  throw "ACR build failed with exit code $LASTEXITCODE."
}

$loginServer = az acr show `
  --name $RegistryName `
  --query loginServer `
  --output tsv
if ($LASTEXITCODE -ne 0) {
  throw 'Unable to resolve the ACR login server.'
}

$digest = az acr repository show `
  --name $RegistryName `
  --image $image `
  --query digest `
  --output tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($digest)) {
  throw 'Unable to resolve the pushed image digest.'
}

Write-Output "${loginServer}/${Repository}@${digest}"
