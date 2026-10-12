using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>Заглушка оценки игры без платформы.</summary>
    public sealed class UnavailableReviewService : IReviewService
    {
        public bool IsPlatformAvailable => false;

        public Task<PlatformServiceResult<bool>> CanReviewAsync()
        {
            return Task.FromResult(
                PlatformServiceResult<bool>.Unavailable(
                    "Оценка игры недоступна на этой платформе."));
        }

        public Task<PlatformServiceResult<bool>> RequestReviewAsync()
        {
            return Task.FromResult(
                PlatformServiceResult<bool>.Unavailable(
                    "Оценка игры недоступна на этой платформе."));
        }
    }
}
