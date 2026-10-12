using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок диалога ярлыка. Показ без проверки <see cref="CanShowPromptAsync"/> даёт ошибку,
    /// после согласия игрока диалог больше не доступен.
    /// </summary>
    public sealed class EditorMockShortcutService : EditorMockPlatformService, IShortcutService
    {
        private bool canShowChecked;

        /// <summary>Можно ли показать диалог (то, что вернёт <see cref="CanShowPromptAsync"/>).</summary>
        public bool CanShow { get; set; } = true;

        /// <summary>Что сделает «игрок»: true согласится, false откажется.</summary>
        public bool UserAccepts { get; set; } = true;

        /// <summary>Сколько раз диалог был показан.</summary>
        public int PromptCount { get; private set; }

        public Task<PlatformServiceResult<bool>> CanShowPromptAsync()
        {
            canShowChecked = true;
            return Task.FromResult(BuildResult(
                CanShow,
                CanShow ? "Диалог ярлыка доступен." : "Мок: диалог ярлыка недоступен."));
        }

        public Task<PlatformServiceResult<bool>> ShowPromptAsync()
        {
            var availability = BuildResult(true);
            if (!availability.IsSuccess)
            {
                return Task.FromResult(PlatformServiceResult<bool>.Error(availability.Message));
            }

            if (!canShowChecked)
            {
                return Task.FromResult(PlatformServiceResult<bool>.Error(
                    "Мок: сначала нужно вызвать CanShowPromptAsync."));
            }

            if (!CanShow)
            {
                return Task.FromResult(PlatformServiceResult<bool>.Denied("Мок: диалог ярлыка недоступен."));
            }

            PromptCount++;
            canShowChecked = false;

            // После согласия платформа ярлык уже не предлагает.
            if (UserAccepts)
            {
                CanShow = false;
            }

            return Task.FromResult(PlatformServiceResult<bool>.Success(
                UserAccepts,
                UserAccepts ? "Мок: игрок согласился." : "Мок: игрок отказался."));
        }
    }
}
