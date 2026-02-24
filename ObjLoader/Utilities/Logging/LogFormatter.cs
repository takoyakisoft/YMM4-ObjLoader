using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;

namespace ObjLoader.Utilities.Logging
{
    /// <summary>
    /// ログエントリを可読文字列に変換するフォーマッター。
    /// ThreadStaticなStringBuilderプールとstackallocによるゼロアロケーション数値整形を使用する。
    /// </summary>
    public static class LogFormatter
    {
        private const string Reset = "\x1b[0m";
        private const string Bold = "\x1b[1m";
        private const string Dim = "\x1b[2m";
        private const string Blink = "\x1b[5m";
        private const string FgRed = "\x1b[31m";
        private const string FgCyan = "\x1b[36m";
        private const string FgGray = "\x1b[90m";
        private const string FgBrightRed = "\x1b[91m";
        private const string FgBrightGreen = "\x1b[92m";
        private const string FgBrightYellow = "\x1b[93m";
        private const string FgBrightBlue = "\x1b[94m";
        private const string FgBrightMagenta = "\x1b[95m";
        private const string FgBrightCyan = "\x1b[96m";
        private const string FgBrightWhite = "\x1b[97m";
        private const string BgRed = "\x1b[41m";

        [ThreadStatic]
        private static StringBuilder? _threadSb;

        private static readonly string[] _levelColorCache =
        {
            FgGray,
            FgBrightCyan,
            FgBrightBlue,
            FgBrightGreen,
            FgBrightYellow,
            FgBrightRed,
            BgRed + FgBrightWhite,
            FgBrightMagenta + Blink
        };

        private static readonly ConcurrentDictionary<(int, bool), string[]> _labelCache = new();

        private static StringBuilder AcquireSb()
        {
            var sb = _threadSb;
            _threadSb = null;
            if (sb is null) return new StringBuilder(512);
            sb.Clear();
            return sb;
        }

        private static string ReleaseSb(StringBuilder sb)
        {
            var result = sb.ToString();
            if (sb.Capacity <= 4096) _threadSb = sb;
            return result;
        }

        /// <summary>
        /// ログエントリをANSI装飾付き文字列に変換する（デフォルト設定使用）。
        /// </summary>
        /// <param name="entry">フォーマットするログエントリ。</param>
        /// <returns>ANSI装飾付きのフォーマット済み文字列。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FormatStyled(in LogEntry entry)
            => Format(in entry, LogStyleConfig.Default);

        /// <summary>
        /// ログエントリをプレーンテキスト文字列に変換する（プレーン設定使用）。
        /// </summary>
        /// <param name="entry">フォーマットするログエントリ。</param>
        /// <returns>プレーンテキストのフォーマット済み文字列。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string FormatPlain(in LogEntry entry)
            => Format(in entry, LogStyleConfig.Plain);

