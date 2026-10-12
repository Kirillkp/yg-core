using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок окна оценки. Повторяет правила платформы, чтобы ошибки порядка вызовов находились в редакторе:
    /// запрос без предварительного <see cref="CanReviewAsync"/> даёт ошибку, а после ответа игрока
    /// (оценил или закрыл окно) запрос в этой сессии больше недоступен.
    /// </summary>
    public sealed class EditorMockReviewService : EditorMockPlatformService, IReviewService
    {
        private bool canReviewChecked;

        /// <summary>Можно ли показать запрос (то, что вернёт <see cref="CanReviewAsync"/>). Задаётся из проверки.</summary>
        public bool CanReview { get; set; } = true;

        /// <summary>Что сделает «игрок» в окне: true оценит игру, false закроет окно без оценки.</summary>
        public bool UserSendsFeedback { get; set; } = true;

        /// <summary>Сколько раз окно оценки было показано.</summary>
        public int RequestCount { get; private set; }

        public Task<PlatformServiceResult<bool>> CanReviewAsync()
        {
            canReviewChecked = true;
            return Task.FromResult(BuildResult(
                CanReview,
                CanReview ? "Запрос оценки доступен." : "Мок: оценка недоступна."));
        }

        public Task<PlatformServiceResult<bool>> RequestReviewAsync()
        {
            var availability = BuildResult(true);
            if (!availability.IsSuccess)
            {
                return Task.FromResult(PlatformServiceResult<bool>.Error(availability.Message));
            }

            // Как у платформы: без CanReview запрос отклоняется.
            if (!canReviewChecked)
            {
                return Task.FromResult(PlatformServiceResult<bool>.Error(
                    "Мок: сначала нужно вызвать CanReviewAsync."));
            }

            if (!CanReview)
            {
                return Task.FromResult(PlatformServiceResult<bool>.Denied("Мок: оценка недоступна."));
            }

            RequestCount++;

            // Платформа принимает запрос оценки один раз за сессию.
            canReviewChecked = false;
            CanReview = false;
            return Task.FromResult(PlatformServiceResult<bool>.Success(
                UserSendsFeedback,
                UserSendsFeedback ? "Мок: игрок оценил игру." : "Мок: игрок закрыл окно."));
        }
    }
}
