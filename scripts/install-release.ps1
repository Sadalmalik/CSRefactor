<#
.SYNOPSIS
    Установка или обновление csr из релиза GitHub без клонирования и без .NET SDK.

.DESCRIPTION
    Скачивает архив csr-<тег>-win-x64.zip последнего релиза (или -Version), распаковывает во временную папку и
    копирует csr.exe, папки BuildHost-* и version.txt в папку установки; папка установки - в PATH пользователя.
    Конфиги: с -Configs - копия или ссылка, как у install.ps1; без -Configs - примеры из архива, только если папки
    config в установке еще нет (свои конфиги при обновлении не заменяются). Повторный запуск - обновление.
    Запущенный из папки установки процесс (csr serve и его BuildHost) - ошибка; с -Stop процессы останавливаются.
    Скрипт самодостаточный: запускается одной командой по ссылке на raw-файл, без файла на диске (TrimStart -
    метка UTF-8 в начале файла, с ней ScriptBlock.Create не разбирает текст):

        & ([scriptblock]::Create((irm https://raw.githubusercontent.com/Sadalmalik/CSRefactor/main/scripts/install-release.ps1).TrimStart([char]0xFEFF)))

.PARAMETER Version
    Тег релиза, например v1.2.0. По умолчанию - последний релиз.

.PARAMETER Zip
    Архив релиза: путь к файлу или URL http(s). Вместо поиска релиза на GitHub.

.PARAMETER Package
    Папка с готовой публикацией (csr.exe, BuildHost-*, version.txt) вместо архива; так ставит install.ps1.

.PARAMETER Repository
    Репозиторий GitHub с релизами, owner/name.

.PARAMETER Configs
    Папка с конфигами по умолчанию (*.jsonc) - заменяет папку config установки.

.PARAMETER Link
    Вместо копии конфигов - ссылка (junction) config -> Configs.

.PARAMETER Destination
    Папка установки. По умолчанию %LOCALAPPDATA%\Programs\csr.

.PARAMETER NoPath
    Не менять PATH пользователя.

.PARAMETER Stop
    Остановить процессы, запущенные из папки установки (csr serve), вместо ошибки.

.EXAMPLE
    & ([scriptblock]::Create((irm https://raw.githubusercontent.com/Sadalmalik/CSRefactor/main/scripts/install-release.ps1).TrimStart([char]0xFEFF))) -Version v1.0.0 -Stop
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Zip,
    [string]$Package,
    [string]$Repository = 'Sadalmalik/CSRefactor',
    [string]$Configs,
    [switch]$Link,
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\csr'),
    [switch]$NoPath,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
# Без полосы прогресса Invoke-WebRequest в Windows PowerShell 5.1 скачивает в разы быстрее
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor
    [Net.SecurityProtocolType]::Tls12

#region Проверки

$Destination = [IO.Path]::GetFullPath($Destination).TrimEnd('\')

if ($Link -and -not $Configs)
{
    throw '-Link требует -Configs'
}

if (@($Version, $Zip, $Package | Where-Object { $_ }).Count -gt 1)
{
    throw '-Version, -Zip и -Package - взаимоисключающие'
}

if ($Configs)
{
    $Configs = (Resolve-Path $Configs).Path.TrimEnd('\')
    if (-not (Get-ChildItem $Configs -Filter '*.jsonc'))
    {
        throw "в $Configs нет файлов *.jsonc"
    }
}

# csr serve и его BuildHost держат файлы папки установки
function Get-InstalledProcess([string]$Folder)
{
    return @(Get-Process | Where-Object {
        $_.Path -and $_.Path.StartsWith($Folder + '\', [StringComparison]::OrdinalIgnoreCase)
    })
}

$running = Get-InstalledProcess $Destination
if ($running.Count -gt 0)
{
    $list = ($running | ForEach-Object { "$($_.Name) $($_.Id)" }) -join ', '
    if (-not $Stop)
    {
        throw "запущены процессы из ${Destination}: $list. Остановить (csr serve --list, csr serve --stop) или повторить с -Stop"
    }

    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
    Write-Host "остановлены: $list"
}

#endregion


$work = Join-Path ([IO.Path]::GetTempPath()) ('csr-install-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $work | Out-Null
try
{
    #region Архив релиза

    if ($Package)
    {
        $source = (Resolve-Path $Package).Path
    }
    else
    {
        if (-not $Zip)
        {
            $release = if ($Version) { "tags/$Version" } else { 'latest' }
            $api = "https://api.github.com/repos/$Repository/releases/$release"
            try
            {
                $info = Invoke-RestMethod $api -Headers @{ 'User-Agent' = 'csr-install-release' }
            }
            catch
            {
                throw "релиз не найден ($api): $($_.Exception.Message)"
            }

            $asset = @($info.assets | Where-Object { $_.name -like 'csr-*-win-x64.zip' }) | Select-Object -First 1
            if (-not $asset)
            {
                throw "в релизе $($info.tag_name) нет архива csr-*-win-x64.zip"
            }

            $Zip = $asset.browser_download_url
        }

        if ($Zip -match '^https?://')
        {
            $archive = Join-Path $work 'csr.zip'
            Write-Host "загрузка $Zip"
            Invoke-WebRequest $Zip -OutFile $archive -UseBasicParsing
        }
        else
        {
            $archive = (Resolve-Path $Zip).Path
        }

        $source = Join-Path $work 'package'
        Expand-Archive $archive $source
    }

    foreach ($name in 'csr.exe', 'BuildHost-net472', 'BuildHost-netcore')
    {
        if (-not (Test-Path (Join-Path $source $name)))
        {
            throw "в $source нет $name"
        }
    }

    #endregion


    #region Копирование

    New-Item -ItemType Directory -Force $Destination | Out-Null
    Get-ChildItem $Destination -Directory -Filter 'BuildHost-*' | Remove-Item -Recurse -Force
    Get-ChildItem $source | Where-Object { $_.Name -ne 'config' } | Copy-Item -Destination $Destination -Recurse -Force

    #endregion


    #region Конфиги по умолчанию

    $config = Join-Path $Destination 'config'
    $packageConfig = Join-Path $source 'config'
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
            $state = "ссылка на $Configs"
        }
        else
        {
            New-Item -ItemType Directory $config | Out-Null
            Copy-Item (Join-Path $Configs '*.jsonc') $config
            $state = "копия $Configs"
        }
    }
    elseif (-not (Test-Path $config) -and (Test-Path $packageConfig))
    {
        Copy-Item $packageConfig $config -Recurse
        $state = 'примеры из релиза'
    }
    else
    {
        $state = 'без изменений'
    }

    $versionFile = Join-Path $Destination 'version.txt'
    $version = "$((Get-Content $versionFile -Encoding UTF8 -Raw).Trim()); config: $state"
    Set-Content $versionFile $version -Encoding UTF8

    #endregion
}
finally
{
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}


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
        if (-not ('Csr.Native' -as [type]))
        {
            Add-Type -Namespace Csr -Name Native -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
public static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam, string lParam,
    uint flags, uint timeout, out UIntPtr result);
'@
        }

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
