<#
.SYNOPSIS
Expands a VSIX file into <repo>\out\vsix\<VSIX name> for inspection.

.PARAMETER VsixPath
Path to the VSIX file to be expanded, e.g. out\binaries\TaefTestAdapter\Release\Packaging\TaefTestAdapter.vsix
(expanded into out\vsix\TaefTestAdapter).
#>

#requires -Version 3.0
Param(
    [Parameter(Mandatory=$True)][String]$VsixPath
)
Set-StrictMode -Version Latest
$WarningPreference = "Stop"
$ErrorActionPreference = "Stop"

$VsixPath = (Resolve-Path $VsixPath).ProviderPath
$VsixName = [IO.Path]::GetFileNameWithoutExtension($VsixPath)
$VsixRoot = Join-Path $PSScriptRoot "..\out\vsix"
$OutPath = Join-Path $VsixRoot $VsixName
$VsixZipPath = Join-Path $VsixRoot "$VsixName.zip"

& "$PSScriptRoot\New-CleanDirectory" $OutPath | Out-Null
Copy-Item $VsixPath $VsixZipPath
Expand-Archive -Path $VsixZipPath -DestinationPath $OutPath
