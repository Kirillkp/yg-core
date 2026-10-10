using System.Collections.Generic;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>Мок аналитики: запоминает имена отправленных событий, чтобы их можно было проверить.</summary>
    public sealed class EditorMockAnalyticsService : EditorMockPlatformService, IAnalyticsService
    {
        private readonly List<string> trackedEvents = new List<string>();

        /// <summary>Имена событий, отправленных успешно, в порядке отправки.</summary>
        public List<string> TrackedEvents => trackedEvents;

        public Task<PlatformServiceResult> TrackEventAsync(string eventName, Dictionary<string, string> parameters = null)
        {
            var result = BuildResult("Мок-событие аналитики отправлено.");
            if (result.IsSuccess)
            {
                trackedEvents.Add(eventName);
            }

            return Task.FromResult(result);
        }
    }
}