        /// <summary>
        /// ログエントリを指定スタイル設定に従って文字列に変換する。
        /// <para>タイムスタンプ・経過時間・スレッドIDの整形はstackallocを使用しヒープアロケーションなし。</para>
        /// </summary>
        /// <param name="entry">フォーマットするログエントリ。</param>
        /// <param name="config">適用するスタイル設定。</param>
        /// <returns>フォーマット済み文字列。</returns>
        public static string Format(in LogEntry entry, LogStyleConfig config)
        {
            config ??= LogStyleConfig.Default;
            var sb = AcquireSb();
            var ansi = config.UseAnsiColors;

            if (config.TimeFormat != null)
            {
                Span<char> tsBuf = stackalloc char[48];
                if (entry.Timestamp.TryFormat(tsBuf, out int tsW, config.TimeFormat))
                {
                    if (ansi) sb.Append(Dim);
                    if (config.BracketTimestamp) sb.Append('[');
                    sb.Append(tsBuf[..tsW]);
                    if (config.BracketTimestamp) sb.Append(']');
                    if (ansi) sb.Append(Reset);
                    sb.Append(' ');
                }
            }

            if (config.ShowElapsed)
            {
                Span<char> elBuf = stackalloc char[16];
                entry.Elapsed.TotalSeconds.TryFormat(elBuf, out int elW, "F3");
                if (ansi) sb.Append(Dim);
                if (config.BracketTimestamp) sb.Append('[');
                sb.Append('+');
                sb.Append(elBuf[..elW]);
                sb.Append('s');
                if (config.BracketTimestamp) sb.Append(']');
                if (ansi) sb.Append(Reset);
                sb.Append(' ');
            }

            if (config.ShowLevel)
            {
                AppendLevelField(sb, entry.Level, config);
                sb.Append(' ');
            }

            if (config.ShowCategory && !string.IsNullOrEmpty(entry.Category))
            {
                if (ansi) sb.Append(FgGray);
                sb.Append('[');
                sb.Append(entry.Category);
                sb.Append(']');
                if (ansi) sb.Append(Reset);
                sb.Append(' ');
            }

            if (ansi)
                AppendStyledMessage(sb, entry.Level, entry.Message);
            else
                sb.Append(entry.Message);

            if (config.ShowThreadId)
            {
                Span<char> tidBuf = stackalloc char[8];
                entry.ManagedThreadId.TryFormat(tidBuf, out int tidW);
                sb.Append(' ');
                if (ansi) sb.Append(Dim);
                sb.Append("(T:");
                sb.Append(tidBuf[..tidW]);
                sb.Append(')');
                if (ansi) sb.Append(Reset);
            }

            if (entry.Exception != null && config.ShowExceptionDetail)
            {
                sb.AppendLine();
                if (ansi) sb.Append(FgRed);
                sb.Append("  ╰─ ");
                sb.Append(entry.Exception.GetType().Name);
                sb.Append(": ");
                sb.Append(entry.Exception.Message);
                if (ansi) sb.Append(Reset);

                if (entry.Exception.StackTrace != null)
                {
                    sb.AppendLine();
                    if (ansi) { sb.Append(Dim); sb.Append(FgRed); }
                    foreach (var line in entry.Exception.StackTrace.Split('\n'))
                    {
                        var trimmed = line.TrimEnd('\r');
                        if (string.IsNullOrWhiteSpace(trimmed)) continue;
                        sb.Append("     ");
                        sb.AppendLine(trimmed.TrimStart());
                    }
                    if (ansi) sb.Append(Reset);
                }
            }
            else if (entry.Exception != null)
            {
                sb.Append(" [");
                sb.Append(entry.Exception.GetType().Name);
                sb.Append(": ");
                sb.Append(entry.Exception.Message);
                sb.Append(']');
            }

            return ReleaseSb(sb);
        }

