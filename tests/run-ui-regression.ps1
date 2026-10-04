$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$qaFolder = Join-Path $projectRoot 'build\ui-qa'
New-Item -ItemType Directory -Force -Path $qaFolder | Out-Null
$env:MARS_PET_PREFS = $qaFolder
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$gac = Join-Path $env:WINDIR 'Microsoft.NET\assembly'
$refs = @('System.dll','System.Core.dll') | ForEach-Object { Join-Path $framework $_ }
$refs += @(
    (Join-Path $gac 'GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll'),
    (Join-Path $gac 'GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll'),
    (Join-Path $gac 'GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll'),
    (Join-Path $gac 'GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll')
)
$program = Join-Path $qaFolder 'UiRegression.exe'
$arguments = @('/nologo','/target:exe',"/out:$program")
$arguments += $refs | ForEach-Object { "/r:$_" }
$arguments += Join-Path $PSScriptRoot 'UiRegression.cs'
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'UI regression compilation failed.' }
& $program (Join-Path $projectRoot 'desktop-pet\WhaleOverlay.exe') $qaFolder
if ($LASTEXITCODE -ne 0) { throw 'UI regression failed.' }
