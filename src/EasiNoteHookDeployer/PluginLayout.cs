namespace EasiNoteHookDeploy
{
    /// <summary>插件在白板里的身份与布局约定。白板靠 Id 和 NetEntryPoint 认插件，改动会让已部署目录失效。</summary>
    internal static class PluginLayout
    {
        /// <summary>插件目录名（Extensions 下的子目录），同时用作 manifest 里的 Id。</summary>
        public const string Id = "EasiNoteToolbarHook";

        /// <summary>入口 DLL 文件名，manifest 的 NetEntryPoint 指向它。</summary>
        public const string EntryAssembly = "EasiNoteToolbarHook.dll";

        /// <summary>白板读取的插件清单文件名。</summary>
        public const string ManifestFile = "manifest.coin";
    }
}
