# EasiNote 悬浮工具栏拦截插件 —— 一键编译
#
# 产物输出到 build\：
#   EasiNoteHookDeploy.exe  部署器（.NET Framework 4.7.2，单 exe，插件已内嵌）
#   plugin\                 插件运行时的独立副本，用于手动拷贝排查
#
# 用法：
#   .\build.ps1                  # Release（默认）
#   .\build.ps1 -Configuration Debug
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root   = $PSScriptRoot
$src    = Join-Path $root 'src'
$build  = Join-Path $root 'build'
$pluginOut   = Join-Path $src "EasiNoteToolbarHook\bin\$Configuration\net6.0"
$deployerOut = Join-Path $src "EasiNoteHookDeployer\bin\$Configuration\net472"

Write-Host "=== EasiNote 悬浮工具栏拦截插件 · 编译（$Configuration）===" -ForegroundColor Cyan

# ====== 1. 编译插件（net6.0）======
Write-Host "`n[1/3] 编译插件 EasiNoteToolbarHook.dll (net6.0) ..." -ForegroundColor Cyan
& dotnet build (Join-Path $src 'EasiNoteToolbarHook\EasiNoteToolbarHook.csproj') -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host '[!] 插件编译失败' -ForegroundColor Red; exit 1 }

# ====== 2. 编译部署器（net472，内部会重新编译插件并把产物内嵌）======
Write-Host "`n[2/3] 编译部署器 EasiNoteHookDeploy.exe (net472) ..." -ForegroundColor Cyan
& dotnet build (Join-Path $src 'EasiNoteHookDeployer\EasiNoteHookDeployer.csproj') -c $Configuration --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host '[!] 部署器编译失败' -ForegroundColor Red; exit 2 }

# ====== 3. 收集产物 ======
Write-Host "`n[3/3] 收集产物到 build\ ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $build | Out-Null
$pluginStage = Join-Path $build 'plugin'
New-Item -ItemType Directory -Force -Path $pluginStage | Out-Null

Copy-Item (Join-Path $deployerOut 'EasiNoteHookDeploy.exe') -Destination $build -Force
Get-ChildItem (Join-Path $pluginOut '*.dll') | Copy-Item -Destination $pluginStage -Force

Write-Host "`n========== 编译完成 ==========" -ForegroundColor Yellow
Get-ChildItem $build -Filter '*.exe' | ForEach-Object {
    Write-Host ("  {0,-30} {1,10:N0} bytes" -f $_.Name, $_.Length)
}
Get-ChildItem $pluginStage | ForEach-Object {
    Write-Host ("  plugin\{0,-23} {1,10:N0} bytes" -f $_.Name, $_.Length)
}
Write-Host "`n下一步：运行 .\apply.ps1 部署到白板（-Restore 卸载）" -ForegroundColor Yellow
