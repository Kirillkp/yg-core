namespace YgCore.Contracts
{
    /// <summary>Итог операции с платформой.</summary>
    public enum PlatformServiceStatus
    {
        /// <summary>Операция выполнена.</summary>
        Success = 0,

        /// <summary>Платформа или игрок отказали (например, игрок закрыл рекламу или окно покупки).</summary>
        Denied = 1,

        /// <summary>Операция не удалась из-за ошибки.</summary>
        Error = 2,

        /// <summary>Платформы нет: редактор, локальный запуск или отключённый модуль.</summary>
        PlatformUnavailable = 3
    }
}
