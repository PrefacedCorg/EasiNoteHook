using System;
using System.IO;
using System.Linq;

namespace EasiNoteHookDeploy
{
    /// <summary>定位白板安装目录。安装根目录下面按版本号分目录（EasiNote5_x.y.z.n），取版本最高的那个。</summary>
    internal static class InstallLocator
    {
        public const string DefaultInstallRoot = @"C:\Program Files (x86)\Seewo\EasiNote5";

        private const string VersionFolderPrefix = "EasiNote5_";
        private const string ExeRelativePath = @"Main\EasiNote.exe";
        private const string ExtensionsRelativePath = @"Main\Extensions";

        public static InstallInfo Locate(string installRoot)
        {
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                throw new DeployException("未指定白板安装根目录");
            }

            if (!Directory.Exists(installRoot))
            {
                throw new DeployException(
                    "白板安装目录不存在：" + installRoot + "\n  可用 --install-root <目录> 指定实际位置。");
            }

            // 目录名带版本号，必须按版本号排序，字符串排序会把 5.10 排到 5.9 前面
            var candidates = Directory.GetDirectories(installRoot, VersionFolderPrefix + "*")
                .Select(dir => new { Dir = dir, Version = ParseFolderVersion(Path.GetFileName(dir)) })
                .OrderByDescending(x => x.Version)
                .ThenByDescending(x => x.Dir, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var item in candidates)
            {
                string exe = Path.Combine(item.Dir, ExeRelativePath);
                if (!File.Exists(exe))
                {
                    continue;
                }

                string extensionsDir = Path.Combine(item.Dir, ExtensionsRelativePath);
                if (!Directory.Exists(extensionsDir))
                {
                    // 白板允许没有 Extensions 目录（没装过插件就会是这种情况），部署时创建即可
                    ConsoleUi.Info("      Extensions 目录尚不存在，部署时将创建：" + extensionsDir);
                }

                return new InstallInfo(Path.GetFileName(item.Dir), exe, extensionsDir);
            }

            if (candidates.Count == 0)
            {
                throw new DeployException(
                    "在 " + installRoot + " 下没有找到 " + VersionFolderPrefix + "* 版本目录，白板可能未安装。");
            }

            throw new DeployException(
                "找到了版本目录但没有 " + ExeRelativePath + "，安装目录结构可能已变化：" + installRoot);
        }

        private static Version ParseFolderVersion(string folderName)
        {
            string text = folderName.StartsWith(VersionFolderPrefix, StringComparison.OrdinalIgnoreCase)
                ? folderName.Substring(VersionFolderPrefix.Length)
                : folderName;
            return Version.TryParse(text, out Version version) ? version : new Version(0, 0);
        }
    }

    /// <summary>一次定位的结果。</summary>
    internal sealed class InstallInfo
    {
        public InstallInfo(string versionFolder, string exe, string extensionsDir)
        {
            VersionFolder = versionFolder;
            Exe = exe;
            ExtensionsDir = extensionsDir;
        }

        /// <summary>版本目录名，例如 EasiNote5_5.2.4.11451。</summary>
        public string VersionFolder { get; }

        public string Exe { get; }

        public string ExtensionsDir { get; }

        /// <summary>Main 目录：白板自带 DLL（含要替换的 SWAuthClients）都在这里。</summary>
        public string MainDir => Path.GetDirectoryName(Exe);

        public string PluginDir => Path.Combine(ExtensionsDir, PluginLayout.Id);
    }
}
