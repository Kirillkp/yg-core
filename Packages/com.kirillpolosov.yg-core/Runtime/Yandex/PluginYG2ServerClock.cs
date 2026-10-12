using System;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Yandex
{
    /// <summary>
    /// Серверное время через SDK Яндекс Игр (<c>ysdk.serverTime()</c>, миллисекунды Unix).
    /// Если у SDK нет этой функции, <see cref="IsPlatformAvailable"/> равно false и результат Unavailable:
    /// запасной вариант (локальные часы UTC) выбирает игра, пакет подмену не делает молча.
    /// </summary>
    public sealed class PluginYG2ServerClock : IServerClock
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15d);

        private readonly PluginYG2FeatureClient client = new PluginYG2FeatureClient();

        public bool IsPlatformAvailable => client.IsAvailable(PluginYG2FeatureClient.ServerTime);

        public async Task<PlatformServiceResult<DateTime>> GetUtcNowAsync()
        {
            var result = await client.CallAsync(PluginYG2FeatureClient.ServerTime, RequestTimeout);
            if (!result.IsSuccess)
            {
                return new PlatformServiceResult<DateTime>(result.Status, default(DateTime), result.Message);
            }

            // Число должно укладываться в диапазон дат: мусор от SDK не превращается в случайную дату.
            var milliseconds = result.Value.Number;
            if (double.IsNaN(milliseconds) || milliseconds <= 0d || milliseconds > 253402300799000d)
            {
                return PlatformServiceResult<DateTime>.Error("SDK вернул некорректное серверное время.");
            }

            var utc = DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds).UtcDateTime;
            return PlatformServiceResult<DateTime>.Success(utc);
        }
    }
}
