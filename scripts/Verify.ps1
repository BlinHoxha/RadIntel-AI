[CmdletBinding()]
param(
    [string]$FrameworkRoot = (Join-Path $PSScriptRoot '..\..\Framework'),
    [string]$AIFrameworkRoot = (Join-Path $PSScriptRoot '..\..\AI-Framework')
)
$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot '..\RadIntel-AI.sln'
$baseProperty = '-p:FrameworkRoot=' + [IO.Path]::GetFullPath($FrameworkRoot)
$aiProperty = '-p:AIFrameworkRoot=' + [IO.Path]::GetFullPath($AIFrameworkRoot)
& dotnet restore $solution $baseProperty $aiProperty
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
& dotnet build $solution --no-restore -c Release $baseProperty $aiProperty
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& dotnet test $solution --no-restore --no-build -c Release $baseProperty $aiProperty
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
# Tests use their own ephemeral in-memory SQLite schemas. No application migrations or seeds run.
