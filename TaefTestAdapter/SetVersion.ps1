<#
.SYNOPSIS
Sets the version of the Test Adapter for TAEF assemblies, the VSIX manifest, the NuGet package and the
templates' reference to the wizard assembly (TaefTestAdapter.VsPackage).

.PARAMETER version
The version to set, <major>.<minor>.<revision>[.<build>], e.g. 1.0.0.42.

.DESCRIPTION
Updates
  - AssemblyVersion/AssemblyFileVersion in the AssemblyInfo.cs files of all C# projects of the
    adapter (<project>\Properties\AssemblyInfo.cs in this folder, including tests and helper tools),
  - the Identity Version of Packaging\source.extension.vsixmanifest,
  - the <version> of the NuGet package (Packaging\VsPackage.nuspec),
  - the Version of the wizard assembly (TaefTestAdapter.VsPackage) in the <WizardExtension> of the
    project and item templates (ProjectTemplates\Test\TAEF\TaefTest.vstemplate and
    ItemTemplates\Test\TAEF\TaefTest.vstemplate): Visual Studio loads the wizard by the full name of
    its assembly, which contains the AssemblyVersion with four parts (e.g. 1.0.0.0 for version 1.0.0).
File encodings (including a UTF-8 BOM) and line endings are preserved. The script can be run
repeatedly. Before a release, also add a section for the new version to CHANGELOG.md (shipped in the
VSIX as ReleaseNotes.txt).
#>
Param(
    [parameter(Mandatory=$true)]
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string] $version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# <project>\Properties\AssemblyInfo.cs of every C# project in this folder (adapter, tests and helper tools)
$assembly_infos = @(Get-ChildItem -LiteralPath $PSScriptRoot -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "Properties\AssemblyInfo.cs") -PathType Leaf } |
    ForEach-Object { Join-Path $_.Name "Properties\AssemblyInfo.cs" } |
    Sort-Object)
if ($assembly_infos.Count -eq 0) {
    throw "No AssemblyInfo.cs files found in the project folders of $PSScriptRoot"
}
$vsix_manifest = "Packaging\source.extension.vsixmanifest"
$nuspec = "Packaging\VsPackage.nuspec"
$templates = @("ProjectTemplates\Test\TAEF\TaefTest.vstemplate", "ItemTemplates\Test\TAEF\TaefTest.vstemplate")

# the version in an assembly's full name always has four parts (AssemblyVersion("1.0.0") is 1.0.0.0)
$assembly_version = if ($version -match '^\d+\.\d+\.\d+$') { "$version.0" } else { $version }

function Update-File([string] $relativePath, [string] $pattern, [string] $newVersion = $version) {
    $path = Join-Path $PSScriptRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Write-Warning "File not found, not versioned: $path"
        return
    }

    $bytes = [IO.File]::ReadAllBytes($path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $content = [IO.File]::ReadAllText($path)

    $regex = New-Object System.Text.RegularExpressions.Regex($pattern)
    if (-not $regex.IsMatch($content)) {
        Write-Warning "No version found in $path"
        return
    }

    $newContent = $regex.Replace($content, { param($m) $m.Groups['prefix'].Value + $newVersion + $m.Groups['suffix'].Value })
    if ($newContent -ne $content) {
        [IO.File]::WriteAllText($path, $newContent, (New-Object System.Text.UTF8Encoding($hasBom)))
        Write-Output "Set version $newVersion in $relativePath"
    }
    else {
        Write-Output "Version $newVersion already set in $relativePath"
    }
}

foreach ($assembly_info in $assembly_infos) {
    Update-File $assembly_info '(?m)^(?<prefix>[ \t]*\[assembly:\s*Assembly(?:File)?Version\(")[^"]*(?<suffix>"\)\])'
}

Update-File $vsix_manifest '(?<prefix><Identity\b[^>]*?\bVersion=")[^"]*(?<suffix>")'

Update-File $nuspec '(?<prefix><metadata\b[^>]*>(?:(?!</metadata>)[\s\S])*?<version>)[^<]*(?<suffix></version>)'

foreach ($template in $templates) {
    Update-File $template '(?<prefix><WizardExtension>\s*<Assembly>\s*TaefTestAdapter\.VsPackage,\s*Version=)[^,<]*(?<suffix>,)' $assembly_version
}
