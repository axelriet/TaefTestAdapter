<#
.SYNOPSIS
Builds the Test Adapter for TAEF (and its sample test DLLs) from source.

.DESCRIPTION
Performs the complete local build of the Test Adapter for TAEF:

  1. Locates Visual Studio 2026 (18.x) or 2022 (17.x) with vswhere.
  2. Prepares the DIA SDK files needed by the DiaResolver project:
     - copies msdia140.dll from '<VS>\DIA SDK\bin', '<VS>\DIA SDK\bin\amd64' and '<VS>\DIA SDK\bin\arm64' into
       TaefTestAdapter\DiaResolver\x86, TaefTestAdapter\DiaResolver\x64 and TaefTestAdapter\DiaResolver\arm64
       (if the DIA SDK has no arm64 DLL, a warning is shown and the adapter cannot read source locations in
       native ARM64 processes),
     - generates the interop assembly TaefTestAdapter\DiaResolver\dia2\dia2.dll from
       '<VS>\DIA SDK\idl\dia2.idl' (midl + tlbimp inside a VS developer environment).
  3. Restores NuGet packages (NuGet.exe restore for packages.config projects, then
     'msbuild /t:Restore' for PackageReference projects).
  4. Builds the sample test DLLs of SampleTests\SampleTests.sln (-SampleConfiguration x
     -SamplePlatform, unless -SkipSamples) and TaefTestAdapter\TaefTestAdapter.sln (-Configuration,
     'Any CPU'). The adapter solution is built with /p:SkipSampleTestsBuild=true: its project
     SampleTestsBuilder would otherwise build all sample configurations (again), which would also
     require TAEF with -SkipSamples.
  5. Optionally (-Test) runs the adapter's unit tests with vstest.console.exe.

Building the sample test DLLs requires the TAEF development files of the Windows Kits
(installed with the WDK: Testing\Development\inc\WexTestClass.h). Many of the adapter's tests run
against the sample test DLLs of all four configurations (Debug/Release x Win32/x64).

By using this script you accept the license terms of the Microsoft DIA SDK (part of your
Visual Studio installation, see https://visualstudio.microsoft.com/license-terms/), since
msdia140.dll is copied into the source tree and redistributed with the adapter.

.PARAMETER Configuration
Build configurations of the adapter (Debug, Release), also used to select the test assemblies
run with -Test. Default: both.

.PARAMETER SampleConfiguration
Configurations of the sample test DLLs (Debug, Release). Default: both, since the adapter's
tests (-Test) use the sample test DLLs of all four configurations.

.PARAMETER SamplePlatform
Platforms of the sample test DLLs (Win32, x64). Default: both, since the adapter's tests (-Test)
use the sample test DLLs of all four configurations.

.PARAMETER VsPath
Installation folder of the Visual Studio instance to use. Default: newest VS 2026/2022
instance found by vswhere that contains MSBuild and the DIA SDK.

.PARAMETER OutRoot
Root folder of all build outputs (passed as /p:OutRoot=...). Default: <repo>\out\.

.PARAMETER PrepareOnly
Only prepare the build (DIA SDK files and NuGet restore); do not build or test.

.PARAMETER SkipPrepare
Skip the preparation steps (DIA SDK files and NuGet restore).

.PARAMETER SkipBuild
Skip building the solutions (e.g. to only run tests of an existing build).

.PARAMETER SkipSamples
Do not build the sample test DLLs (SampleTests\SampleTests.sln), e.g. if TAEF is not installed:
the adapter is then built without TAEF. The adapter's tests (-Test) use sample test DLLs of an
earlier build (or of the folder given by environment variable TAEF_ADAPTER_SAMPLES_DIR); tests
which need sample test DLLs fail if there are none.

.PARAMETER SkipAdapter
Do not build TaefTestAdapter\TaefTestAdapter.sln.

.PARAMETER ForceDia
Regenerate dia2.dll even if it already exists.

.PARAMETER DiaTargetDir
Folder receiving x86\msdia140.dll, x64\msdia140.dll, arm64\msdia140.dll and dia2\dia2.dll.
Default: <repo>\TaefTestAdapter\DiaResolver.

.PARAMETER Test
Run the adapter's tests with vstest.console.exe after building.

.PARAMETER TestCaseFilter
vstest.console /TestCaseFilter expression used with -Test. Default: 'TestCategory=Unit'.
Pass an empty string to run all tests of the selected test projects.

.PARAMETER TestProjects
Test projects (folder names below TaefTestAdapter) whose assemblies are passed to
vstest.console. Default: DiaResolver.Tests, Core.Tests, TestAdapter.Tests, VsPackage.Tests.Unit.

.PARAMETER Verbosity
MSBuild verbosity (quiet, minimal, normal, detailed, diagnostic). Default: minimal.

.PARAMETER MaxCpuCount
Maximum number of parallel MSBuild nodes (0 = number of processors). Node reuse is always
disabled (/nr:false). Default: 0.

.EXAMPLE
.\build.ps1
Prepares, restores and builds everything (Debug and Release).

.EXAMPLE
.\build.ps1 -PrepareOnly
Only copies/generates the DIA SDK files and restores NuGet packages.

.EXAMPLE
.\build.ps1 -Configuration Release -Test
Builds the Release configuration of the adapter (and the sample test DLLs of all four
configurations) and runs the unit tests.

.EXAMPLE
.\build.ps1 -SkipSamples
Builds the adapter (Debug and Release) without the sample test DLLs, i.e. without TAEF.
#>

#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string[]] $Configuration = @('Debug', 'Release'),

    [ValidateSet('Debug', 'Release')]
    [string[]] $SampleConfiguration = @('Debug', 'Release'),

    [ValidateSet('Win32', 'x64')]
    [string[]] $SamplePlatform = @('Win32', 'x64'),

    [string] $VsPath,

    [string] $OutRoot,

    [switch] $PrepareOnly,

    [switch] $SkipPrepare,

    [switch] $SkipBuild,

    [switch] $SkipSamples,

    [switch] $SkipAdapter,

    [switch] $ForceDia,

    [string] $DiaTargetDir,

    [switch] $Test,

    [AllowEmptyString()]
    [string] $TestCaseFilter = 'TestCategory=Unit',

    [string[]] $TestProjects = @('DiaResolver.Tests', 'Core.Tests', 'TestAdapter.Tests', 'VsPackage.Tests.Unit'),

    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string] $Verbosity = 'minimal',

    [ValidateRange(0, 256)]
    [int] $MaxCpuCount = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = $PSScriptRoot
