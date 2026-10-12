using System;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Yandex
{
    /// <summary>
    /// Ярлык игры через SDK Яндекс Игр (<c>ysdk.shortcut</c>). Сначала <see cref="CanShowPromptAsync"/>,
    /// затем <see cref="ShowPromptAsync"/>. Согласием считается результат платформы «accepted».
    /// </summary>
    public sealed class PluginYG2ShortcutService : IShortcutService
    {
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15d);
        private static readonly TimeSpan DialogTimeout = TimeSpan.FromSeconds(120d);

        private readonly PluginYG2FeatureClient client = new PluginYG2FeatureClient();
        private bool isPromptRunning;

        public bool IsPlatformAvailable => client.IsAvailable(
            PluginYG2FeatureClient.CanShowPrompt,
            PluginYG2FeatureClient.ShowPrompt);

        public async Task<PlatformServiceResult<bool>> CanShowPromptAsync()
        {
            var result = await client.CallAsync(PluginYG2FeatureClient.CanShowPrompt, CheckTimeout);
            return Convert(result);
        }

        public async Task<PlatformServiceResult<bool>> ShowPromptAsync()
        {
            if (isPromptRunning)
            {
                return PlatformServiceResult<bool>.Denied("Диалог ярлыка уже открыт.");
            }

            isPromptRunning = true;
            try
            {
                var result = await client.CallAsync(PluginYG2FeatureClient.ShowPrompt, DialogTimeout);
                return Convert(result);
            }
            finally
            {
                isPromptRunning = false;
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
