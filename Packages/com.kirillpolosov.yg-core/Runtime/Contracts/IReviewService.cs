using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>
    /// Оценка игры через окно платформы. Порядок обязателен: сначала <see cref="CanReviewAsync"/>,
    /// и только при <c>Value == true</c> можно вызывать <see cref="RequestReviewAsync"/>.
    /// Платформа сама учитывает, вошёл ли игрок и оставлял ли он оценку раньше, а запрос
    /// принимает один раз за сессию.
    /// </summary>
    public interface IReviewService
    {
        /// <summary>Есть ли у платформы окно оценки (в редакторе и вне Яндекс Игр false, кроме мока).</summary>
        bool IsPlatformAvailable { get; }

        /// <summary>
        /// Можно ли сейчас показать запрос оценки. Success со значением false означает «нельзя»
        /// (не вошёл, уже оценил, запрос уже был), причина лежит в <see cref="PlatformServiceResult.Message"/>.
        /// </summary>
        Task<PlatformServiceResult<bool>> CanReviewAsync();

        /// <summary>
        /// Показывает окно оценки. Success со значением true, если игрок оценил игру;
        /// Success со значением false, если игрок закрыл окно без оценки.
        /// </summary>
        Task<PlatformServiceResult<bool>> RequestReviewAsync();
    }
}
