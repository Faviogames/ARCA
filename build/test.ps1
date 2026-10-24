param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$runner = Join-Path $root 'src/packages/NUnit.Runners.2.6.4/tools/nunit-console.exe'
$assembly = Join-Path $root "src/bin/$Configuration/WinMemoryCleaner.exe"
if (!(Test-Path -LiteralPath $runner) -or !(Test-Path -LiteralPath $assembly)) { throw 'Compile first with build/build.ps1.' }
$fixtures = 'WinMemoryCleaner.Test.OneClickCleanTests,WinMemoryCleaner.Test.TopMemoryAppsTests,WinMemoryCleaner.Test.BrowserCloseTests,WinMemoryCleaner.Test.FloatingBubbleTests+ClassifyTests,WinMemoryCleaner.Test.FloatingBubbleTests+ShouldShowTests,WinMemoryCleaner.Test.FloatingBubbleTests+ViewModelTests,WinMemoryCleaner.Test.FloatingBubbleTests+WindowLifecycleTests'
& $runner $assembly /nologo "/run:$fixtures" "/xml:$root/src/bin/ARCA-tests.xml"
exit $LASTEXITCODE
