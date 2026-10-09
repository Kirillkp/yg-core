using System.Collections.Generic;
using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>Таблицы лидеров. Идентификаторы таблиц заводятся в консоли платформы.</summary>
    public interface ILeaderboardService
    {
        bool IsPlatformAvailable { get; }

        /// <summary>Отправляет результат игрока в таблицу.</summary>
        Task<PlatformServiceResult> SubmitScoreAsync(string leaderboardId, long score);

        /// <summary>Возвращает верхние записи таблицы, не больше <paramref name="limit"/>.</summary>
        Task<PlatformServiceResult<List<LeaderboardEntry>>> GetTopAsync(string leaderboardId, int limit);
    }
}
