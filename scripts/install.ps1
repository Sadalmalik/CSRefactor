<#
.SYNOPSIS
    Установка csr из исходников: публикация из текущей ветки, копирование в папку приложения, папка - в PATH.

.DESCRIPTION
    Публикует src/CSR (scripts/publish.ps1: win-x64, один файл, .NET внутри) во временную папку и ставит
    ее через scripts/install-release.ps1 -Package: csr.exe и папки BuildHost-* - в папку установки, конфиги по
    умолчанию (-Configs) - в папку config рядом с csr.exe. Без -Configs папка config установки не меняется.
    Скрипт ничего не удаляет за пределами папки установки; запущенный из нее csr (csr serve) - ошибка.

.PARAMETER Configs
    Папка с конфигами по умолчанию (*.jsonc). Без параметра папка config установки не меняется.

.PARAMETER Link
    Вместо копии конфигов - ссылка (junction) config -> Configs: правки конфигов действуют без переустановки.

.PARAMETER Destination
    Папка установки. По умолчанию %LOCALAPPDATA%\Programs\csr.

.PARAMETER NoPath
    Не менять PATH пользователя.

.EXAMPLE
    .\scripts\install.ps1 -Configs D:\my\csr-configs -Link
#>
[CmdletBinding()]
param(
    [string]$Configs,
    [switch]$Link,
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\csr'),
    [switch]$NoPath
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$full = [IO.Path]::GetFullPath($Destination).TrimEnd('\')

# Запущенный из папки установки csr (csr serve) держит файлы: проверка до публикации
$running = @(Get-Process csr -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and $_.Path.StartsWith($full + '\', [StringComparison]::OrdinalIgnoreCase)
})
if ($running.Count -gt 0)
{
    throw "запущен csr из $full (pid $($running.Id -join ', ')): остановить и повторить"
}

$commit = git -C $root rev-parse --short HEAD
$branch = git -C $root rev-parse --abbrev-ref HEAD
$dirty = git -C $root status --porcelain
$publish = Join-Path ([IO.Path]::GetTempPath()) ('csr-publish-' + [Guid]::NewGuid().ToString('N'))
try
{
    & (Join-Path $PSScriptRoot 'publish.ps1') -Output $publish

    $state = if ($dirty) { ', есть незакоммиченные изменения' } else { '' }
    $version = "csr $commit ($branch$state), $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    Set-Content (Join-Path $publish 'version.txt') $version -Encoding UTF8

    $install = @{ Package = $publish; Destination = $Destination; Link = $Link; NoPath = $NoPath }
    if ($Configs)
    {
        $install.Configs = $Configs
    }

    & (Join-Path $PSScriptRoot 'install-release.ps1') @install
}
finally
{
    Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
}
