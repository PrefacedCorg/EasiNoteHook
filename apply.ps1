# 部署 / 卸载插件到希沃白板（EasiNote5）
#
# 原理：白板启动时会扫描安装目录的 Extensions\* 作为「预装插件」自动加载
#       （EasiPluginDiscover.FindPreinstalledExtensions），只要目录里有
#       manifest.coin（NetEntryPoint 指向 DLL）即可，无需命令行参数、无需改配置；
#       且路径位于 ExtensionsFolder 内，不会触发插件加载上报。
#
# 用法：
#   .\apply.ps1                  # 部署：结束白板 → 释放插件 → 重新打开白板
#   .\apply.ps1 -Restore         # 卸载：删除插件目录（等同于 --remove）
#   .\apply.ps1 -NoRestart       # 只部署，不自动打开白板
#   .\apply.ps1 -DryRun          # 只看会做什么，不动任何东西
#   .\apply.ps1 -Console         # 部署并让这次拉起的白板弹出插件控制台（排查插件是否加载）
#   .\apply.ps1 -NoConsole       # 清除残留的环境变量开关，恢复正常
#
# 写入 Program Files 需要管理员：脚本会自动以管理员身份拉起部署器（弹 UAC）。
param(
    [switch]$Restore,
    [switch]$NoRestart,
    [switch]$NoKill,
    [switch]$DryRun,
    [switch]$Console,
    [switch]$NoConsole,
    [string]$InstallRoot
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $PSScriptRoot 'build\EasiNoteHookDeploy.exe'
if (-not (Test-Path $exe)) {
    throw "未找到 $exe`n请先运行 .\build.ps1"
}

$argList = [System.Collections.Generic.List[string]]::new()
if ($Restore)  { $argList.Add('--remove') }
if ($NoRestart){ $argList.Add('--no-restart') }
if ($NoKill)   { $argList.Add('--no-kill') }
if ($DryRun)   { $argList.Add('--dry-run') }
if ($Console)  { $argList.Add('--console') }
if ($NoConsole){ $argList.Add('--no-console') }
if ($InstallRoot) { $argList.Add('--install-root'); $argList.Add($InstallRoot) }

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

Write-Host "=== EasiNote 悬浮工具栏拦截插件 · $(if ($Restore) { '卸载' } else { '部署' }) ===" -ForegroundColor Cyan

# --dry-run 不写文件，不需要管理员；其余情况交给部署器自己的 manifest 弹 UAC
if (-not $isAdmin -and -not $DryRun) {
    Write-Host '[*] 需要管理员权限，正在请求提权（会弹 UAC 授权框）...' -ForegroundColor Yellow
    $p = Start-Process -FilePath $exe -ArgumentList $argList -Verb RunAs -Wait -PassThru
    exit $p.ExitCode
}

& $exe @argList
exit $LASTEXITCODE