        /// <summary>
        /// 起動バナーを生成する。stackallocにより数値・タイムスタンプのヒープアロケーションなし。
        /// </summary>
        /// <param name="appName">アプリケーション名。</param>
        /// <param name="version">バージョン文字列。</param>
        /// <returns>ANSI装飾付きの起動バナー文字列。</returns>
        public static string FormatBanner(string appName, string version)
        {
            var now = DateTimeOffset.Now;
            var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            var os = Environment.OSVersion.ToString();
            var contentWidth = Math.Max(appName.Length + version.Length + 5, 52);

            var sb = AcquireSb();
            sb.AppendLine();
            sb.Append(FgBrightCyan);
            sb.Append(Bold);
            sb.Append('╔');
            sb.Append('═', contentWidth + 2);
            sb.Append('╗');
            sb.AppendLine();

            sb.Append("║ ");
            sb.Append(' ', contentWidth);
            sb.Append(" ║");
            sb.AppendLine();

            var titleLine = $"{appName}  v{version}";
            var titlePadLeft = (contentWidth - titleLine.Length) / 2;
            var titlePadRight = contentWidth - titleLine.Length - titlePadLeft;
            sb.Append("║ ");
            sb.Append(FgBrightWhite);
            sb.Append(' ', titlePadLeft);
            sb.Append(titleLine);
            sb.Append(' ', titlePadRight);
            sb.Append(FgBrightCyan);
            sb.Append(" ║");
            sb.AppendLine();

            sb.Append("║ ");
            sb.Append(' ', contentWidth);
            sb.Append(" ║");
            sb.AppendLine();

            sb.Append("╠");
            sb.Append('─', contentWidth + 2);
            sb.Append("╣");
            sb.AppendLine();

            AppendBannerField(sb, "Runtime", runtime, contentWidth);
            AppendBannerField(sb, "OS", os, contentWidth);

            Span<char> numBuf = stackalloc char[16];
            Environment.ProcessId.TryFormat(numBuf, out int pidW);
            AppendBannerFieldSpan(sb, "PID", numBuf[..pidW], contentWidth);

            Environment.ProcessorCount.TryFormat(numBuf, out int coresW);
            AppendBannerFieldSpan(sb, "Cores", numBuf[..coresW], contentWidth);

            Span<char> tsBuf = stackalloc char[48];
            now.TryFormat(tsBuf, out int tsW, "yyyy-MM-dd HH:mm:ss.fff zzz");
            AppendBannerFieldSpan(sb, "Started", tsBuf[..tsW], contentWidth);

            sb.Append("║ ");
            sb.Append(' ', contentWidth);
            sb.Append(" ║");
            sb.AppendLine();

            sb.Append('╚');
            sb.Append('═', contentWidth + 2);
            sb.Append('╝');
            sb.Append(Reset);
            sb.AppendLine();

            return ReleaseSb(sb);
        }

        /// <summary>
        /// アラートフレームを生成する。
        /// </summary>
        /// <param name="title">アラートのタイトル。</param>
        /// <param name="message">アラートの詳細メッセージ。</param>
        /// <param name="level">アラートのログレベル。</param>
        /// <returns>ANSI装飾付きのアラートフレーム文字列。</returns>
        public static string FormatAlert(string title, string message, LogLevel level)
        {
            var maxLen = Math.Max(title.Length, message.Length);
            var width = Math.Max(maxLen + 4, 40);
            var color = GetLevelColor(level);
            var icon = GetLevelIcon(level);

            var sb = AcquireSb();
            sb.AppendLine();
            sb.Append(color);
            sb.Append(Bold);
            sb.Append('┏');
            sb.Append('━', width);
            sb.Append('┓');
            sb.AppendLine();

            var headerText = $" {icon} {title} ";
            var hPadRight = width - headerText.Length;
            if (hPadRight < 0) hPadRight = 0;
            sb.Append("┃");
            sb.Append(headerText);
            sb.Append(' ', hPadRight);
            sb.Append("┃");
            sb.AppendLine();

            sb.Append("┠");
            sb.Append('─', width);
            sb.Append("┨");
            sb.AppendLine();

            foreach (var line in message.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                var bodyText = $" {trimmed} ";
                var bPadRight = width - bodyText.Length;
                if (bPadRight < 0) bPadRight = 0;
                sb.Append("┃");
                sb.Append(bodyText);
                sb.Append(' ', bPadRight);
                sb.Append("┃");
                sb.AppendLine();
            }

            sb.Append("┗");
            sb.Append('━', width);
            sb.Append("┛");
            sb.Append(Reset);
            sb.AppendLine();

            return ReleaseSb(sb);
        }

        /// <summary>
        /// スコープの開始行を生成する。stackallocでタイムスタンプアロケーションなし。
        /// </summary>
        /// <param name="scopeName">スコープ名。</param>
        /// <param name="timestamp">開始時刻。</param>
        /// <returns>ANSI装飾付きのスコープ開始文字列。</returns>
        public static string FormatScopeBegin(string scopeName, DateTimeOffset timestamp)
        {
            var sb = AcquireSb();
            sb.Append(FgBrightCyan);
            sb.Append("┌─── ");
            sb.Append(Bold);
            sb.Append(scopeName);
            sb.Append(Reset);
            sb.Append(FgBrightCyan);
            sb.Append(" ───");
            sb.Append(Reset);
            sb.Append(Dim);
            sb.Append(" [");
            Span<char> tsBuf = stackalloc char[16];
            timestamp.TryFormat(tsBuf, out int tsW, "HH:mm:ss.fff");
            sb.Append(tsBuf[..tsW]);
            sb.Append(']');
            sb.Append(Reset);
            return ReleaseSb(sb);
        }

