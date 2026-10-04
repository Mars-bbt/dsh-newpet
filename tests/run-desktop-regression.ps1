$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$program = Join-Path $env:TEMP 'MarsNewPet.DesktopRegression.exe'
& $compiler /nologo /target:exe "/out:$program" (Join-Path $PSScriptRoot 'DesktopRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression harness compilation failed.' }
& $program ([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\desktop-pet\WhaleOverlay.exe')))
if ($LASTEXITCODE -ne 0) { throw 'Desktop regression failed.' }
