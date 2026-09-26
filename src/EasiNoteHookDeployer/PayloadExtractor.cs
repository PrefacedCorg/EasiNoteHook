using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EasiNoteHookDeploy
{
    /// <summary>把内嵌的插件文件释放到白板的 Extensions\&lt;PluginId&gt;\ 目录。</summary>
    internal static class PayloadExtractor
    {
        /// <summary>释放全部内嵌 DLL 并写入 manifest，返回（文件名, 字节数）清单。</summary>
        internal static IReadOnlyList<PayloadFile> Extract(string pluginDir)
        {
            Directory.CreateDirectory(pluginDir);

            var written = new List<PayloadFile>();
            foreach (PayloadFile payload in EnumeratePayload())
            {
                string target = Path.Combine(pluginDir, payload.Name);
                WriteResource(payload.ResourceName, target);
                written.Add(new PayloadFile(payload.Name, payload.ResourceName, new FileInfo(target).Length));
            }

            if (written.Count == 0)
            {
                throw new DeployException("本 exe 内没有打包任何插件文件（" + PayloadLayout.PluginPrefix + "*），构建产物可能不完整。");
            }

            PluginManifest.Write(pluginDir);
            return written;
        }

        /// <summary>删除已部署目录。不存在则视为已卸载。</summary>
        internal static bool Remove(string pluginDir)
        {
            if (!Directory.Exists(pluginDir))
            {
                return false;
            }

            try
            {
                Directory.Delete(pluginDir, true);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                throw new DeployException("删除失败（白板可能仍在运行）：" + pluginDir + " → " + ex.Message, ex);
            }
        }

        /// <summary>只列出内嵌的插件文件，不写盘。供 --dry-run 展示。</summary>
        internal static IReadOnlyList<string> Describe()
        {
            return EnumeratePayload().Select(p => p.Name).ToList();
        }

        /// <summary>按资源名前缀枚举，文件清单由构建脚本决定，代码里不硬编码。</summary>
        private static IEnumerable<PayloadFile> EnumeratePayload()
        {
            Assembly self = Assembly.GetExecutingAssembly();
            foreach (string resource in self.GetManifestResourceNames())
            {
                if (!resource.StartsWith(PayloadLayout.PluginPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                string fileName = resource.Substring(PayloadLayout.PluginPrefix.Length);
                if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return new PayloadFile(fileName, resource, 0);
            }
        }

        private static void WriteResource(string resourceName, string target)
        {
            Assembly self = Assembly.GetExecutingAssembly();
            try
            {
                using (Stream source = self.GetManifestResourceStream(resourceName))
                {
                    if (source == null)
                    {
                        throw new DeployException("内嵌资源缺失：" + resourceName);
                    }

                    using (FileStream dest = File.Create(target))
                    {
                        source.CopyTo(dest);
                    }
                }
            }
            catch (IOException ex)
            {
                throw new DeployException("写入被占用或失败（白板可能仍在运行）：" + target + " → " + ex.Message, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new DeployException("写入被拒绝（需要管理员权限）：" + target, ex);
            }
        }
    }

    internal sealed class PayloadFile
    {
        public PayloadFile(string name, string resourceName, long size)
        {
            Name = name;
            ResourceName = resourceName;
            Size = size;
        }

        public string Name { get; }

        public string ResourceName { get; }

        public long Size { get; }
    }
}
