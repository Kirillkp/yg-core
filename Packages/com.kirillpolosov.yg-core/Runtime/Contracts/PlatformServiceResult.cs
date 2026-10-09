namespace YgCore.Contracts
{
    /// <summary>
    /// Результат операции с платформой без значения. Исключения для обычных отказов
    /// не используются: вызывающий код проверяет <see cref="Status"/> или <see cref="IsSuccess"/>.
    /// </summary>
    public class PlatformServiceResult
    {
        public PlatformServiceResult(PlatformServiceStatus status, string message = null)
        {
            Status = status;
            Message = message;
        }

        public PlatformServiceStatus Status { get; }

        /// <summary>Пояснение для логов. Игроку не показывается.</summary>
        public string Message { get; }

        public bool IsSuccess => Status == PlatformServiceStatus.Success;

        public static PlatformServiceResult Success(string message = null)
        {
            return new PlatformServiceResult(PlatformServiceStatus.Success, message);
        }

        public static PlatformServiceResult Denied(string message = null)
        {
            return new PlatformServiceResult(PlatformServiceStatus.Denied, message);
        }

        public static PlatformServiceResult Error(string message = null)
        {
            return new PlatformServiceResult(PlatformServiceStatus.Error, message);
        }

        public static PlatformServiceResult Unavailable(string message = null)
        {
            return new PlatformServiceResult(PlatformServiceStatus.PlatformUnavailable, message);
        }
    }

    /// <summary>
    /// Результат операции со значением. Значение имеет смысл только при успехе,
    /// при остальных статусах оно равно значению по умолчанию.
    /// </summary>
    public sealed class PlatformServiceResult<T> : PlatformServiceResult
    {
        public PlatformServiceResult(PlatformServiceStatus status, T value = default(T), string message = null)
            : base(status, message)
        {
            Value = value;
        }

        public T Value { get; }

        public static PlatformServiceResult<T> Success(T value, string message = null)
        {
            return new PlatformServiceResult<T>(PlatformServiceStatus.Success, value, message);
        }

        // new: скрываем одноимённые методы базового класса, чтобы фабрики возвращали типизированный результат.
        public static new PlatformServiceResult<T> Denied(string message = null)
        {
            return new PlatformServiceResult<T>(PlatformServiceStatus.Denied, default(T), message);
        }

        public static new PlatformServiceResult<T> Error(string message = null)
        {
            return new PlatformServiceResult<T>(PlatformServiceStatus.Error, default(T), message);
        }

        public static new PlatformServiceResult<T> Unavailable(string message = null)
        {
            return new PlatformServiceResult<T>(PlatformServiceStatus.PlatformUnavailable, default(T), message);
        }
    }
}
