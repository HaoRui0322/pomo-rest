$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found" }

$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$gac = 'C:\Windows\Microsoft.NET\assembly'

# The default csc response file already references the common framework
# assemblies (System, System.Core, System.Xml, System.Runtime.Serialization).
# No explicit references are required for the pure core/service tests.
$refArgs = @()

$build = Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $build | Out-Null

$files = @()
$files += (Get-ChildItem -Path (Join-Path $root 'src\Core') -Filter '*.cs' | ForEach-Object { $_.FullName })
$files += (Join-Path $root 'src\Services\AppPaths.cs')
$files += (Join-Path $root 'src\Services\StateStore.cs')
$files += (Join-Path $root 'src\Services\HistoryStore.cs')
$files += (Join-Path $root 'src\Services\StartupManager.cs')
$files += (Get-ChildItem -Path (Join-Path $root 'tests') -Filter '*.cs' | ForEach-Object { $_.FullName })

$out = Join-Path $build 'CoreTests.exe'
$args = @(
    '/nologo','/target:exe','/optimize-','/codepage:65001','/platform:anycpu',
    "/out:$out"
) + $refArgs + $files

& $csc $args
if ($LASTEXITCODE -ne 0) { throw "Test build failed (exit $LASTEXITCODE)" }

& $out
exit $LASTEXITCODE
