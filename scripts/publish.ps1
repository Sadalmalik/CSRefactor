<#
.SYNOPSIS
    Публикация csr: win-x64, один файл, .NET внутри (self-contained), папки BuildHost-* рядом.

.DESCRIPTION
    Общий шаг install.ps1 и build-release.ps1. В папке Output после публикации - csr.exe и папки
    BuildHost-net472, BuildHost-netcore (BuildHost Roslyn запускается отдельным процессом и в csr.exe
    не упаковывается). .NET на машине для самого csr.exe не нужен.

.PARAMETER Output
    Папка публикации; создается, содержимое с теми же именами заменяется.

.PARAMETER Version
    Версия сборки (свойство Version, например 1.2.0); по умолчанию - версия проекта.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Output,
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\CSR\CSR.csproj'
$properties = @(
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PackAsTool=false',
    '-p:DebugType=none'
)
if ($Version)
{
    $properties += "-p:Version=$Version"
}

dotnet publish $project -nologo -v minimal -c Release -r win-x64 --self-contained @properties -o $Output
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish: код $LASTEXITCODE"
}

foreach ($name in 'csr.exe', 'BuildHost-net472', 'BuildHost-netcore')
{
    if (-not (Test-Path (Join-Path $Output $name)))
    {
        throw "публикация без $name"
    }
}
