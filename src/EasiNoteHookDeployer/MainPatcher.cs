using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EasiNoteHookDeploy
{
    /// <summary>
    /// 替换白板 Main 目录下的 DLL（目前是 SWCoreSharp.SWAuthorization.SWAuthClients.dll，
    /// 用于让白板认为本机有一体机授权）。
    ///
    /// 替换前把原文件备份成 &lt;原名&gt;.bak，卸载时可还原；
    /// 已存在 .bak 时不再覆盖，保证备份的始终是最原始的那一份。
    /// </summary>
    internal static class MainPatcher
    {
        private const string BackupSuffix = ".bak";

        /// <summary>替换 payload/main/* 下的全部文件，返回（文件名, 字节数）清单。</summary>
        internal static IReadOnlyList<PayloadFile> Replace(string mainDir)
        {
            if (!Directory.Exists(mainDir))
            {
                throw new DeployException("白板 Main 目录不存在：" + mainDir);
            }

            var written = new List<PayloadFile>();
            foreach (PayloadFile payload in EnumerateMainPayload())
            {
                string target = Path.Combine(mainDir, payload.Name);
                string backup = target + BackupSuffix;

                if (File.Exists(target) && !File.Exists(backup))
                {
                    File.Copy(target, backup, true);
                    ConsoleUi.Info("      备份原文件 → " + Path.GetFileName(backup));
                }

                WriteResource(payload.ResourceName, target);
                written.Add(new PayloadFile(payload.Name, payload.ResourceName, new FileInfo(target).Length));
            }

            if (written.Count == 0)
            {
                throw new DeployException("没有打包任何 payload/main/ 文件，构建产物可能不完整。");
            }

            return written;
        }

        /// <summary>卸载时从 .bak 还原。返回还原的文件名列表。</summary>
        internal static IReadOnlyList<string> Restore(string mainDir)
        {
            var restored = new List<string>();
            foreach (PayloadFile payload in EnumerateMainPayload())
            {
                string target = Path.Combine(mainDir, payload.Name);
                string backup = target + BackupSuffix;
                if (!File.Exists(backup))
                {
                    continue;
                }

                File.Copy(backup, target, true);
                restored.Add(payload.Name);
            }

            return restored;
        }

        /// <summary>只列出 payload/main/* 的文件名，不写盘。供 --dry-run 展示。</summary>
        internal static IReadOnlyList<string> Describe()
        {
            return EnumerateMainPayload().Select(p => p.Name).ToList();
        }

        private static IEnumerable<PayloadFile> EnumerateMainPayload()
        {
            foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (!resource.StartsWith(PayloadLayout.MainPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return new PayloadFile(
                    resource.Substring(PayloadLayout.MainPrefix.Length), resource, 0);
            }
        }

        private static void WriteResource(string resourceName, string target)
        {
            try
            {
                using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
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
}
