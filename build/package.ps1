param([string]$Configuration = 'Release', [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assembly = Join-Path $root "src/bin/$Configuration/WinMemoryCleaner.exe"
if (!(Test-Path -LiteralPath $assembly)) { throw 'Compile and test first.' }
$gitOptions = @('-c', "safe.directory=$root", '-C', $root)
& git @gitOptions diff --quiet HEAD
if ($LASTEXITCODE -ne 0) { throw 'Commit source changes before packaging.' }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($assembly)
$label = 'ARCA-v{0}.{1}' -f $version.FileMajorPart,$version.FileMinorPart
if ($version.FileBuildPart -gt 0) { $label += '.' + $version.FileBuildPart }
$release = if ($OutputDirectory) { [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory)) } else { Join-Path $root 'releases' }
$portable = Join-Path $release 'portable'
if (Test-Path -LiteralPath $portable) { throw 'Archive the previous local releases folder before packaging.' }
New-Item -ItemType Directory -Path $portable -Force | Out-Null
$sourceZip = Join-Path $release "$label-source.zip"
& git @gitOptions archive --format=zip "--output=$sourceZip" HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source archive failed.' }
Copy-Item -LiteralPath $assembly -Destination (Join-Path $portable 'ARCA.exe')
Copy-Item -LiteralPath (Join-Path (Split-Path $assembly) 'nunit.framework.dll') -Destination $portable
foreach ($name in @('LICENSE','THIRD_PARTY_NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $portable }
Copy-Item -LiteralPath (Join-Path $root 'src/packages/NUnit.2.6.4/license.txt') -Destination (Join-Path $portable 'NUNIT-LICENSE.txt')
Copy-Item -LiteralPath $sourceZip -Destination $portable
$readme = @"
$label - Another RAM Cleaner Application

Extract the complete folder and open ARCA.exe. Keep nunit.framework.dll next to it.
The window opens without administrator rights. System cleaning requests Windows authorization.
Canceling that prompt cancels the operation. Authorize with the same Windows account.
Startup: Memoria RAM > Opciones avanzadas > Iniciar con Windows.
Starting ARCA never performs automatic cleaning.

Source and license are included (GPL-3.0). The application is not Authenticode signed.
https://github.com/Faviogames/ARCA
"@
[IO.File]::WriteAllText((Join-Path $portable 'LEEME.txt'),$readme,(New-Object Text.UTF8Encoding($false)))
$portableZip = Join-Path $release "$label-portable.zip"
Compress-Archive -LiteralPath $portable -DestinationPath $portableZip
$lines = foreach ($file in @($portableZip,$sourceZip)) { '{0}  {1}' -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant(),(Split-Path $file -Leaf) }
[IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'),$lines,(New-Object Text.UTF8Encoding($false)))
Get-Item -LiteralPath $portableZip,$sourceZip
