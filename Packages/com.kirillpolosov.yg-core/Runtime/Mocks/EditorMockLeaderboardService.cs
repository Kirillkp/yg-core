using System.Collections.Generic;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок таблицы лидеров. Все таблицы делят один общий список записей (идентификатор таблицы игнорируется),
    /// записи не сортируются: они возвращаются в порядке добавления.
    /// </summary>
    public sealed class EditorMockLeaderboardService : EditorMockPlatformService, ILeaderboardService
    {
        private readonly List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

        public List<LeaderboardEntry> Entries => entries;

        public Task<PlatformServiceResult> SubmitScoreAsync(string leaderboardId, long score)
        {
            var result = BuildResult("Мок-результат отправлен.");
            if (result.IsSuccess)
            {
                entries.Add(new LeaderboardEntry("local-player", "Local Player", score, entries.Count + 1));
            }

            return Task.FromResult(result);
        }

        public Task<PlatformServiceResult<List<LeaderboardEntry>>> GetTopAsync(string leaderboardId, int limit)
        {
            var count = limit < entries.Count ? limit : entries.Count;
            var resultEntries = entries.GetRange(0, count);
            return Task.FromResult(BuildResult(resultEntries));
        }

        /// <summary>Добавляет запись «другого игрока» для проверки отображения таблицы.</summary>
        public void SeedEntry(string playerId, string displayName, long score, int rank)
        {
            entries.Add(new LeaderboardEntry(playerId, displayName, score, rank));
        }
    }
}
