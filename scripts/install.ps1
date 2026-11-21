<#
.SYNOPSIS
    Установка csr: публикация из текущей ветки, копирование в папку приложения, папка - в PATH пользователя.

.DESCRIPTION
    Публикует src/CSR (win-x64, один файл, .NET внутри) во временную папку и копирует csr.exe и папки
    BuildHost-* в папку установки. Конфиги по умолчанию (-Configs) кладутся в папку config рядом с csr.exe:
    без --config csr берет их первым слоем, проектный слой - .csr в папке цели или выше (до корня репозитория).
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

#region Проверки

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\CSR\CSR.csproj'
$Destination = [IO.Path]::GetFullPath($Destination).TrimEnd('\')

if ($Link -and -not $Configs)
{
    throw '-Link требует -Configs'
}

if ($Configs)
{
    $Configs = (Resolve-Path $Configs).Path.TrimEnd('\')
    if (-not (Get-ChildItem $Configs -Filter '*.jsonc'))
    {
        throw "в $Configs нет файлов *.jsonc"
    }
}

# Запущенный из папки установки csr (csr serve) держит файлы
$running = @(Get-Process csr -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and $_.Path.StartsWith($Destination + '\', [StringComparison]::OrdinalIgnoreCase)
})
if ($running.Count -gt 0)
{
    throw "запущен csr из $Destination (pid $($running.Id -join ', ')): остановить и повторить"
}

#endregion


#region Публикация и копирование

$commit = git -C $root rev-parse --short HEAD
$branch = git -C $root rev-parse --abbrev-ref HEAD
$dirty = git -C $root status --porcelain
$publish = Join-Path ([IO.Path]::GetTempPath()) ('csr-publish-' + [Guid]::NewGuid().ToString('N'))

dotnet publish $project -nologo -v minimal -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PackAsTool=false -p:DebugType=none `
    -o $publish
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish: код $LASTEXITCODE"
}

New-Item -ItemType Directory -Force $Destination | Out-Null
Get-ChildItem $Destination -Directory -Filter 'BuildHost-*' | Remove-Item -Recurse -Force
Copy-Item (Join-Path $publish '*') $Destination -Recurse -Force
Remove-Item $publish -Recurse -Force

#endregion


#region Конфиги по умолчанию

$config = Join-Path $Destination 'config'
if ($Configs)
{
    if (Test-Path $config)
    {
        $item = Get-Item $config -Force
        # Ссылка удаляется без содержимого: файлы в папке-источнике не трогаются
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)
        {
            [IO.Directory]::Delete($config)
        }
        else
        {
            Remove-Item $config -Recurse -Force
        }
    }

    if ($Link)
    {
        New-Item -ItemType Junction -Path $config -Target $Configs | Out-Null
    }
    else
    {
        New-Item -ItemType Directory $config | Out-Null
        Copy-Item (Join-Path $Configs '*.jsonc') $config
    }
}

$state = if ($dirty) { ', есть незакоммиченные изменения' } else { '' }
$source = if (-not $Configs) { 'без изменений' } elseif ($Link) { "ссылка на $Configs" } else { "копия $Configs" }
$version = "csr $commit ($branch$state), $(Get-Date -Format 'yyyy-MM-dd HH:mm'); config: $source"
Set-Content (Join-Path $Destination 'version.txt') $version -Encoding UTF8

#endregion


#region PATH пользователя

function Test-PathEntry([string]$List, [string]$Folder)
{
    return [bool](($List -split ';') | Where-Object { $_.TrimEnd('\') -ieq $Folder })
}

if (-not $NoPath)
{
    # Значение из реестра без раскрытия %...%: SetEnvironmentVariable записал бы раскрытые пути строкой REG_SZ
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
    $userPath = [string]$key.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    if (-not (Test-PathEntry $userPath $Destination))
    {
        $newPath = (@($userPath -split ';' | Where-Object { $_ }) + $Destination) -join ';'
        $key.SetValue('Path', $newPath, [Microsoft.Win32.RegistryValueKind]::ExpandString)

        # WM_SETTINGCHANGE: новые процессы проводника и терминалов видят новый PATH
        Add-Type -Namespace Csr -Name Native -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam, string lParam,
    uint flags, uint timeout, out UIntPtr result);
'@
        $result = [UIntPtr]::Zero
        [Csr.Native]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [UIntPtr]::Zero, 'Environment', 2, 5000, [ref]$result) |
            Out-Null
        Write-Host "PATH пользователя: + $Destination (действует в новых терминалах)"
    }

    $key.Close()
    if (-not (Test-PathEntry $env:Path $Destination))
    {
        $env:Path += ";$Destination"
    }
}

#endregion

Write-Host $version
& (Join-Path $Destination 'csr.exe') help | Select-Object -First 1
