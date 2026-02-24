using System.Diagnostics;

namespace ObjLoader.Utilities.Logging
{
    /// <summary>
    /// System.Diagnostics.Debug へログを出力するシンク。
    /// Debug.WriteLineは内部でスレッドセーフなため外部ロックなし。
    /// </summary>
    public sealed class DebugSink : ILogSink
    {
        /// <summary>
        /// 使用するスタイル設定を取得または設定する。
        /// デフォルトは<see cref="LogStyleConfig.Default"/>。
        /// </summary>
        public LogStyleConfig Style { get; set; } = LogStyleConfig.Default;

        /// <summary>
        /// ログエントリをDebug出力ストリームに書き込む。
        /// </summary>
        /// <param name="entry">出力するログエントリ。</param>
        public void Emit(in LogEntry entry)
        {
            Debug.WriteLine(LogFormatter.Format(in entry, Style));
        }

        /// <summary>
        /// Debug出力にバッファはないため、何もしない。
        /// </summary>
        public void Flush()
        {
        }
    }
}