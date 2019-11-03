using System;
using Microsoft.Extensions.Logging;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Extensions
{
    /// <summary>
    /// DateTimeLogger
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <inheritdoc />
    public class DateTimeLogger<T> : ILogger<T>
    {
        private readonly ILogger _logger;

        /// <inheritdoc />
        public DateTimeLogger(ILogger logger) => _logger = logger;

        /// <inheritdoc />
        public DateTimeLogger(ILoggerFactory loggerFactory) : this(new Logger<T>(loggerFactory)) { }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            _logger.Log(logLevel, eventId, state, exception, (s, ex) => $"[{DateTime.Now:dd-MM-yyyy HH:mm:ss.fff}]: {formatter(s, ex)}");

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);
    }
}