namespace ObjLoader.Utilities.Logging
{
    /// <summary>
    /// ログ出力のスタイルとフォーマットを定義する設定クラス。
    /// <para>
    /// 出力例:
    /// <c>[10:05:42.381] [+0.003s] [Warning ] [MyService] メッセージ (T:1)</c>
    /// </para>
    /// </summary>
    public sealed class LogStyleConfig
    {
        /// <summary>
        /// タイムスタンプのフォーマット文字列（DateTimeOffset.Tostring形式）。
        /// <list type="bullet">
        ///   <item><c>"HH:mm:ss.fff"</c> → <c>10:05:42.381</c>（デフォルト）</item>
        ///   <item><c>"HH:mm:ss"</c> → <c>10:05:42</c></item>
        ///   <item><c>"yyyy-MM-dd HH:mm:ss.fff"</c> → <c>2026-02-24 10:05:42.381</c></item>
        ///   <item><c>null</c> → タイムスタンプを非表示</item>
        /// </list>
        /// </summary>
        public string? TimeFormat { get; set; } = "HH:mm:ss.fff";

        /// <summary>
        /// 経過時間を表示するかどうか。
        /// 表示例: <c>+0.003s</c>
        /// </summary>
        public bool ShowElapsed { get; set; } = true;

        /// <summary>
        /// ログレベルを角括弧付きで表示するかどうか。
        /// 表示例: <c>[Warning ]</c>
        /// </summary>
        public bool ShowLevel { get; set; } = true;

        /// <summary>
        /// ログレベル表示の幅（文字数）。この幅に揃えてパディングされる。
        /// <para>
        /// 0以上の値を指定する。0は自動（パディングなし）。
        /// デフォルトは7（"Warning "のように最長レベル名に合わせた幅）。
        /// </para>
        /// <list type="bullet">
        ///   <item>7（デフォルト）→ <c>[Warning ]</c> / <c>[Info    ]</c></item>
        ///   <item>3              → <c>[WRN]</c> / <c>[INF]</c>（短縮タグ使用）</item>
        ///   <item>0              → <c>[Warning]</c> / <c>[Info]</c>（パディングなし）</item>
        /// </list>
        /// </summary>
        public int LevelWidth { get; set; } = 7;

        /// <summary>
        /// ログレベルをフルネームで表示するか、短縮タグ（3文字）で表示するかを指定する。
        /// <c>true</c>: <c>[Warning ]</c> / <c>false</c>: <c>[WRN]</c>
        /// <para>LevelWidthが3以下の場合は自動的に短縮タグが使われる。</para>
        /// </summary>
        public bool UseLevelFullName { get; set; } = true;

        /// <summary>
        /// カテゴリ名を表示するかどうか。
        /// 表示例: <c>[MyService]</c>
        /// </summary>
        public bool ShowCategory { get; set; } = true;

        /// <summary>
        /// スレッドIDを表示するかどうか。
        /// 表示例: <c>(T:1)</c>
        /// </summary>
        public bool ShowThreadId { get; set; } = true;

        /// <summary>
        /// レベルバッジにアイコン（絵文字）を表示するかどうか。
        /// <c>true</c>: <c>⚠ WRN</c> / <c>false</c>: <c>WRN</c>
        /// </summary>
        public bool ShowLevelIcon { get; set; } = true;

        /// <summary>
        /// ANSI装飾（カラーコード）を出力に含めるかどうか。
        /// ファイル出力など、ANSIをサポートしない環境では<c>false</c>を指定する。
        /// </summary>
        public bool UseAnsiColors { get; set; } = true;

        /// <summary>
        /// 例外の詳細（スタックトレース）を出力に含めるかどうか。
        /// </summary>
        public bool ShowExceptionDetail { get; set; } = true;

        /// <summary>
        /// タイムスタンプを角括弧で囲むかどうか。
        /// <c>true</c>: <c>[10:05:42.381]</c> / <c>false</c>: <c>10:05:42.381</c>
        /// </summary>
        public bool BracketTimestamp { get; set; } = false;

        /// <summary>
        /// デフォルト設定（スタイル付き・Debug出力向け）を返す。
        /// </summary>
        public static LogStyleConfig Default { get; } = new();

        /// <summary>
        /// プレーンテキスト設定（ANSIなし・ファイル出力向け）を返す。
        /// </summary>
        public static LogStyleConfig Plain { get; } = new()
        {
            UseAnsiColors = false,
            ShowLevelIcon = false,
            BracketTimestamp = true,
            TimeFormat = "yyyy-MM-dd HH:mm:ss.fff zzz",
            LevelWidth = 7,
            UseLevelFullName = true
        };

        /// <summary>
        /// コンパクト設定（最小限の情報のみ）を返す。
        /// </summary>
        public static LogStyleConfig Compact { get; } = new()
        {
            ShowElapsed = false,
            ShowThreadId = false,
            ShowLevelIcon = false,
            LevelWidth = 3,
            UseLevelFullName = false,
            BracketTimestamp = false
        };
    }
}