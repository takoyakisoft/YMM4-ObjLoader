namespace ObjLoader.Utilities.Logging
{
    /// <summary>
    /// 単一のログイベントを表す不変レコード構造体。
    /// 高精度タイムスタンプとスレッド情報を含む。
    /// </summary>
    public readonly record struct LogEntry
    {
        /// <summary>ログが記録された時刻。</summary>
        public DateTimeOffset Timestamp { get; init; }

        /// <summary>アプリケーション起動からの経過時間（高精度）。</summary>
        public TimeSpan Elapsed { get; init; }

        /// <summary>ログレベル。</summary>
        public LogLevel Level { get; init; }

        /// <summary>ログの発生元カテゴリ名。</summary>
        public string Category { get; init; }

        /// <summary>ログメッセージ本文。</summary>
        public string Message { get; init; }

        /// <summary>関連する例外情報（ない場合はnull）。</summary>
        public Exception? Exception { get; init; }

        /// <summary>ログを記録したマネージドスレッドID。</summary>
        public int ManagedThreadId { get; init; }
    }
}