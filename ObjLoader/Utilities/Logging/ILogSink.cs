namespace ObjLoader.Utilities.Logging
{
    /// <summary>
    /// ログの出力先を抽象化するインターフェース。
    /// 新しい出力先を追加する場合はこのインターフェースを実装する。
    /// </summary>
    public interface ILogSink
    {
        /// <summary>ログエントリを出力する。</summary>
        /// <param name="entry">出力するログエントリ。</param>
        void Emit(in LogEntry entry);

        /// <summary>バッファされたログを強制的にフラッシュする。</summary>
        void Flush();
    }
}