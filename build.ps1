# Builds Quadra.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
# No SDK install required.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }

$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
$sources = (Get-ChildItem (Join-Path $root 'src') -Filter *.cs).FullName

& $csc /nologo /target:winexe /optimize+ /warn:4 `
    "/out:$(Join-Path $out 'Quadra.exe')" `
    "/win32manifest:$(Join-Path $root 'src\app.manifest')" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed (csc exit code $LASTEXITCODE)" }
Write-Host "Built $(Join-Path $out 'Quadra.exe')"
