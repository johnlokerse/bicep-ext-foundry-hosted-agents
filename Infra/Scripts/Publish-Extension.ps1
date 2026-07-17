#!/usr/bin/env pwsh
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Target
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked {
  param(
    [Parameter(Mandatory = $true)]
    [scriptblock] $Command
  )

  & $Command
  if ($LASTEXITCODE -ne 0) {
    throw "Command failed with exit code $LASTEXITCODE."
  }
}

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

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $root 'src/FoundryExtension.csproj'
$extensionName = 'foundry-hosted-agents-extension'
$targetFramework = 'net10.0'
$bicep = Get-BicepCommand

foreach ($runtime in @('osx-arm64', 'osx-x64', 'linux-x64', 'win-x64')) {
  Invoke-Checked {
    dotnet publish $project `
      --configuration Release `
      --runtime $runtime `
      --nologo
  }
}

$publishRoot = Join-Path $root "src/bin/Release/$targetFramework"
Invoke-Checked {
  & $bicep publish-extension `
    --bin-osx-arm64 (Join-Path $publishRoot "osx-arm64/publish/$extensionName") `
    --bin-osx-x64 (Join-Path $publishRoot "osx-x64/publish/$extensionName") `
    --bin-linux-x64 (Join-Path $publishRoot "linux-x64/publish/$extensionName") `
    --bin-win-x64 (Join-Path $publishRoot "win-x64/publish/$extensionName.exe") `
    --target $Target `
    --force
}
