namespace YgCore.Mocks
{
    /// <summary>Что должен вернуть мок платформы: позволяет проверить в редакторе все исходы операций.</summary>
    public enum MockPlatformScenario
    {
        /// <summary>Операции проходят успешно.</summary>
        Success = 0,

        /// <summary>Платформа отказывает (как будто игрок отменил действие).</summary>
        Denied = 1,

        /// <summary>Платформа возвращает ошибку.</summary>
        Error = 2,

        /// <summary>Платформы нет.</summary>
        PlatformUnavailable = 3
    }
}