        /// <summary>
        /// スコープの終了行を生成する。
        /// </summary>
        /// <param name="scopeName">スコープ名。</param>
        /// <param name="elapsed">スコープの所要時間。</param>
        /// <returns>ANSI装飾付きのスコープ終了文字列。</returns>
        public static string FormatScopeEnd(string scopeName, TimeSpan elapsed)
        {
            var sb = AcquireSb();
            sb.Append(FgBrightCyan);
            sb.Append("└─── ");
            sb.Append(Bold);
            sb.Append(scopeName);
            sb.Append(Reset);
            sb.Append(FgBrightCyan);
            sb.Append(" ─── ");
            sb.Append(Reset);
            AppendElapsedHuman(sb, elapsed);
            return ReleaseSb(sb);
        }

        /// <summary>
        /// 経過時間を人間が読みやすい形式に変換する。
        /// </summary>
        /// <param name="elapsed">経過時間。</param>
        /// <returns>読みやすいフォーマット。</returns>
        public static string FormatElapsedHuman(TimeSpan elapsed)
        {
            var sb = AcquireSb();
            AppendElapsedHuman(sb, elapsed);
            return ReleaseSb(sb);
        }

        private static void AppendElapsedHuman(StringBuilder sb, TimeSpan elapsed)
        {
            Span<char> buf = stackalloc char[16];
            if (elapsed.TotalMilliseconds < 1.0)
            {
                sb.Append(FgBrightGreen);
                sb.Append(Bold);
                elapsed.TotalMicroseconds.TryFormat(buf, out int w, "F0");
                sb.Append(buf[..w]);
                sb.Append("μs");
            }
            else if (elapsed.TotalSeconds < 1.0)
            {
                sb.Append(FgBrightGreen);
                sb.Append(Bold);
                elapsed.TotalMilliseconds.TryFormat(buf, out int w, "F1");
                sb.Append(buf[..w]);
                sb.Append("ms");
            }
            else if (elapsed.TotalMinutes < 1.0)
            {
                var color = elapsed.TotalSeconds < 5 ? FgBrightGreen :
                            elapsed.TotalSeconds < 30 ? FgBrightYellow : FgBrightRed;
                sb.Append(color);
                sb.Append(Bold);
                elapsed.TotalSeconds.TryFormat(buf, out int w, "F2");
                sb.Append(buf[..w]);
                sb.Append('s');
            }
            else
            {
                sb.Append(FgBrightRed);
                sb.Append(Bold);
                elapsed.Minutes.TryFormat(buf, out int mW);
                sb.Append(buf[..mW]);
                sb.Append("m ");
                elapsed.Seconds.TryFormat(buf, out int sW);
                sb.Append(buf[..sW]);
                sb.Append('s');
            }
            sb.Append(Reset);
        }

        private static void AppendLevelField(StringBuilder sb, LogLevel level, LogStyleConfig config)
        {
            var ansi = config.UseAnsiColors;
            var useFullName = config.UseLevelFullName && config.LevelWidth != 3;
            var label = GetCachedLabel(level, config.LevelWidth, useFullName);

            if (ansi) sb.Append(GetLevelColor(level));
            if (ansi) sb.Append(Bold);

            if (config.ShowLevelIcon)
            {
                sb.Append(GetLevelIcon(level));
                sb.Append(' ');
            }

            sb.Append('[');
            sb.Append(label);
            sb.Append(']');

            if (ansi) sb.Append(Reset);
        }

