using System;
using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>
    /// Реклама. Во время показа игра должна быть на паузе (причина Ad), а звук приглушён:
    /// для этого есть события <see cref="AdStarted"/> и <see cref="AdFinished"/>.
    /// </summary>
    public interface IAdsService
    {
        /// <summary>Реклама открылась.</summary>
        event EventHandler<AdPlaybackEventArgs> AdStarted;

        /// <summary>Реклама закончилась. Приходит только если реклама перед этим открывалась
        /// (в том числе когда после открытия случилась ошибка).</summary>
        event EventHandler<AdPlaybackEventArgs> AdFinished;

        /// <summary>Есть ли реальная платформа. В редакторе и при заглушке будет false.</summary>
        bool IsPlatformAvailable { get; }

        /// <summary>
        /// Показывает рекламу с наградой.
        /// Success: реклама закрыта и награда засчитана платформой, награду можно выдавать.
        /// Denied: награды нет (закрыли без награды, другая реклама уже идёт или реклама ещё не готова), выдавать нельзя.
        /// Error: сбой показа или ответ не пришёл за 3 минуты. Unavailable: платформы нет.
        /// </summary>
        Task<PlatformServiceResult> ShowRewardedAsync(string placementId);

        /// <summary>
        /// Показывает полноэкранную рекламу.
        /// Success: показ состоялся. Denied: реклама не показана (не готова или уже идёт другая).
        /// Error и Unavailable как у рекламы с наградой.
        /// </summary>
        Task<PlatformServiceResult> ShowInterstitialAsync(string placementId);
    }
}
