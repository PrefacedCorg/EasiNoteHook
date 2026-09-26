using System;
using System.Runtime.Versioning;
using System.Threading;

namespace Cvte.EasiNote.Extensions
{
    /// <summary>
    /// 兜底方案：进程内 WinEvent 钩子，工具栏一旦 Show 立刻隐藏。
    ///
    /// 不依赖任何方法名，白板改版（或 Harmony stub 没命中）时仍然有效，
    /// 代价是窗口会被创建出来再隐藏，可能有一帧闪烁。
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class DockToolbarWatcher
    {
        private const int MaxSessions = 5;
        private const int RestartDelayMs = 1000;

        private static int _hiddenCount;

        /// <summary>
        /// 在当前线程跑消息循环。WinEvent 回调依赖消息泵，所以必须独占一个 STA 后台线程。
        ///
        /// 消息泵可能因为 WM_QUIT 或 GetMessage 出错而退出 —— 之前退出后钩子就静默失效了，
        /// 现在会打日志并重新安装钩子（最多 MaxSessions 次）。
        /// </summary>
        internal static void Run()
        {
            try
            {
                for (int attempt = 1; attempt <= MaxSessions; attempt++)
                {
                    int code = PumpSession(attempt);

                    // >0 不会从循环里出来；0 = WM_QUIT，-1 = 出错
                    string reason = code == 0 ? "收到 WM_QUIT" : "GetMessage 出错 (code=" + code + ")";
                    PluginLog.Write($"WinEvent 消息循环退出（{reason}），累计隐藏 {Volatile.Read(ref _hiddenCount)} 次");

                    if (attempt >= MaxSessions)
                    {
                        PluginLog.Write("兜底监视已停止，不再重新安装钩子。");
                        return;
                    }

                    PluginLog.Write("正在重新安装 WinEvent 钩子...");
                    Thread.Sleep(RestartDelayMs);
                }
            }
            catch (Exception ex)
            {
                // 后台线程未捕获异常会直接结束宿主进程，必须吞掉
                PluginLog.Write("兜底监视线程异常：" + ex.Message);
            }
        }

        /// <summary>安装一次钩子并跑消息泵，返回 GetMessage 的退出码。</summary>
        private static int PumpSession(int attempt)
        {
            NativeMethods.WinEventDelegate callback = OnWindowEvent;
            IntPtr hook = NativeMethods.SetWinEventHook(
                NativeMethods.EventObjectCreate,
                NativeMethods.EventObjectShow,
                IntPtr.Zero,
                callback,
                0,
                0,
                NativeMethods.WineventOutofcontext);

            if (hook == IntPtr.Zero)
            {
                PluginLog.Write("SetWinEventHook 安装失败，兜底监视未启用");
                return -2;
            }

            PluginLog.Write(attempt == 1
                ? "进程内 WinEvent 兜底监视已启动"
                : "WinEvent 钩子已重新安装（第 " + attempt + " 次）");

            try
            {
                int code;
                while ((code = NativeMethods.GetMessage(out NativeMethods.NativeMessage msg, IntPtr.Zero, 0, 0)) > 0)
                {
                    NativeMethods.TranslateMessage(ref msg);
                    NativeMethods.DispatchMessage(ref msg);
                }

                return code;
            }
            finally
            {
                NativeMethods.UnhookWinEvent(hook);

                // 委托必须活到 Unhook，否则回调期间被回收会直接崩进程
                GC.KeepAlive(callback);
            }
        }

        private static void OnWindowEvent(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                if (idObject != NativeMethods.ObjIdWindow || idChild != 0 || hwnd == IntPtr.Zero)
                {
                    return;
                }

                if (!DockToolbarSignature.Default.Matches(hwnd))
                {
                    return;
                }

                NativeMethods.ShowWindow(hwnd, NativeMethods.SwHide);
                Interlocked.Increment(ref _hiddenCount);
                PluginLog.Write($"已隐藏悬浮工具栏 0x{hwnd.ToInt64():X}（事件 {eventType}，累计 {Volatile.Read(ref _hiddenCount)} 次）");
            }
            catch (Exception ex)
            {
                PluginLog.Write("WinEvent 回调异常：" + ex.Message);
            }
        }
    }
}
