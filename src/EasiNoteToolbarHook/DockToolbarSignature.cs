using System;
using System.Runtime.Versioning;
using System.Text;

namespace Cvte.EasiNote.Extensions
{
    /// <summary>
    /// 悬浮工具栏的识别特征。
    ///
    /// 数值来自实测（白板 5.2.4.11451）：Class=HwndWrapper[EasiNote;;{GUID}]、Style=0x16080000、Size=550x200。
    /// 这些是"猜"出来的特征，白板改版就会失效，所以集中放在这里，改版时只动这一个文件。
    /// 尺寸按 DPI 缩放后再比对，容差 2px 覆盖整数取整误差。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class DockToolbarSignature
    {
        /// <summary>WPF 原生窗口类名前缀：HwndWrapper[{AppDomain友好名};{线程名};{GUID}]，白板进程友好名为 EasiNote。</summary>
        public string ClassNamePrefix { get; init; } = "HwndWrapper[EasiNote";

        /// <summary>窗口样式：无边框 + 工具窗口 + 置顶的组合值。</summary>
        public int WindowStyle { get; init; } = 0x16080000;

        /// <summary>96 DPI 下的设计宽度。</summary>
        public double Width { get; init; } = 550;

        /// <summary>96 DPI 下的设计高度。</summary>
        public double Height { get; init; } = 200;

        /// <summary>尺寸比对容差（像素）。</summary>
        public double Tolerance { get; init; } = 2;

        public static DockToolbarSignature Default { get; } = new();

        /// <summary>逐个特征比对，任一不匹配即不是目标窗口。</summary>
        internal bool Matches(IntPtr hwnd)
        {
            var className = new StringBuilder(256);
            if (NativeMethods.GetClassName(hwnd, className, className.Capacity) == 0)
            {
                return false;
            }

            if (!className.ToString().StartsWith(ClassNamePrefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlStyle) != WindowStyle)
            {
                return false;
            }

            if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.Rect rect))
            {
                return false;
            }

            double scale = GetScale(hwnd);
            return Math.Abs(rect.Width - Width * scale) <= Tolerance
                && Math.Abs(rect.Height - Height * scale) <= Tolerance;
        }

        private static double GetScale(IntPtr hwnd)
        {
            try
            {
                uint dpi = NativeMethods.GetDpiForWindow(hwnd);
                return dpi > 0 ? dpi / 96.0 : 1.0;
            }
            catch
            {
                // Win10 1607 以下没有 GetDpiForWindow
                return 1.0;
            }
        }
    }
}
