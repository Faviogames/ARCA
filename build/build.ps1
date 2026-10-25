# RamPrototype build without Visual Studio: requires the .NET SDK and nuget.exe in PATH or $env:NUGET.
param([string]$Configuration = 'Release', [switch]$Test, [switch]$SkipRestore)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'src\WinMemoryCleaner.csproj'
$nuget = if ($env:NUGET) { $env:NUGET } else { 'nuget' }

# Restore packages.config directly: nuget.exe would otherwise need a Visual Studio MSBuild.
if (!$SkipRestore) {
    & $nuget restore (Join-Path $root 'src\packages.config') -PackagesDirectory (Join-Path $root 'src\packages') -NonInteractive | Out-Null
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$refs = Join-Path $root 'src\packages\Microsoft.NETFramework.ReferenceAssemblies.net40.1.0.3\build\'
dotnet msbuild $proj -nologo -v:m "-p:Configuration=$Configuration" -p:CI=true `
    "-p:TargetFrameworkRootPath=$refs" `
    "-p:CustomAfterMicrosoftCommonTargets=$PSScriptRoot\RamPrototype.Build.targets"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Test) {
    $runner = Join-Path $root 'src\packages\NUnit.Runners.2.6.4\tools\nunit-console.exe'
    & $runner (Join-Path $root "src\bin\$Configuration\ARCA.exe") /nologo "/xml:$root\src\bin\TestResults.xml"
    exit $LASTEXITCODE
}
