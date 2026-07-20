using System.Text;

namespace AiProxyHub;

/// <summary>
/// An <see cref="ILoggerProvider"/> that writes log messages to an <see cref="TextWriter"/>.
/// The writer can be set after construction, so the UI-backed writer can be wired up at runtime.
/// </summary>
public sealed class TextWriterLoggerProvider : ILoggerProvider
{
    private TextWriter? _writer;

    /// <summary>Sets the target writer. Call once the UI is ready.</summary>
    public void SetWriter(TextWriter writer) => _writer = writer;

    public ILogger CreateLogger(string categoryName)
        => new TextWriterLogger(categoryName, this);

    public void Dispose() { }

    private sealed class TextWriterLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly TextWriterLoggerProvider _provider;

        public TextWriterLogger(string categoryName, TextWriterLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var writer = _provider._writer;
            if (writer is null) return;

            string message = formatter(state, exception);
            var sb = new StringBuilder();

            // Timestamp prefix
            sb.Append(DateTime.Now.ToString("HH:mm:ss"));
            sb.Append(' ');

            // Level prefix (abbreviated)
            sb.Append(logLevel switch
            {
                LogLevel.Trace => "[TRCE]",
                LogLevel.Debug => "[DBUG]",
                LogLevel.Information => "[INFO]",
                LogLevel.Warning => "[WARN]",
                LogLevel.Error => "[ERR!]",
                LogLevel.Critical => "[CRIT]",
                LogLevel.None => "",
                _ => "[????]"
            });

            sb.Append(' ');
            sb.Append(message);

            if (exception is not null)
            {
                sb.AppendLine();
                sb.Append("       → ");
                sb.Append(exception.GetType().Name);
                sb.Append(": ");
                sb.Append(exception.Message);
            }

            writer.WriteLine(sb.ToString());
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
            private NullScope() { }
        }
    }
}
