namespace EasiNoteHookDeploy
{
    /// <summary>
    /// 内嵌资源的命名规则：资源名前缀决定文件释放到哪。
    ///
    ///   payload/plugin/&lt;文件&gt;  → &lt;白板&gt;\Main\Extensions\EasiNoteToolbarHook\   （插件）
    ///   payload/main/&lt;文件&gt;    → &lt;白板&gt;\Main\                                  （替换白板自带 DLL）
    ///   payload/setup/&lt;文件&gt;   → 临时目录，按需执行                              （一次性 setup）
    ///
    /// 新增文件只要丢进对应的 payload\ 子目录即可，不需要改代码。
    /// </summary>
    internal static class PayloadLayout
    {
        public const string PluginPrefix = "payload/plugin/";
        public const string MainPrefix = "payload/main/";
        public const string SetupPrefix = "payload/setup/";
    }
}
