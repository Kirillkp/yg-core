using System;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Yandex
{
    /// <summary>
    /// Оценка игры через SDK Яндекс Игр (<c>ysdk.feedback</c>). Порядок вызовов соблюдает вызывающий:
    /// сначала <see cref="CanReviewAsync"/>, затем <see cref="RequestReviewAsync"/>.
    /// Одновременно выполняется один запрос: второе окно поверх первого не открывается.
    /// </summary>
    public sealed class PluginYG2ReviewService : IReviewService
    {
        // Проверка отвечает быстро, а окно оценки ждёт решения игрока.
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15d);
        private static readonly TimeSpan DialogTimeout = TimeSpan.FromSeconds(120d);

        private readonly PluginYG2FeatureClient client = new PluginYG2FeatureClient();
        private bool isRequestRunning;

        public bool IsPlatformAvailable => client.IsAvailable(
            PluginYG2FeatureClient.CanReview,
            PluginYG2FeatureClient.RequestReview);

        public async Task<PlatformServiceResult<bool>> CanReviewAsync()
        {
            var result = await client.CallAsync(PluginYG2FeatureClient.CanReview, CheckTimeout);
            return Convert(result);
        }

        public async Task<PlatformServiceResult<bool>> RequestReviewAsync()
        {
            if (isRequestRunning)
            {
                return PlatformServiceResult<bool>.Denied("Окно оценки уже открыто.");
            }

            isRequestRunning = true;
            try
            {
                var result = await client.CallAsync(PluginYG2FeatureClient.RequestReview, DialogTimeout);
                return Convert(result);
            }
            finally
            {
                isRequestRunning = false;
            }
        }

        private static PlatformServiceResult<bool> Convert(
            PlatformServiceResult<PluginYG2FeatureClient.Answer> result)
        {
            return result.IsSuccess
                ? PlatformServiceResult<bool>.Success(result.Value.Flag, result.Message)
                : new PlatformServiceResult<bool>(result.Status, false, result.Message);
        }
    }
}
