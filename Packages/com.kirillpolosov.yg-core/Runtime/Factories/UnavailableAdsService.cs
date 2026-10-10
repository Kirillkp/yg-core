using System;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Безопасная заглушка рекламы для случая, когда платформы нет: игра работает дальше без рекламы,
    /// а любой показ сразу возвращает PlatformUnavailable.
    /// </summary>
    public sealed class UnavailableAdsService : IAdsService
    {
        // Пустые add/remove: подписка допустима, но событий не будет.
        public event EventHandler<AdPlaybackEventArgs> AdStarted
        {
            add { }
            remove { }
        }

        public event EventHandler<AdPlaybackEventArgs> AdFinished
        {
            add { }
            remove { }
        }

        public bool IsPlatformAvailable => false;

        public Task<PlatformServiceResult> ShowRewardedAsync(string placementId)
        {
            return Unavailable();
        }

        public Task<PlatformServiceResult> ShowInterstitialAsync(string placementId)
        {
            return Unavailable();
        }

        private static Task<PlatformServiceResult> Unavailable()
        {
            return Task.FromResult(
                PlatformServiceResult.Unavailable(
                    "Реклама недоступна на этой платформе."));
        }
    }
}
