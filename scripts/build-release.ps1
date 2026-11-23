<#
.SYNOPSIS
    Сборка архива релиза csr-<тег>-win-x64.zip: тот же набор, что ставит install.ps1, и конфиги по умолчанию.

.DESCRIPTION
    Публикует csr (scripts/publish.ps1) в папку <Output>/csr-<тег>-win-x64, добавляет version.txt и папку config
    с примерами из examples/ (<вид>.example.jsonc -> config/<вид>.jsonc), проверяет запуск csr.exe и упаковывает
    содержимое папки в <Output>/csr-<тег>-win-x64.zip. Тот же скрипт запускает workflow релиза на тег v*.

.PARAMETER Tag
    Тег релиза: v<версия>, например v1.2.0 или v1.2.0-rc.1. Версия сборки - тег без v.

.PARAMETER Output
    Папка результата; по умолчанию dist в корне репозитория.

.EXAMPLE
    .\scripts\build-release.ps1 -Tag v1.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^v\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Tag,
    [string]$Output
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $Output)
{
    $Output = Join-Path $root 'dist'
}

$Output = [IO.Path]::GetFullPath($Output)
$name = "csr-$Tag-win-x64"
$package = Join-Path $Output $name
$zip = Join-Path $Output "$name.zip"

#region Публикация

if (Test-Path $package)
{
    Remove-Item $package -Recurse -Force
}

& (Join-Path $PSScriptRoot 'publish.ps1') -Output $package -Version $Tag.Substring(1)

#endregion


#region Конфиги и версия

$config = New-Item -ItemType Directory (Join-Path $package 'config')
foreach ($example in Get-ChildItem (Join-Path $root 'examples') -Filter '*.example.jsonc')
{
    $kind = $example.Name -replace '\.example\.jsonc$', ''
    Copy-Item $example.FullName (Join-Path $config "$kind.jsonc")
}

$commit = git -C $root rev-parse --short HEAD
$version = "csr $Tag ($commit), $(Get-Date -Format 'yyyy-MM-dd')"
Set-Content (Join-Path $package 'version.txt') $version -Encoding UTF8

$help = & (Join-Path $package 'csr.exe') help | Select-Object -First 1
if ($LASTEXITCODE -ne 0 -or -not $help)
{
    throw "csr.exe help: код $LASTEXITCODE"
}

#endregion


#region Архив

# Записи по одной с разделителем /: Compress-Archive и ZipFile.CreateFromDirectory в Windows PowerShell 5.1
# (.NET Framework) пишут пути с обратной косой чертой
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
if (Test-Path $zip)
{
    Remove-Item $zip -Force
}

$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try
{
    foreach ($file in Get-ChildItem $package -Recurse -File)
    {
        $entry = $file.FullName.Substring($package.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry) | Out-Null
    }
}
finally
{
    $archive.Dispose()
}

#endregion

$size = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "$version"
Write-Host "$zip ($size МБ)"
