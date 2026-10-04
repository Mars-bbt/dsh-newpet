param([string]$OutputPath = (Join-Path $PSScriptRoot 'WhaleOverlay.exe'))
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$assemblies = Join-Path $env:WINDIR 'Microsoft.NET\assembly'
$refs = @('System.dll','System.Core.dll','System.Web.Extensions.dll') | ForEach-Object { Join-Path $framework $_ }
$refs += @(
    (Join-Path $assemblies 'GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll'),
    (Join-Path $assemblies 'GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll'),
    (Join-Path $assemblies 'GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll'),
    (Join-Path $assemblies 'GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll')
)
$arguments = @('/nologo','/noconfig','/target:winexe',"/out:$OutputPath")
$arguments += $refs | ForEach-Object { "/r:$_" }
$arguments += Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
