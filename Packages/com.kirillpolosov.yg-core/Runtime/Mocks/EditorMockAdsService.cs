using System;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок рекламы: показ «проходит» мгновенно. При успехе шлёт события начала и конца,
    /// как настоящая реклама, чтобы игра могла ставить паузу.
    /// </summary>
    public sealed class EditorMockAdsService : EditorMockPlatformService, IAdsService
    {
        public event EventHandler<AdPlaybackEventArgs> AdStarted;

        public event EventHandler<AdPlaybackEventArgs> AdFinished;

        public Task<PlatformServiceResult> ShowRewardedAsync(string placementId)
        {
            return ShowAsync(AdPlacementKind.Rewarded, placementId);
        }

        public Task<PlatformServiceResult> ShowInterstitialAsync(string placementId)
        {
            return ShowAsync(AdPlacementKind.Interstitial, placementId);
        }

        private Task<PlatformServiceResult> ShowAsync(AdPlacementKind kind, string placementId)
        {
            var args = new AdPlaybackEventArgs(kind, placementId);
            var result = BuildResult($"Мок-реклама ({kind}) завершена.");

            // События только при успехе: при отказе или ошибке реклама «не открывалась».
            if (result.IsSuccess)
            {
                AdStarted?.Invoke(this, args);
                AdFinished?.Invoke(this, args);
            }

            return Task.FromResult(result);
        }
    }
}
