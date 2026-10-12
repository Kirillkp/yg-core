using System;
using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>
    /// Серверное время платформы. Нужно там, где подмена локальных часов даёт выгоду игроку
    /// (ежедневная награда). Если серверного времени нет, результат не Success, и игра сама решает,
    /// использовать ли запасной вариант (локальные часы UTC) и считать ли его ненадёжным.
    /// </summary>
    public interface IServerClock
    {
        /// <summary>Отдаёт ли платформа серверное время (в редакторе и вне Яндекс Игр false, кроме мока).</summary>
        bool IsPlatformAvailable { get; }

        /// <summary>Серверное время в UTC (<see cref="DateTime.Kind"/> равен Utc). Нет API или сбой: не Success.</summary>
        Task<PlatformServiceResult<DateTime>> GetUtcNowAsync();
    }
}
