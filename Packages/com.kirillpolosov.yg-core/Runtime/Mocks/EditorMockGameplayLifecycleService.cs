using System;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок жизненного цикла геймплея. Паузу от платформы можно «прислать» вручную
    /// вызовом <see cref="SetPlatformPaused"/>.
    /// </summary>
    public sealed class EditorMockGameplayLifecycleService : EditorMockPlatformService,
        IGameplayLifecycleService
    {
        public event EventHandler<PlatformPauseChangedEventArgs> PlatformPauseChanged;

        public bool IsGameplayActive { get; private set; }

        public PlatformServiceResult SetGameplayActive(bool isActive)
        {
            var result = BuildResult(
                isActive
                    ? "Мок: начало геймплея (GameplayAPI.start) отправлено."
                    : "Мок: конец геймплея (GameplayAPI.stop) отправлен.");
            if (result.IsSuccess)
            {
                IsGameplayActive = isActive;
            }

            return result;
        }

        /// <summary>Имитирует просьбу платформы поставить игру на паузу или снять её.</summary>
        public void SetPlatformPaused(bool isPaused)
        {
            PlatformPauseChanged?.Invoke(
                this,
                new PlatformPauseChangedEventArgs(isPaused));
        }
    }
}
