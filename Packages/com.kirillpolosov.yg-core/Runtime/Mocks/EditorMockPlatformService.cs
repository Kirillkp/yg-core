using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Общая основа всех моков: хранит сценарий и доступность «платформы» и собирает из них результат операции.
    /// Задайте <see cref="Scenario"/>, и мок будет возвращать нужный исход для любых своих операций.
    /// </summary>
    public abstract class EditorMockPlatformService
    {
        /// <summary>Есть ли «платформа». Если false, все операции вернут PlatformUnavailable.</summary>
        public bool IsPlatformAvailable { get; set; } = true;

        /// <summary>Исход, который вернут операции мока.</summary>
        public MockPlatformScenario Scenario { get; set; } = MockPlatformScenario.Success;

        protected PlatformServiceResult BuildResult(string successMessage = null)
        {
            if (!IsPlatformAvailable || Scenario == MockPlatformScenario.PlatformUnavailable)
            {
                return PlatformServiceResult.Unavailable("Мок платформы недоступен.");
            }

            switch (Scenario)
            {
                case MockPlatformScenario.Success:
                    return PlatformServiceResult.Success(successMessage);
                case MockPlatformScenario.Denied:
                    return PlatformServiceResult.Denied("Мок платформы отклонил запрос.");
                case MockPlatformScenario.Error:
                    return PlatformServiceResult.Error("Мок платформы вернул ошибку.");
                default:
                    return PlatformServiceResult.Unavailable("Мок платформы недоступен.");
            }
        }

        protected PlatformServiceResult<T> BuildResult<T>(T successValue, string successMessage = null)
        {
            if (!IsPlatformAvailable || Scenario == MockPlatformScenario.PlatformUnavailable)
            {
                return PlatformServiceResult<T>.Unavailable("Мок платформы недоступен.");
            }

            switch (Scenario)
            {
                case MockPlatformScenario.Success:
                    return PlatformServiceResult<T>.Success(successValue, successMessage);
                case MockPlatformScenario.Denied:
                    return PlatformServiceResult<T>.Denied("Мок платформы отклонил запрос.");
                case MockPlatformScenario.Error:
                    return PlatformServiceResult<T>.Error("Мок платформы вернул ошибку.");
                default:
                    return PlatformServiceResult<T>.Unavailable("Мок платформы недоступен.");
            }
        }
    }
}
