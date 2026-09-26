# EasiNote 悬浮工具栏拦截插件

让希沃白板（EasiNote5）「桌面模式」的悬浮工具栏（实测 550x200）不再出现。

原 `EasiNoteToolbarHook`（插件）与 `EasiNoteHookDeployer`（部署器）已合并到本仓库，
目录结构参照 `Patch`：`src\` 放源码、`build\` 放产物、`build.ps1` 编译、`apply.ps1` 部署。

## 目录结构

```
EasiNoteHook/
├─ Directory.Build.props      共享属性：版本号、插件 Id/名称
├─ build.ps1                  一键编译 → build\
├─ apply.ps1                  部署 / 卸载入口
├─ build\                     编译产物（不入库）
│  ├─ EasiNoteHookDeploy.exe  部署器，单 exe，插件与 payload 已内嵌
│  └─ plugin\                 插件运行时副本
├─ payload\                   随部署器分发的外部文件（内嵌进 exe）
│  ├─ main\                   替换到白板 Main\ 的 DLL（原文件备份为 .bak）
│  └─ setup\                  一体机伪装 setup（不可逆，按需执行）
└─ src\
   ├─ EasiNoteToolbarHook/    插件（net6.0，注入白板进程）
   │  ├─ AssemblyProvider.cs      白板插件契约入口（类型全名不可改）
   │  ├─ ToolbarKiller.cs         编排：Harmony stub + WinEvent 兜底
   │  ├─ DesktopModePatcher.cs    Harmony stub 桌面模式启动方法
   │  ├─ DockToolbarWatcher.cs    WinEvent 钩子兜底隐藏
   │  ├─ DockToolbarSignature.cs  悬浮工具栏识别特征（改版时只改这里）
   │  ├─ PluginAssemblyResolver.cs 0Harmony 解析
   │  ├─ PluginLog.cs             诊断输出（仅控制台）
   │  └─ NativeMethods.cs         Win32 P/Invoke
   └─ EasiNoteHookDeployer/   部署器（net472，部署机无需运行时）
      ├─ Program.cs               流程编排
      ├─ DeployOptions.cs         命令行解析
      ├─ InstallLocator.cs        定位白板安装目录
      ├─ PluginManifest.cs        生成 manifest.coin
      ├─ PayloadExtractor.cs      释放内嵌插件文件 / 卸载
      ├─ ProcessHelper.cs         结束与启动白板
      ├─ ConsoleUi.cs             输出、暂停、提权
      ├─ PluginLayout.cs          插件身份与布局约定
      ├─ PayloadLayout.cs         内嵌资源前缀 → 释放目标
      ├─ MainPatcher.cs           替换 Main\ DLL（带 .bak，可还原）
      ├─ SetupRunner.cs           一体机伪装 setup 询问与执行
      ├─ MachineState.cs          本机级持久化（setup 是否运行过）
      └─ DeployException.cs       可预期的失败
```

### 内嵌资源命名决定释放位置

| 资源前缀 | 释放到 |
|---|---|
| `payload/plugin/<文件>` | `<白板>\Main\Extensions\EasiNoteToolbarHook\` |
| `payload/main/<文件>` | `<白板>\Main\`（替换自带 DLL，原文件备份为 `<原名>.bak`） |
| `payload/setup/<文件>` | 临时目录，按需执行 |

新增文件只要放进对应的 `payload\` 子目录，不需要改代码。

## 用法

```powershell
.\build.ps1          # 编译（Release）
.\apply.ps1          # 部署：结束白板 → 释放插件 → 重新打开白板
.\apply.ps1 -Restore # 卸载
.\apply.ps1 -DryRun  # 只看会做什么，不动任何东西
```

部署目标：`<白板>\Main\Extensions\EasiNoteToolbarHook\`。
插件是「预装插件」形式，白板启动时自动加载，不需要命令行参数。

直接跑部署器也可以：

```
build\EasiNoteHookDeploy.exe [--install-root <目录>] [--no-kill] [--kill-all-easi]
                             [--no-restart] [--no-elevate] [--dry-run] [--remove] [--pause]
                             [--setup | --no-setup]
