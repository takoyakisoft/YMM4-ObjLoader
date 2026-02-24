namespace ObjLoader.Utilities.Logging
{
    /// <summary>
    /// ログの重要度を表す列挙型。
    /// 値が大きいほど重要度が高い。
    /// </summary>
    public enum LogLevel
    {
        /// <summary>最も詳細なトレース情報。</summary>
        Trace = 0,

        /// <summary>デバッグ用の詳細情報。</summary>
        Debug = 1,

        /// <summary>一般的な情報メッセージ。</summary>
        Info = 2,

        /// <summary>正常完了の通知。</summary>
        Success = 3,

        /// <summary>潜在的な問題の警告。</summary>
        Warning = 4,

        /// <summary>エラー発生の通知。</summary>
        Error = 5,

        /// <summary>致命的なエラー。アプリケーションの継続が困難。</summary>
        Fatal = 6,

        /// <summary>即座に注意が必要なアラート。</summary>
        Alert = 7
    }
}