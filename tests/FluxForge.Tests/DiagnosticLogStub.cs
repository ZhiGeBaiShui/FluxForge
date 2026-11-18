namespace FluxForge.Mod
{
    // FlowGridAdapter 的诊断走游戏日志；独立测试将同一条消息写到标准错误。
    internal static class FFModEntry
    {
        internal static readonly DiagnosticTestLog Log = new DiagnosticTestLog();
    }

    internal sealed class DiagnosticTestLog
    {
        public void Error(string message)
        {
            System.Console.Error.WriteLine(message);
        }
    }
}
