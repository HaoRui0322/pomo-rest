$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found" }

$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$gac = 'C:\Windows\Microsoft.NET\assembly'

function Find-Assembly([string]$name) {
    $found = Get-ChildItem -Path $gac -Recurse -Filter $name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { return $found.FullName }
    $inFw = Join-Path $fw $name
    if (Test-Path $inFw) { return $inFw }
    throw "Assembly not found: $name"
}

$refs = @(
    (Find-Assembly 'System.Xaml.dll'),
    (Find-Assembly 'WindowsBase.dll'),
    (Find-Assembly 'PresentationCore.dll'),
    (Find-Assembly 'PresentationFramework.dll')
)
$refArgs = @()
foreach ($r in $refs) { $refArgs += "/r:$r" }

$dist = Join-Path $root 'dist'
$build = Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $dist, $build | Out-Null

# Run unit tests first.
& (Join-Path $root 'test.ps1')
if ($LASTEXITCODE -ne 0) { throw "Unit tests failed (exit $LASTEXITCODE)" }

$srcFiles = @()
$srcFiles += (Get-ChildItem -Path (Join-Path $root 'src') -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName })

$resources = @(
    "/resource:$(Join-Path $root 'src\UI\Theme.xaml'),Theme.xaml",
    "/resource:$(Join-Path $root 'src\UI\MainWindow.xaml'),MainWindow.xaml",
    "/resource:$(Join-Path $root 'src\UI\BreakWindow.xaml'),BreakWindow.xaml",
    "/resource:$(Join-Path $root 'src\Assets\app.ico'),app.ico"
)

$out = Join-Path $dist 'PomoRest.exe'
$args = @(
    '/nologo','/target:winexe','/optimize+','/codepage:65001','/platform:x64',
    "/out:$out",
    "/win32icon:$(Join-Path $root 'src\Assets\app.ico')",
    "/win32manifest:$(Join-Path $root 'src\app.manifest')"
) + $refArgs + $resources + $srcFiles

& $csc $args
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }

Write-Host "Build OK -> $out"
