using System;

namespace YgCore.Contracts
{
    /// <summary>
    /// Жизненный цикл геймплея относительно платформы: игра сообщает платформе, когда геймплей
    /// идёт, а платформа может попросить поставить игру на паузу или снять её.
    /// </summary>
    public interface IGameplayLifecycleService
    {
        /// <summary>Платформа просит поставить на паузу или снять паузу.
        /// Игра должна превратить это в причину паузы Platform.</summary>
        event EventHandler<PlatformPauseChangedEventArgs> PlatformPauseChanged;

        bool IsPlatformAvailable { get; }

        /// <summary>Что платформа считает: идёт ли сейчас геймплей.</summary>
        bool IsGameplayActive { get; }

        /// <summary>Сообщает платформе о начале или конце геймплея.</summary>
        PlatformServiceResult SetGameplayActive(bool isActive);
    }
}