$AdapterDir = Join-Path $RepoRoot 'TaefTestAdapter'
$AdapterSolution = Join-Path $AdapterDir 'TaefTestAdapter.sln'
$SampleSolution = Join-Path $RepoRoot 'SampleTests\SampleTests.sln'
$BundledNuGet = Join-Path $RepoRoot 'NuGetPackages\NuGet.CommandLine.5.4.0\tools\NuGet.exe'
$NuGetDownloadUrl = 'https://dist.nuget.org/win-x86-commandline/v5.4.0/nuget.exe'
$SupportedVsVersions = '[17.0,19.0)'

if (-not $DiaTargetDir) { $DiaTargetDir = Join-Path $AdapterDir 'DiaResolver' }
if (-not $OutRoot) { $OutRoot = Join-Path $RepoRoot 'out' }
$OutRoot = [IO.Path]::GetFullPath($OutRoot)
if (-not $OutRoot.EndsWith('\')) { $OutRoot += '\' }

# Never let MSBuild keep worker nodes alive after this script has finished.
$env:MSBUILDDISABLENODEREUSE = '1'


#region Helpers

function Write-Step([string] $Message) {
    Write-Host ''
    Write-Host "=== $Message" -ForegroundColor Cyan
}

# Quotes a single argument according to the MSVCRT / CommandLineToArgvW rules, so that
# arguments such as /p:OutRoot=C:\some dir\ (trailing backslash!) survive unchanged.
function ConvertTo-CommandLineArgument([string] $Argument) {
    if ($Argument.Length -eq 0) { return '""' }
    if ($Argument -notmatch '[\s"]') { return $Argument }

    $builder = New-Object System.Text.StringBuilder
    [void] $builder.Append('"')
    $backslashes = 0
    foreach ($c in $Argument.ToCharArray()) {
        if ($c -eq '\') {
            $backslashes++
        }
        elseif ($c -eq '"') {
            [void] $builder.Append('\' * (2 * $backslashes + 1))
            [void] $builder.Append('"')
            $backslashes = 0
        }
        else {
            [void] $builder.Append('\' * $backslashes)
            [void] $builder.Append($c)
            $backslashes = 0
        }
    }
    [void] $builder.Append('\' * (2 * $backslashes))
    [void] $builder.Append('"')
    return $builder.ToString()
}

# Runs a native tool with a raw, correctly quoted command line; its output goes directly
# to this script's console/stdout. Returns the exit code.
function Invoke-Tool {
    param(
        [Parameter(Mandatory = $true)] [string] $FilePath,
        [string[]] $Arguments = @(),
        [string] $WorkingDirectory = $RepoRoot
    )

    $argumentString = ($Arguments | ForEach-Object { ConvertTo-CommandLineArgument $_ }) -join ' '
    Write-Host "> $(ConvertTo-CommandLineArgument $FilePath) $argumentString" -ForegroundColor DarkGray

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.Arguments = $argumentString
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $process.WaitForExit()
        return $process.ExitCode
    }
    finally {
        $process.Dispose()
    }
}

function Assert-Tool {
    param(
        [Parameter(Mandatory = $true)] [string] $FilePath,
        [string[]] $Arguments = @(),
        [string] $WorkingDirectory = $RepoRoot,
        [Parameter(Mandatory = $true)] [string] $What
    )

    $exitCode = Invoke-Tool -FilePath $FilePath -Arguments $Arguments -WorkingDirectory $WorkingDirectory
    if ($exitCode -ne 0) {
        throw "$What failed with exit code $exitCode."
    }
}

function Test-SameFile([string] $Source, [string] $Target) {
    if (-not (Test-Path -LiteralPath $Target -PathType Leaf)) { return $false }
    if ((Get-Item -LiteralPath $Source).Length -ne (Get-Item -LiteralPath $Target).Length) { return $false }
    return (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $Target -Algorithm SHA256).Hash
}

#endregion


#region Step 1: locate Visual Studio

function Find-VisualStudio {
    if ($VsPath) {
        if (-not (Test-Path -LiteralPath $VsPath -PathType Container)) {
            throw "Visual Studio folder '$VsPath' (parameter -VsPath) does not exist."
        }
        return (Resolve-Path -LiteralPath $VsPath).ProviderPath
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
        throw "vswhere.exe not found at '$vswhere'. Install Visual Studio 2026 or 2022, or pass -VsPath."
    }

    $json = & $vswhere -products '*' -version $SupportedVsVersions -prerelease -requires Microsoft.Component.MSBuild -sort -utf8 -format json
    if ($LASTEXITCODE -ne 0) { throw "vswhere.exe failed with exit code $LASTEXITCODE." }

    # Note: do not pipe into ConvertFrom-Json - Windows PowerShell 5.1 would emit the whole
    # array as a single object.
    $instances = ConvertFrom-Json ($json -join "`n")
    foreach ($instance in $instances) {
        $path = $instance.installationPath
        $hasMsBuild = Test-Path -LiteralPath (Join-Path $path 'MSBuild\Current\Bin\MSBuild.exe') -PathType Leaf
        $hasDia = Test-Path -LiteralPath (Join-Path $path 'DIA SDK\bin\msdia140.dll') -PathType Leaf
        if ($hasMsBuild -and $hasDia) {
            Write-Host "Using $($instance.displayName) $($instance.installationVersion) at $path"
            return $path
        }
        Write-Host "Skipping $path (MSBuild found: $hasMsBuild, DIA SDK found: $hasDia)"
    }

    throw "No Visual Studio 2026/2022 instance with MSBuild and the DIA SDK was found (vswhere -version $SupportedVsVersions). " +
          "Install the 'Desktop development with C++' workload, or pass -VsPath."
}

#endregion


#region Step 2: DIA SDK

function Initialize-Dia([string] $VisualStudio) {
    $diaSdk = Join-Path $VisualStudio 'DIA SDK'

    # the arm64 DLL is optional: without it, the adapter works, but has no source locations in native ARM64 processes
    foreach ($pair in @(@{ Source = 'bin\msdia140.dll'; Target = 'x86'; Optional = $false },
                        @{ Source = 'bin\amd64\msdia140.dll'; Target = 'x64'; Optional = $false },
                        @{ Source = 'bin\arm64\msdia140.dll'; Target = 'arm64'; Optional = $true })) {
        $source = Join-Path $diaSdk $pair.Source
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            if ($pair.Optional) {
                # a copy from an earlier build (e.g. with another Visual Studio) would still be packaged
                $stale = Join-Path (Join-Path $DiaTargetDir $pair.Target) 'msdia140.dll'
                if (Test-Path -LiteralPath $stale -PathType Leaf) {
                    Remove-Item -LiteralPath $stale -Force
                }
                Write-Warning ("DIA SDK file '$source' not found: the adapter is built without $($pair.Target)\msdia140.dll " +
                               "and cannot read the source locations of tests in native ARM64 processes (e.g. the ARM64 test host).")
                continue
            }
            throw "DIA SDK file '$source' not found."
        }
        $targetDir = Join-Path $DiaTargetDir $pair.Target
        $target = Join-Path $targetDir 'msdia140.dll'
        if (Test-SameFile $source $target) {
            Write-Host "Up to date: $target"
        }
        else {
            New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
            Copy-Item -LiteralPath $source -Destination $target -Force
            Write-Host "Copied $source -> $target"
        }
    }

    $dia2Dll = Join-Path $DiaTargetDir 'dia2\dia2.dll'
    if ((Test-Path -LiteralPath $dia2Dll -PathType Leaf) -and -not $ForceDia) {
        Write-Host "Up to date: $dia2Dll (use -ForceDia to regenerate)"
        return
    }

    $idl = Join-Path $diaSdk 'idl\dia2.idl'
    $include = Join-Path $diaSdk 'include'
    $devCmd = Join-Path $VisualStudio 'Common7\Tools\VsDevCmd.bat'
    foreach ($required in @($idl, $include, $devCmd)) {
        if (-not (Test-Path -LiteralPath $required)) { throw "'$required' not found; cannot generate dia2.dll." }
    }

    $tempDir = Join-Path ([IO.Path]::GetTempPath()) ('taef-dia2-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tempDir | Out-Null
    try {
        # midl and tlbimp are only on PATH inside a VS developer environment (same as
        # TaefTestAdapter\DiaResolver\dia2\compile_typelib.ps1, which requires a developer prompt).
        $tempDll = Join-Path $tempDir 'dia2.dll'
        $script = Join-Path $tempDir 'make-dia2.cmd'
        $vsInstallerDir = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
        $midlLog = Join-Path $tempDir 'midl.log'
        $lines = @(
            '@echo off',
            # VsDevCmd.bat's helper scripts call vswhere.exe without a path
            "set `"PATH=$vsInstallerDir;%PATH%`"",
            "call `"$devCmd`" -no_logo -arch=x86 -host_arch=amd64",
            'where midl.exe >nul 2>&1 || (echo ERROR: midl.exe not found in the VS developer environment & exit /b 2)',
            'where tlbimp.exe >nul 2>&1 || (echo ERROR: tlbimp.exe not found in the VS developer environment & exit /b 3)',
            "midl.exe /nologo `"$idl`" /out `"$tempDir`" /I `"$include`" >`"$midlLog`" 2>&1 || (type `"$midlLog`" & exit /b 4)",
            "tlbimp.exe /nologo /silent `"$tempDir\dia2.tlb`" /out:`"$tempDll`" /namespace:Microsoft.Dia || exit /b 5",
            'exit /b 0'
        )
        [IO.File]::WriteAllLines($script, $lines, (New-Object System.Text.UTF8Encoding($false)))

        Assert-Tool -FilePath (Join-Path $env:SystemRoot 'System32\cmd.exe') -Arguments @('/d', '/c', $script) `
                    -WorkingDirectory $tempDir -What 'Generating dia2.dll (midl/tlbimp)'
        if (-not (Test-Path -LiteralPath $tempDll -PathType Leaf)) { throw "tlbimp did not produce '$tempDll'." }

        New-Item -ItemType Directory -Force -Path (Split-Path $dia2Dll) | Out-Null
        Copy-Item -LiteralPath $tempDll -Destination $dia2Dll -Force
        Write-Host "Generated $dia2Dll"
    }
    finally {
        Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

#endregion


#region Step 3: NuGet restore

function Get-NuGetExe {
    if (Test-Path -LiteralPath $BundledNuGet -PathType Leaf) { return $BundledNuGet }

    $onPath = Get-Command nuget.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($onPath) { return $onPath.Source }

    $downloaded = Join-Path $OutRoot 'tools\nuget.exe'
    if (-not (Test-Path -LiteralPath $downloaded -PathType Leaf)) {
        Write-Host "Downloading $NuGetDownloadUrl"
        New-Item -ItemType Directory -Force -Path (Split-Path $downloaded) | Out-Null
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        (New-Object System.Net.WebClient).DownloadFile($NuGetDownloadUrl, $downloaded)
    }
    return $downloaded
}

function Restore-Packages([string] $MsBuild) {
    $nuget = Get-NuGetExe
    $msBuildDir = Split-Path $MsBuild

    $solutions = @()
    if (-not $SkipAdapter) { $solutions += $AdapterSolution }
    if (-not $SkipSamples) { $solutions += $SampleSolution }

    # NuGet.exe evaluates the projects with MSBuild, which takes environment variables as properties: with OutRoot set,
    # it uses the restore folders of this build (see Common.props) instead of those of the default OutRoot
    $previousOutRoot = $env:OutRoot
    $env:OutRoot = $OutRoot
    try {
        foreach ($solution in $solutions) {
            # packages.config projects -> NuGetPackages\ (see NuGet.config)
            Assert-Tool -FilePath $nuget -Arguments @('restore', $solution, '-NonInteractive', '-MSBuildPath', $msBuildDir) `
                        -What "NuGet restore of $(Split-Path $solution -Leaf)"
        }
    }
    finally {
        $env:OutRoot = $previousOutRoot
    }

    foreach ($solution in $solutions) {
        # PackageReference projects (no-op for packages.config projects)
        Assert-Tool -FilePath $MsBuild -Arguments @($solution, '/t:Restore', "/p:OutRoot=$OutRoot", '/nr:false', '/nologo', "/v:$Verbosity") `
                    -What "msbuild /t:Restore of $(Split-Path $solution -Leaf)"
    }
}

#endregion


#region Step 4: build

function Get-MsBuildParallelSwitch {
    if ($MaxCpuCount -eq 0) { return '/m' }
    return "/m:$MaxCpuCount"
}

function Build-Solution([string] $MsBuild, [string] $Solution, [string] $Config, [string] $Platform,
                        [string[]] $ExtraArguments = @()) {
    Write-Step "Building $(Split-Path $Solution -Leaf) ($Config|$Platform)"
    Assert-Tool -FilePath $MsBuild -Arguments (@(
        $Solution,
        '/t:Build',
        "/p:Configuration=$Config",
        "/p:Platform=$Platform",
        "/p:OutRoot=$OutRoot",
        (Get-MsBuildParallelSwitch),
        '/nr:false',
        '/nologo',
        "/v:$Verbosity"
    ) + $ExtraArguments) -What "Building $(Split-Path $Solution -Leaf) ($Config|$Platform)"
}

function Test-TaefKit {
    $roots = @()
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)) {
        try {
            $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
            $key = $base.OpenSubKey('SOFTWARE\Microsoft\Windows Kits\Installed Roots')
            if ($key) {
                $value = $key.GetValue('KitsRoot10')
                if ($value) { $roots += [string] $value }
                $key.Dispose()
            }
            $base.Dispose()
        }
        catch { }
    }
    $roots += Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\'

    foreach ($root in ($roots | Select-Object -Unique)) {
        $header = Join-Path $root 'Testing\Development\inc\WexTestClass.h'
        if (Test-Path -LiteralPath $header -PathType Leaf) {
            Write-Host "TAEF development files: $(Split-Path (Split-Path $header))"
            return $true
        }
    }

    Write-Warning ("TAEF development files (Testing\Development\inc\WexTestClass.h) were not found in the Windows Kits. " +
                   "Install the Windows Driver Kit (WDK), which installs TAEF, to build the sample test DLLs, or use -SkipSamples " +
                   "to build the adapter without them.")
    return $false
}

#endregion


#region Step 5: tests

# Warns about sample test DLLs the adapter's tests will not find. Mirrors how the tests locate them
# (TaefTestAdapter\Tests.Common\TestResources.cs): environment variable TAEF_ADAPTER_SAMPLES_DIR, else
# <OutRoot>binaries\SampleTests\ if it contains samples, else <repo>\out\binaries\SampleTests\.
function Test-SampleDlls {
    $sampleConfigurations = @('Debug', 'Release', 'Debug-x64', 'Release-x64')
    function Test-ContainsSamples([string] $Dir) {
        foreach ($sampleConfiguration in $sampleConfigurations) {
            if (Test-Path -LiteralPath (Join-Path $Dir "$sampleConfiguration\Tests_taef.dll") -PathType Leaf) { return $true }
        }
        return $false
    }

    if ($env:TAEF_ADAPTER_SAMPLES_DIR -and $env:TAEF_ADAPTER_SAMPLES_DIR.Trim()) {
        $samplesDir = $env:TAEF_ADAPTER_SAMPLES_DIR.Trim().Trim('"')
    }
    else {
        $samplesDir = Join-Path $OutRoot 'binaries\SampleTests'
        if (-not (Test-ContainsSamples $samplesDir)) {
            $samplesDir = Join-Path $RepoRoot 'out\binaries\SampleTests'
        }
    }

    $missing = @($sampleConfigurations | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $samplesDir "$_\Tests_taef.dll") -PathType Leaf)
    })
    if ($missing.Count -gt 0) {
        Write-Warning ("Sample test DLLs not found in '$samplesDir' for: $($missing -join ', '). The adapter's tests which " +
                       "use them will fail; build the samples (without -SkipSamples and with all sample configurations and " +
                       "platforms) or set environment variable TAEF_ADAPTER_SAMPLES_DIR.")
    }
    else {
        Write-Host "Sample test DLLs: $samplesDir"
    }
}

function Invoke-Tests([string] $VisualStudio) {
    $vstest = Join-Path $VisualStudio 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe'
    if (-not (Test-Path -LiteralPath $vstest -PathType Leaf)) { throw "vstest.console.exe not found at '$vstest'." }

    $failed = @()
    foreach ($config in $Configuration) {
        $assemblies = @()
        foreach ($project in $TestProjects) {
            $assembly = Join-Path $OutRoot "binaries\TaefTestAdapter\$config\$project\TaefTestAdapter.$project.dll"
            if (Test-Path -LiteralPath $assembly -PathType Leaf) {
                $assemblies += $assembly
            }
            else {
                Write-Warning "Test assembly not found (not built?): $assembly"
            }
        }
        if ($assemblies.Count -eq 0) {
            $failed += $config
            Write-Warning "No test assemblies found for configuration $config."
            continue
        }

        Write-Step "Running tests ($config)"
        $arguments = @($assemblies)
        if ($TestCaseFilter) { $arguments += "/TestCaseFilter:$TestCaseFilter" }
        $exitCode = Invoke-Tool -FilePath $vstest -Arguments $arguments
        if ($exitCode -ne 0) { $failed += $config }
    }

    if ($failed.Count -gt 0) {
        throw "Tests failed (or could not be run) for configuration(s): $($failed -join ', ')."
    }
}

#endregion


#region Main

$stopwatch = [Diagnostics.Stopwatch]::StartNew()

Write-Step 'Locating Visual Studio 2026/2022'
$visualStudio = Find-VisualStudio
$msBuild = Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msBuild -PathType Leaf)) { throw "MSBuild.exe not found at '$msBuild'." }
Write-Host "MSBuild: $msBuild"
Write-Host "OutRoot: $OutRoot"

if (-not $SkipPrepare) {
    Write-Step 'Preparing DIA SDK files'
    Write-Host 'Note: msdia140.dll is part of the Microsoft DIA SDK and subject to the Visual Studio license terms.'
    Initialize-Dia $visualStudio

    Write-Step 'Restoring NuGet packages'
    Restore-Packages $msBuild
}

if ($PrepareOnly) {
    Write-Step "Preparation finished in $([int] $stopwatch.Elapsed.TotalSeconds) s"
    exit 0
}

if (-not $SkipBuild) {
    if (-not $SkipSamples) {
        [void] (Test-TaefKit)
        foreach ($config in $SampleConfiguration) {
            foreach ($platform in $SamplePlatform) {
                Build-Solution $msBuild $SampleSolution $config $platform
            }
        }
    }

    if (-not $SkipAdapter) {
        foreach ($config in $Configuration) {
            # the samples have been built above (or are to be skipped): SampleTestsBuilder must not build them
            Build-Solution $msBuild $AdapterSolution $config 'Any CPU' @('/p:SkipSampleTestsBuild=true')
        }
    }
}

if ($Test) {
    Test-SampleDlls
    Invoke-Tests $visualStudio
}

Write-Step "Build finished in $([int] $stopwatch.Elapsed.TotalSeconds) s"
foreach ($config in $Configuration) {
    $packagingDir = Join-Path $OutRoot "binaries\TaefTestAdapter\$config\Packaging"
    if (Test-Path -LiteralPath $packagingDir) {
        Get-ChildItem -LiteralPath $packagingDir -File |
            Where-Object { $_.Extension -in @('.vsix', '.nupkg') } |
            ForEach-Object { Write-Host "Artifact: $($_.FullName)" }
    }
}

#endregion
