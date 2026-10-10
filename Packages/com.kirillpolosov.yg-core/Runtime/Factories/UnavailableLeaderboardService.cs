using System.Collections.Generic;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>Заглушка таблиц лидеров без платформы.</summary>
    public sealed class UnavailableLeaderboardService : ILeaderboardService
    {
        public bool IsPlatformAvailable => false;

        public Task<PlatformServiceResult> SubmitScoreAsync(
            string leaderboardId,
            long score)
        {
            return Task.FromResult(
                PlatformServiceResult.Unavailable(
                    "Таблицы лидеров недоступны на этой платформе."));
        }

        public Task<PlatformServiceResult<List<LeaderboardEntry>>> GetTopAsync(
            string leaderboardId,
            int limit)
        {
            return Task.FromResult(
                PlatformServiceResult<List<LeaderboardEntry>>.Unavailable(
                    "Таблицы лидеров недоступны на этой платформе."));
        }
    }
}