```

部署时会做 5 件事：定位白板 → 结束白板进程 → 释放插件 + 替换 `Main\` 下的
`SWCoreSharp.SWAuthorization.SWAuthClients.dll` → （可选）一体机伪装 setup → 重新打开白板。

## 一体机伪装 setup（不可逆）

`payload\setup\setup.exe` 会把本机伪装成希沃一体机，白板打开后直接进入大屏授课模式。

因为**运行后无法恢复**，它不是默认动作，而是在启动白板之前单独询问：

```
[4/5] 一体机伪装 setup（可选，不可逆）
      ┌─ 一体机伪装 setup ─────────────────────────────
      │ 作用：把本机伪装成希沃一体机，白板打开直接进入大屏授课模式。
      │ 注意：运行后无法恢复，且需要管理员权限。
      └───────────────────────────────────────────────
      是否运行？不可逆操作，默认否 [y/N]
```

- 默认 **否**，必须明确输入 `y` / `yes` 才执行（回车 = 不运行）
- `--setup` 不问直接运行，`--no-setup` 不问直接跳过
- 需要管理员权限，未提权时不会运行并给出提示
- **持久化**：执行成功后写入 `HKLM\SOFTWARE\EasiNoteHook\BigScreenSetupDoneAt`，
  （写不了注册表时退回 `%ProgramData%\EasiNoteHook\bigscreen-setup.done`）。
  同一台电脑之后再部署会显示「本机已于 xxx 运行过，不重复执行」，不再询问。
- setup 退出码非 0 时**不**记为已执行，下次仍会询问

## Main DLL 替换

`payload\main\*` 会被复制到白板 `Main\` 目录覆盖同名文件。替换前把原文件备份为
`<原名>.bak`；已存在 `.bak` 时不覆盖，保证备份始终是最原始的那一份。
`--remove` 卸载时会从 `.bak` 还原。

## 拦截原理（双保险）

1. **根治** — Harmony 把桌面模式启动方法 stub 掉，窗口根本不会被创建。
   候选目标按「最贴近窗口创建」排序，改版改名时依次回退：
   `Cvte.EasiNote.UI.DesktopMode.DesktopBoardManager:Start`、
   `Cvte.EasiNote.UI.DesktopMode.PerformanceDesktopMode:Start`。
2. **兜底** — 进程内 WinEvent 钩子，工具栏一旦 Show 立刻隐藏。不依赖方法名，
   白板改版后仍然有效，代价是可能有一帧闪烁。

两者都跑：根治失效时兜底仍能顶住。

## 注意事项

- 插件目标框架必须是 **net6.0**：白板 5.2.4.11451 自带 .NET 6 运行时。
- `AssemblyProvider` 的类型全名 `Cvte.EasiNote.Extensions.AssemblyProvider` 不可修改，
  否则白板不会加载。
- 诊断输出只到控制台，**永远不写日志文件**。三种情况：
  1. 白板本身有控制台 → 直接输出；
  2. 从 cmd/PowerShell 启动白板 → `AttachConsole` 挂到父控制台，输出可见：
     ```powershell
     cd "C:\Program Files (x86)\Seewo\EasiNote5\EasiNote5_5.2.4.11451\Main"
     .\EasiNote.exe
     ```
  3. 双击/开始菜单启动（父进程是 Explorer，没有控制台）→ 默认静默；
     设环境变量 `EASINOTEHOOK_CONSOLE=1` 后插件会**自建一个控制台窗口**显示日志
     （会多出一个黑窗口，所以默认关闭，只在排查时临时开）。

## 让部署器拉起的那次白板弹控制台

```powershell
build\EasiNoteHookDeploy.exe --console      # 只有这次启动的白板弹控制台
build\EasiNoteHookDeploy.exe --no-console   # 清理遗留的用户级变量（迁移用，见下）
```

`--console` **不写任何持久化开关**：环境变量只加在部署器自己的进程里，由它启动的那个白板
继承一份，所以**以后双击 / 开始菜单打开白板完全不受影响**，不会莫名多出黑窗口。

代价：要传环境变量就必须直接启动、不能走 `explorer.exe`（ShellExecute 传不了），
所以部署器处于提权状态时，**这一次**白板也会以管理员身份运行 —— 关掉白板重开即恢复正常。

> 早期版本曾把开关写进用户级环境变量（`HKCU\Environment`），那会污染双击启动。
> 如果你之前用过那个版本，跑一次 `--no-console` 清掉即可。
- 默认只结束 `EasiNote*` 进程。`EasiUpdate*` / `EasiUpdate3Protect` / `EasiScreenPerception`
  是希沃的更新与保护组件，杀掉会导致更新/校验异常（需要时加 `--kill-all-easi`）。
- 悬浮工具栏的识别特征（类名 `HwndWrapper[EasiNote`、样式 `0x16080000`、尺寸 550x200）
  来自实测，白板改版后需更新 `DockToolbarSignature.cs`。
