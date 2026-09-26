using System;

namespace EasiNoteHookDeploy
{
    /// <summary>可预期的部署失败。消息直接显示给用户，不附带堆栈。</summary>
    internal sealed class DeployException : Exception
    {
        public DeployException(string message) : base(message)
        {
        }

        public DeployException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