        private static string GetCachedLabel(LogLevel level, int width, bool fullName)
        {
            var labels = _labelCache.GetOrAdd((width, fullName), static key =>
            {
                var arr = new string[8];
                for (int i = 0; i < 8; i++)
                {
                    var raw = key.Item2 ? GetLevelName((LogLevel)i) : GetLevelTag((LogLevel)i);
                    arr[i] = key.Item1 > 0 && raw.Length < key.Item1 ? raw.PadRight(key.Item1) : raw;
                }
                return arr;
            });
            var idx = (int)level;
            return (uint)idx < (uint)labels.Length ? labels[idx] : "???";
        }

        private static void AppendStyledMessage(StringBuilder sb, LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Trace:
                    sb.Append(Dim);
                    sb.Append(message);
                    sb.Append(Reset);
                    break;
                case LogLevel.Debug:
                    sb.Append(FgCyan);
                    sb.Append(message);
                    sb.Append(Reset);
                    break;
                case LogLevel.Warning:
                    sb.Append(FgBrightYellow);
                    sb.Append(message);
                    sb.Append(Reset);
                    break;
                case LogLevel.Error:
                    sb.Append(FgBrightRed);
                    sb.Append(Bold);
                    sb.Append(message);
                    sb.Append(Reset);
                    break;
                case LogLevel.Fatal:
                    sb.Append(BgRed);
                    sb.Append(FgBrightWhite);
                    sb.Append(Bold);
                    sb.Append(' ');
                    sb.Append(message);
                    sb.Append(' ');
                    sb.Append(Reset);
                    break;
                case LogLevel.Alert:
                    sb.Append(FgBrightMagenta);
                    sb.Append(Bold);
                    sb.Append(message);
                    sb.Append(Reset);
                    break;
                default:
                    sb.Append(message);
                    break;
            }
        }

        private static void AppendBannerField(StringBuilder sb, string label, string value, int contentWidth)
        {
            sb.Append("║ ");
            sb.Append(FgGray);
            sb.Append("  ");
            sb.Append(label);
            sb.Append(": ");
            sb.Append(FgBrightWhite);
            sb.Append(value);
            var remaining = contentWidth - (label.Length + value.Length + 4);
            if (remaining > 0) sb.Append(' ', remaining);
            sb.Append(FgBrightCyan);
            sb.Append(Bold);
            sb.Append(" ║");
            sb.AppendLine();
        }

        private static void AppendBannerFieldSpan(StringBuilder sb, string label, ReadOnlySpan<char> value, int contentWidth)
        {
            sb.Append("║ ");
            sb.Append(FgGray);
            sb.Append("  ");
            sb.Append(label);
            sb.Append(": ");
            sb.Append(FgBrightWhite);
            sb.Append(value);
            var remaining = contentWidth - (label.Length + value.Length + 4);
            if (remaining > 0) sb.Append(' ', remaining);
            sb.Append(FgBrightCyan);
            sb.Append(Bold);
            sb.Append(" ║");
            sb.AppendLine();
        }

        internal static string GetLevelTag(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Success => "SUC",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Fatal => "FTL",
            LogLevel.Alert => "ALT",
            _ => "???"
        };

        internal static string GetLevelName(LogLevel level) => level switch
        {
            LogLevel.Trace => "Trace",
            LogLevel.Debug => "Debug",
            LogLevel.Info => "Info",
            LogLevel.Success => "Success",
            LogLevel.Warning => "Warning",
            LogLevel.Error => "Error",
            LogLevel.Fatal => "Fatal",
            LogLevel.Alert => "Alert",
            _ => "???"
        };

        internal static string GetLevelIcon(LogLevel level) => level switch
        {
            LogLevel.Trace => "●",
            LogLevel.Debug => "◆",
            LogLevel.Info => "✦",
            LogLevel.Success => "✔",
            LogLevel.Warning => "⚠",
            LogLevel.Error => "✖",
            LogLevel.Fatal => "☠",
            LogLevel.Alert => "🔔",
            _ => "?"
        };

        internal static string GetLevelColor(LogLevel level)
        {
            var idx = (int)level;
            return (uint)idx < (uint)_levelColorCache.Length ? _levelColorCache[idx] : Reset;
        }
    }
}