namespace ObjLoader.Utilities.Logging
{
    /// <summary>
    /// 複数のILogSinkをまとめて管理する複合シンク。
    /// 個々のシンクの失敗は他のシンクに影響しない。
    /// </summary>
    public sealed class CompositeSink : ILogSink, IDisposable
    {
        private readonly ILogSink[] _sinks;
        private int _disposed;

        /// <summary>
        /// 指定されたシンク群を統合する複合シンクを生成する。
        /// </summary>
        /// <param name="sinks">統合するシンクのコレクション。</param>
        public CompositeSink(IEnumerable<ILogSink> sinks)
        {
            var list = new List<ILogSink>();
            foreach (var sink in sinks)
            {
                if (sink != null)
                    list.Add(sink);
            }
            _sinks = list.ToArray();
        }

        /// <summary>
        /// すべてのシンクにログエントリを出力する。
        /// </summary>
        /// <param name="entry">出力するログエントリ。</param>
        public void Emit(in LogEntry entry)
        {
            var sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++)
            {
                try
                {
                    sinks[i].Emit(in entry);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// すべてのシンクをフラッシュする。
        /// </summary>
        public void Flush()
        {
            var sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++)
            {
                try
                {
                    sinks[i].Flush();
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// IDisposableを実装するシンクをすべて解放する。
        /// </summary>
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;

            var sinks = _sinks;
            for (int i = 0; i < sinks.Length; i++)
            {
                try
                {
                    (sinks[i] as IDisposable)?.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}