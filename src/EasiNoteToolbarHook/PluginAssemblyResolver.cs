using System;
using System.IO;
using System.Reflection;

namespace Cvte.EasiNote.Extensions
{
    /// <summary>
    /// 让 0Harmony.dll 能被找到。
    ///
    /// 白板用 <c>Assembly.LoadFile</c> 加载插件，这种方式不会为依赖注册探测路径，
    /// 所以 0Harmony 只能自己从插件所在目录解析，否则 Harmony 一调用就 FileNotFoundException。
    /// </summary>
    internal static class PluginAssemblyResolver
    {
        private const string HarmonySimpleName = "0Harmony";

        private static string? _probeDirectory;
        private static bool _installed;

        /// <summary>注册 AssemblyResolve 钩子。<paramref name="probeDirectory"/> 为插件所在目录。</summary>
        internal static void Install(string probeDirectory)
        {
            _probeDirectory = probeDirectory;

            if (_installed)
            {
                return;
            }

            _installed = true;
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                string simpleName = new AssemblyName(args.Name).Name ?? string.Empty;
                if (!string.Equals(simpleName, HarmonySimpleName, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(_probeDirectory))
                {
                    return null;
                }

                string path = Path.Combine(_probeDirectory, HarmonySimpleName + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            }
            catch
            {
                // 解析失败必须静默：异常会从 CLR 内部抛出，波及宿主进程
                return null;
            }
        }
    }
}
