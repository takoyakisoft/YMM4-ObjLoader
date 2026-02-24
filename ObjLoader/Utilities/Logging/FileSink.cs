using System.IO;
using System.Text;
using System.Threading.Channels;

namespace ObjLoader.Utilities.Logging
{
    /// <summary>
    /// ファイルへログを出力するシンク。
    /// 非同期バッファリング書き込み、サイズベースの自動ローテーション、世代管理を備える。
    /// </summary>
    public sealed class FileSink : ILogSink, IDisposable
    {
        private readonly string _basePath;
        private readonly long _maxFileSize;
        private readonly int _maxRetainedFiles;
        private readonly Channel<LogEntry> _channel;
        private readonly CancellationTokenSource _cts;
        private readonly Task _writerTask;
        private StreamWriter? _writer;
        private long _currentFileSize;
        private int _disposed;

        /// <summary>
        /// 使用するスタイル設定を取得または設定する。
        /// デフォルトは<see cref="LogStyleConfig.Plain"/>（ANSIなし・ファイル向け）。
        /// </summary>
        public LogStyleConfig Style { get; set; } = LogStyleConfig.Plain;

        /// <summary>
        /// FileSinkのインスタンスを生成する。
        /// </summary>
        /// <param name="filePath">ログファイルのベースパス。</param>
        /// <param name="maxFileSizeBytes">ログファイルの最大サイズ（バイト）。デフォルトは10MB。</param>
        /// <param name="maxRetainedFiles">保持する最大ファイル数。デフォルトは5。</param>
        public FileSink(string filePath, long maxFileSizeBytes = 10 * 1024 * 1024, int maxRetainedFiles = 5)
        {
            _basePath = Path.GetFullPath(filePath);
            _maxFileSize = maxFileSizeBytes > 0 ? maxFileSizeBytes : 10 * 1024 * 1024;
            _maxRetainedFiles = maxRetainedFiles > 0 ? maxRetainedFiles : 5;

            _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(4096)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

            _cts = new CancellationTokenSource();
            _writerTask = Task.Run(() => ProcessQueueAsync(_cts.Token));
        }

        /// <summary>
        /// ログエントリを書き込みキューに追加する。
        /// </summary>
        /// <param name="entry">出力するログエントリ。</param>
        public void Emit(in LogEntry entry)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            _channel.Writer.TryWrite(entry);
        }

        /// <summary>
        /// バッファされたログをフラッシュする。
        /// </summary>
        public void Flush()
        {
            if (Volatile.Read(ref _disposed) != 0) return;

            SpinWait spin = default;
            while (_channel.Reader.Count > 0 && Volatile.Read(ref _disposed) == 0)
            {
                spin.SpinOnce();
            }

            try
            {
                _writer?.Flush();
            }
            catch
            {
            }
        }

        /// <summary>
        /// リソースを解放し、すべてのバッファをフラッシュする。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            _channel.Writer.TryComplete();

            try
            {
                _cts.CancelAfter(TimeSpan.FromSeconds(5));
                _writerTask.Wait(TimeSpan.FromSeconds(6));
            }
            catch
            {
            }

            CloseWriter();
            _cts.Dispose();
        }

        private async Task ProcessQueueAsync(CancellationToken ct)
        {
            try
            {
                await foreach (var entry in _channel.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        WriteEntry(in entry);
                    }
                    catch
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }

            DrainRemaining();
        }

        private void DrainRemaining()
        {
            while (_channel.Reader.TryRead(out var entry))
            {
                try
                {
                    WriteEntry(in entry);
                }
                catch
                {
                }
            }

            try
            {
                _writer?.Flush();
            }
            catch
            {
            }
        }

        private void WriteEntry(in LogEntry entry)
        {
            EnsureWriter();
            if (_writer == null) return;

            var line = LogFormatter.Format(in entry, Style);
            _writer.WriteLine(line);
            _currentFileSize += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;

            if (_currentFileSize >= _maxFileSize)
            {
                RotateFile();
            }
        }

        private void EnsureWriter()
        {
            if (_writer != null) return;

            try
            {
                var dir = Path.GetDirectoryName(_basePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var stream = new FileStream(_basePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096);
                _writer = new StreamWriter(stream, new UTF8Encoding(false), 4096)
                {
                    AutoFlush = false
                };

                try
                {
                    _currentFileSize = new FileInfo(_basePath).Length;
                }
                catch
                {
                    _currentFileSize = 0;
                }
            }
            catch
            {
                _writer = null;
                _currentFileSize = 0;
            }
        }

        private void RotateFile()
        {
            CloseWriter();

            try
            {
                var dir = Path.GetDirectoryName(_basePath) ?? ".";
                var baseName = Path.GetFileNameWithoutExtension(_basePath);
                var ext = Path.GetExtension(_basePath);

                for (int i = _maxRetainedFiles - 1; i >= 1; i--)
                {
                    var src = Path.Combine(dir, $"{baseName}.{i}{ext}");
                    var dst = Path.Combine(dir, $"{baseName}.{i + 1}{ext}");

                    if (File.Exists(dst))
                    {
                        File.Delete(dst);
                    }

                    if (File.Exists(src))
                    {
                        File.Move(src, dst);
                    }
                }

                var first = Path.Combine(dir, $"{baseName}.1{ext}");
                if (File.Exists(first))
                {
                    File.Delete(first);
                }

                if (File.Exists(_basePath))
                {
                    File.Move(_basePath, first);
                }

                CleanOldFiles(dir, baseName, ext);
            }
            catch
            {
            }

            _currentFileSize = 0;
        }

        private void CleanOldFiles(string dir, string baseName, string ext)
        {
            for (int i = _maxRetainedFiles + 1; i <= _maxRetainedFiles + 10; i++)
            {
                var old = Path.Combine(dir, $"{baseName}.{i}{ext}");
                if (!File.Exists(old)) break;

                try
                {
                    File.Delete(old);
                }
                catch
                {
                }
            }
        }

        private void CloseWriter()
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
            }
            catch
            {
            }

            _writer = null;
        }
    }
}