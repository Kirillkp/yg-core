using System;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>Заглушка жизненного цикла геймплея без платформы: пауз от платформы не приходит.</summary>
    public sealed class UnavailableGameplayLifecycleService : IGameplayLifecycleService
    {
        public event EventHandler<PlatformPauseChangedEventArgs> PlatformPauseChanged
        {
            add { }
            remove { }
        }

        public bool IsPlatformAvailable => false;

        public bool IsGameplayActive => false;

        public PlatformServiceResult SetGameplayActive(bool isActive)
        {
            return PlatformServiceResult.Unavailable(
                "Отметки геймплея недоступны на этой платформе.");
        }
    }
}
