using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>Мок отметки готовности игры.</summary>
    public sealed class EditorMockGameReadinessService : EditorMockPlatformService, IGameReadinessService
    {
        public bool IsReadySent { get; private set; }

        public Task<PlatformServiceResult> MarkReadyAsync()
        {
            var result = BuildResult("Мок-отметка готовности (LoadingAPI.ready) отправлена.");
            if (result.IsSuccess)
            {
                IsReadySent = true;
            }

            return Task.FromResult(result);
        }

        /// <summary>Сбрасывает отметку (для повторной проверки сценария).</summary>
        public void Reset()
        {
            IsReadySent = false;
        }
    }
}
