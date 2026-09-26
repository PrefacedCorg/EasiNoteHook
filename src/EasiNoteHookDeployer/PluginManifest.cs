using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace EasiNoteHookDeploy
{
    /// <summary>
    /// 生成插件清单 manifest.coin。
    ///
    /// 格式是白板私有的：以 "&gt;" 开头，之后每三行一组（&gt; / 键 / 值），文件为 UTF-8(BOM)。
    /// 白板靠 NetEntryPoint 找入口 DLL，Version 参与版本比较。
    /// </summary>
    internal static class PluginManifest
    {
        /// <summary>按已释放到 <paramref name="pluginDir"/> 的入口 DLL 生成清单内容。</summary>
        internal static string Build(string pluginDir)
        {
            string version = ReadFileVersion(Path.Combine(pluginDir, PluginLayout.EntryAssembly));

            string[] lines =
            {
                ">", "Id", PluginLayout.Id,
                ">", "Name", "悬浮工具栏拦截",
                ">", "Description", "进程内 stub 白板的桌面模式悬浮工具栏",
                ">", "Version", version,
                ">", "NetEntryPoint", PluginLayout.EntryAssembly,
                ">", "NetFrameworkEntryPoint", PluginLayout.EntryAssembly,
                ">", "Preinstalled", "True",
                ">",
            };

            return string.Join("\r\n", lines) + "\r\n";
        }

        internal static void Write(string pluginDir)
        {
            File.WriteAllText(
                Path.Combine(pluginDir, PluginLayout.ManifestFile),
                Build(pluginDir),
                new UTF8Encoding(true));
        }

        /// <summary>取入口 DLL 的文件版本作为插件版本；取不到就退回 1.0.0.0，不让部署失败。</summary>
        private static string ReadFileVersion(string assemblyPath)
        {
            try
            {
                if (!File.Exists(assemblyPath))
                {
                    return "1.0.0.0";
                }

                return FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion ?? "1.0.0.0";
            }
            catch
            {
                return "1.0.0.0";
            }
        }
    }
}
