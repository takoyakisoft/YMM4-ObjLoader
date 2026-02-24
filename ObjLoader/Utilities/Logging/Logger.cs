using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ObjLoader.Utilities.Logging
{
    /// <summary>カテゴリ別ロガー。ログレベルフィルタリング・スコープ計測・アラート出力を提供する。</summary>
    public sealed class Logger
    {
        private static readonly Stopwatch _globalStopwatch = Stopwatch.StartNew();
        private readonly string _category;
        private readonly ILogSink _sink;
        private LogLevel _minimumLevel;

        internal Logger(string category, ILogSink sink, LogLevel minimumLevel)
        {
            _category = category ?? string.Empty;
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _minimumLevel = minimumLevel;
        }

        /// <summary>現在の最小ログレベル。</summary>
        public LogLevel MinimumLevel
        {
            get => _minimumLevel;
            set => _minimumLevel = value;
        }

        /// <summary>指定レベルが出力対象かどうかを返す。グローバル無効化も考慮する。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEnabled(LogLevel level) => !LoggerFactory.IsDisabled && level >= _minimumLevel;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Trace(string message) => Log(LogLevel.Trace, message, null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Debug(string message) => Log(LogLevel.Debug, message, null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Info(string message) => Log(LogLevel.Info, message, null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Success(string message) => Log(LogLevel.Success, message, null);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Warning(string message, Exception? exception = null) => Log(LogLevel.Warning, message, exception);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Fatal(string message, Exception? exception = null) => Log(LogLevel.Fatal, message, exception);

        /// <summary>指定レベルでログを出力する。</summary>
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (LoggerFactory.IsDisabled || level < _minimumLevel) return;

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Elapsed = _globalStopwatch.Elapsed,
                Level = level,
                Category = _category,
                Message = message,
                Exception = exception,
                ManagedThreadId = Environment.CurrentManagedThreadId
            };

            _sink.Emit(in entry);
        }

        /// <summary>視覚的に目立つアラートフレームを出力する。</summary>
        public void Alert(string title, string message, LogLevel level = LogLevel.Alert)
        {
            if (LoggerFactory.IsDisabled || level < _minimumLevel) return;

            var alertText = LogFormatter.FormatAlert(title, message, level);

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Elapsed = _globalStopwatch.Elapsed,
                Level = level,
                Category = _category,
                Message = alertText,
                ManagedThreadId = Environment.CurrentManagedThreadId
            };

            _sink.Emit(in entry);
        }

        /// <summary>パフォーマンス計測付きスコープを開始する。usingで囲むと終了時に所要時間を自動出力する。</summary>
        public LogScope BeginScope(string scopeName) => new(this, scopeName);

        /// <summary>起動バナーを出力する。</summary>
        public void Banner(string appName, string version)
        {
            if (LoggerFactory.IsDisabled) return;

            var banner = LogFormatter.FormatBanner(appName, version);
            System.Diagnostics.Debug.Write(banner);

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Elapsed = _globalStopwatch.Elapsed,
                Level = LogLevel.Info,
                Category = _category,
                Message = $"{appName} v{version} started",
                ManagedThreadId = Environment.CurrentManagedThreadId
            };

            _sink.Emit(in entry);
        }

        /// <summary>Disposeで所要時間を自動出力するスコープ。</summary>
        public readonly struct LogScope : IDisposable
        {
            private readonly Logger _logger;
            private readonly string _scopeName;
            private readonly DateTimeOffset _startTime;
            private readonly long _startTicks;

            internal LogScope(Logger logger, string scopeName)
            {
                _logger = logger;
                _scopeName = scopeName;
                _startTime = DateTimeOffset.Now;
                _startTicks = Stopwatch.GetTimestamp();

                if (!LoggerFactory.IsDisabled)
                    System.Diagnostics.Debug.WriteLine(LogFormatter.FormatScopeBegin(scopeName, _startTime));
            }

            public void Dispose()
            {
                if (LoggerFactory.IsDisabled) return;

                var elapsed = Stopwatch.GetElapsedTime(_startTicks);
                System.Diagnostics.Debug.WriteLine(LogFormatter.FormatScopeEnd(_scopeName, elapsed));

                _logger.Log(
                    elapsed.TotalSeconds > 5 ? LogLevel.Warning : LogLevel.Debug,
                    $"{_scopeName} completed in {elapsed.TotalMilliseconds:F1}ms");
            }
        }
    }

    /// <summary>型名を自動カテゴリとするロガーのスタティックアクセサ。</summary>
    /// <typeparam name="T">カテゴリとなる型。</typeparam>
    public static class Logger<T>
    {
        private static Logger? _instance;

        /// <summary>型Tに対応するLoggerインスタンス。</summary>
        public static Logger Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var created = LoggerFactory.CreateLogger(typeof(T).Name);
                Interlocked.CompareExchange(ref _instance, created, null);
                return _instance;
            }
        }

        internal static void Reset() => _instance = null;
    }
}