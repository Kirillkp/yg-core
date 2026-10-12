using System;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>Заглушка серверного времени без платформы. Запасной вариант (локальные часы) выбирает игра.</summary>
    public sealed class UnavailableServerClock : IServerClock
    {
        public bool IsPlatformAvailable => false;

        public Task<PlatformServiceResult<DateTime>> GetUtcNowAsync()
        {
            return Task.FromResult(
                PlatformServiceResult<DateTime>.Unavailable(
                    "Серверное время недоступно на этой платформе."));
        }
    }
}
