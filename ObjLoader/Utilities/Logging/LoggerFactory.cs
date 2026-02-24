using System.Collections.Concurrent;

namespace ObjLoader.Utilities.Logging
{
    /// <summary>ロガーインスタンスの生成・管理・グローバル有効/無効制御を行うファクトリ。</summary>
    public sealed class LoggerFactory : IDisposable
    {
        private static volatile LoggerFactory? _default;
        private static readonly object _defaultLock = new();

        /// <summary>
        /// グローバルログ無効フラグ。trueのときすべてのLogger.Log()が即座にreturnする。
        /// LoggerFactory.Disable()/Enable()で操作する。
        /// </summary>
        internal static volatile bool IsDisabled;

        private readonly ConcurrentDictionary<string, Logger> _loggers = new();
        private readonly List<ILogSink> _sinks = new();
        private readonly object _sinkLock = new();
        private volatile ILogSink _compositeSink;
        private LogLevel _globalMinimumLevel;
        private int _disposed;

        /// <param name="minimumLevel">グローバル最小ログレベル。</param>
        public LoggerFactory(LogLevel minimumLevel = LogLevel.Trace)
        {
            _globalMinimumLevel = minimumLevel;
            var defaultSink = new DebugSink();
            _sinks.Add(defaultSink);
            _compositeSink = defaultSink;
        }

        /// <summary>デフォルトファクトリ。初回アクセス時にDebugSinkで自動初期化される。</summary>
        public static LoggerFactory Default
        {
            get
            {
                if (_default != null) return _default;
                lock (_defaultLock)
                {
                    _default ??= new LoggerFactory();
                }
                return _default;
            }
        }

        /// <summary>すべてのロガーが無効化されているかどうか。</summary>
        public static bool LoggingEnabled => !IsDisabled;

        /// <summary>グローバル最小ログレベル。変更時は既存のすべてのLoggerに反映される。</summary>
        public LogLevel GlobalMinimumLevel
        {
            get => _globalMinimumLevel;
            set
            {
                _globalMinimumLevel = value;
                foreach (var kvp in _loggers)
                    kvp.Value.MinimumLevel = value;
            }
        }

        /// <summary>
        /// すべてのロギングを停止する。
        /// 既存のLogger.Log()が即座にreturnするようになり、フォーマット・シンク呼び出しは一切行われない。
        /// FileSinkなど非同期シンクのI/Oも停止するにはあわせてDispose()を呼ぶこと。
        /// </summary>
        public static void Disable()
        {
            IsDisabled = true;
        }

        /// <summary>Disable()で停止したロギングを再開する。</summary>
        public static void Enable()
        {
            IsDisabled = false;
        }

        /// <summary>シンクを追加する。チェーン呼び出し可能。</summary>
        public LoggerFactory AddSink(ILogSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(LoggerFactory));

            lock (_sinkLock)
            {
                _sinks.Add(sink);
                RebuildCompositeSink();
            }

            return this;
        }

        /// <summary>FileSinkを追加する。チェーン呼び出し可能。</summary>
        public LoggerFactory AddFileSink(string filePath, long maxFileSizeBytes = 10 * 1024 * 1024, int maxRetainedFiles = 5)
            => AddSink(new FileSink(filePath, maxFileSizeBytes, maxRetainedFiles));

        /// <summary>指定カテゴリのLoggerを取得する。同カテゴリはキャッシュされ再利用される。</summary>
        public Logger GetLogger(string category)
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(LoggerFactory));
            return _loggers.GetOrAdd(category, name => new Logger(name, _compositeSink, _globalMinimumLevel));
        }

        /// <summary>型名をカテゴリとしてLoggerを取得する。</summary>
        public Logger GetLogger<T>() => GetLogger(typeof(T).Name);

        /// <summary>デフォルトファクトリからLoggerを生成する。</summary>
        public static Logger CreateLogger(string category) => Default.GetLogger(category);

        /// <summary>デフォルトファクトリから型名カテゴリのLoggerを生成する。</summary>
        public static Logger CreateLogger<T>() => Default.GetLogger<T>();

        /// <summary>デフォルトファクトリを置き換える。</summary>
        public static void SetDefault(LoggerFactory factory)
        {
            lock (_defaultLock)
            {
                _default = factory ?? throw new ArgumentNullException(nameof(factory));
            }
        }

        /// <summary>すべてのシンクをフラッシュする。</summary>
        public void FlushAll() => _compositeSink.Flush();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            _loggers.Clear();

            lock (_sinkLock)
            {
                (_compositeSink as IDisposable)?.Dispose();

                foreach (var sink in _sinks)
                {
                    try
                    {
                        (sink as IDisposable)?.Dispose();
                    }
                    catch
                    {
                    }
                }

                _sinks.Clear();
            }
        }

        private void RebuildCompositeSink()
        {
            _compositeSink = _sinks.Count == 1 ? _sinks[0] : new CompositeSink(_sinks);
        }
    }
}