using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Cvte.EasiNote.Extensions
{
    /// <summary>
    /// 希沃白板（EasiNote5）插件契约类型。
    ///
    /// 加载方式：白板启动时带命令行参数 -EasiPluginFile &lt;本程序集路径&gt;，
    /// 或放在安装目录 Main\Extensions\&lt;目录&gt;\（预装插件，自动加载）；
    /// 由 EasiNote.Business 的 EasiPluginLauncher 用 Assembly.LoadFile 加载本程序集，
    /// 反射查找「Cvte.EasiNote.Extensions.AssemblyProvider」并调用 RunAsync()（找不到则调用 Run()）。
    ///
    /// 注意：类型全名不可修改，否则不会被加载。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AssemblyProvider
    {
        private static readonly string PluginDirectory =
            Path.GetDirectoryName(typeof(AssemblyProvider).Assembly.Location) ?? string.Empty;

        static AssemblyProvider()
        {
            PluginAssemblyResolver.Install(PluginDirectory);
        }

        /// <summary>插件入口：白板启动阶段（UI 创建之前）在进程内被调用。</summary>
        public Task RunAsync()
        {
            try
            {
                PluginLog.Write($"插件已加载：PID={Process.GetCurrentProcess().Id}，插件目录={PluginDirectory}");
                ToolbarKiller.Start();
            }
            catch (Exception ex)
            {
                // 插件入口抛异常会导致白板加载插件失败，吞掉并记为诊断信息
                PluginLog.Write("插件启动失败：" + ex);
            }

            return Task.CompletedTask;
        }
    }
}
