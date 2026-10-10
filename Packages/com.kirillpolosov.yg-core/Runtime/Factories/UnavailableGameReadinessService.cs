using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>Заглушка отметки готовности без платформы.</summary>
    public sealed class UnavailableGameReadinessService : IGameReadinessService
    {
        public bool IsPlatformAvailable => false;

        public bool IsReadySent => false;

        public Task<PlatformServiceResult> MarkReadyAsync()
        {
            return Task.FromResult(
                PlatformServiceResult.Unavailable(
                    "Отметка готовности недоступна на этой платформе."));
        }
    }
}
