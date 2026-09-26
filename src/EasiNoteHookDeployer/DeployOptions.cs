using System;
using System.Collections.Generic;

namespace EasiNoteHookDeploy
{
    /// <summary>命令行选项。只做解析与取值，不做任何判断之外的动作。</summary>
    internal sealed class DeployOptions
    {
        /// <summary>白板安装根目录。下面按 EasiNote5_* 子目录找版本号最高的那个。</summary>
        public string InstallRoot { get; private set; } = InstallLocator.DefaultInstallRoot;

        public bool NoKill { get; private set; }

        public bool KillAllEasi { get; private set; }

        public bool NoRestart { get; private set; }

        public bool NoElevate { get; private set; }

        public bool DryRun { get; private set; }

        public bool Pause { get; private set; }

        public bool ShowHelp { get; private set; }

        /// <summary>卸载：删除已部署的插件目录。</summary>
        public bool Remove { get; private set; }

        /// <summary>一体机伪装 setup 的处理方式。</summary>
        public SetupChoice Setup { get; private set; } = SetupChoice.Ask;

        /// <summary>
        /// 是否设置用户级环境变量 EASINOTEHOOK_CONSOLE（让插件自建控制台窗口显示日志）。
        /// null = 不动当前设置。
        /// </summary>
        public bool? ConsoleSwitch { get; private set; }

        /// <summary>解析失败时返回 null（并把原因写到 stderr）。</summary>
        public static DeployOptions Parse(string[] args)
        {
            var options = new DeployOptions();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--install-root":
                        if (i + 1 >= args.Length)
                        {
                            Console.Error.WriteLine("--install-root 缺少参数");
                            return null;
                        }

                        options.InstallRoot = args[++i];
                        break;
                    case "--no-kill":
                        options.NoKill = true;
                        break;
                    case "--kill-all-easi":
                        options.KillAllEasi = true;
                        break;
                    case "--no-restart":
                        options.NoRestart = true;
                        break;
                    case "--no-elevate":
                        options.NoElevate = true;
                        break;
                    case "--dry-run":
                        options.DryRun = true;
                        break;
                    case "--pause":
                        options.Pause = true;
                        break;
                    case "--remove":
                    case "--uninstall":
                        options.Remove = true;
                        break;
                    case "--setup":
                        options.Setup = SetupChoice.Run;
                        break;
                    case "--no-setup":
                        options.Setup = SetupChoice.Skip;
                        break;
                    case "--console":
                        options.ConsoleSwitch = true;
                        break;
                    case "--no-console":
                        options.ConsoleSwitch = false;
                        break;
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        break;
                    default:
                        Console.Error.WriteLine("未知参数：" + args[i]);
                        return null;
                }
            }

            return options;
        }

        /// <summary>提权重启时重建命令行：原样带上已有参数，并确保带 --pause 让结果看得见。</summary>
        public IEnumerable<string> ToArgv()
        {
            if (!string.Equals(InstallRoot, InstallLocator.DefaultInstallRoot, StringComparison.OrdinalIgnoreCase))
            {
                yield return "--install-root";
                yield return InstallRoot;
            }

            if (NoKill)
            {
                yield return "--no-kill";
            }

            if (KillAllEasi)
            {
                yield return "--kill-all-easi";
            }

            if (NoRestart)
            {
                yield return "--no-restart";
            }

            if (DryRun)
            {
                yield return "--dry-run";
            }

            if (Remove)
            {
                yield return "--remove";
            }

            if (Setup == SetupChoice.Run)
            {
                yield return "--setup";
            }

            if (Setup == SetupChoice.Skip)
            {
                yield return "--no-setup";
            }

            if (ConsoleSwitch == true)
            {
                yield return "--console";
            }

            if (ConsoleSwitch == false)
            {
                yield return "--no-console";
            }

            // 提权后的新窗口是独立控制台，必须停住，否则一闪而过
            yield return "--pause";
        }

        public static void PrintUsage()
        {
            Console.WriteLine();
            Console.WriteLine("EasiNoteHookDeploy.exe - 部署/卸载 EasiNote 悬浮工具栏拦截插件");
            Console.WriteLine();
            Console.WriteLine("用法：EasiNoteHookDeploy.exe [选项]");
            Console.WriteLine();
            Console.WriteLine("默认流程：结束所有 EasiNote* 进程 → 释放插件文件 → 自动打开白板");
            Console.WriteLine();
            Console.WriteLine("  --install-root <目录>  白板安装根目录（默认 " + InstallLocator.DefaultInstallRoot + "）");
            Console.WriteLine("  --no-kill              不结束进程（默认结束所有 EasiNote* 进程）");
            Console.WriteLine("  --kill-all-easi        连 EasiUpdate / EasiUpdate3Protect / EasiCamera 等一起结束（有风险）");
            Console.WriteLine("  --no-restart           部署后不自动打开白板（默认会自动打开）");
            Console.WriteLine("  --no-elevate           不自动提权（目标目录不需要管理员时使用）");
            Console.WriteLine("  --dry-run              只显示将要执行的操作，不杀进程/不写文件/不重启");
            Console.WriteLine("  --remove, --uninstall  卸载：删除已部署的插件目录并还原被替换的 DLL");
            Console.WriteLine("  --setup                直接运行一体机伪装 setup，不问（不可逆）");
            Console.WriteLine("  --no-setup             跳过一体机伪装 setup，不问");
            Console.WriteLine("  --console              设置 EASINOTEHOOK_CONSOLE=1：白板启动时插件弹控制台显示日志");
            Console.WriteLine("  --no-console           清除该环境变量，恢复正常（排查完建议执行）");
            Console.WriteLine("  --pause                结束前按回车（自动提权时会默认带上）");
            Console.WriteLine("  -h, --help             显示帮助");
            Console.WriteLine();
        }
    }

    /// <summary>一体机伪装 setup（不可逆）的处理方式。</summary>
    internal enum SetupChoice
    {
        /// <summary>默认：本机没运行过才询问，运行过就直接跳过。</summary>
        Ask,

        /// <summary>--setup：不问，直接运行。</summary>
        Run,

        /// <summary>--no-setup：不问，直接跳过。</summary>
        Skip,
    }
}
