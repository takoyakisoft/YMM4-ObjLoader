namespace ObjLoader.Utilities.Logging
{
    /// <summary>何もしないシンク。ログ無効化時の置き換えに使用する。</summary>
    public sealed class NullSink : ILogSink
    {
        /// <summary>シングルトンインスタンス。</summary>
        public static readonly NullSink Instance = new();

        private NullSink() { }

        public void Emit(in LogEntry entry) { }
        public void Flush() { }
    }
}