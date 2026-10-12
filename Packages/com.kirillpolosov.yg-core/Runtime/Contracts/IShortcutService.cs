using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>
    /// Предложение добавить ярлык игры на рабочий стол. Доступность зависит от устройства,
    /// браузера и правил платформы, поэтому сначала вызывается <see cref="CanShowPromptAsync"/>.
    /// </summary>
    public interface IShortcutService
    {
        /// <summary>Есть ли у платформы диалог ярлыка (в редакторе и вне Яндекс Игр false, кроме мока).</summary>
        bool IsPlatformAvailable { get; }

        /// <summary>Можно ли сейчас показать диалог. Success со значением false означает «нельзя».</summary>
        Task<PlatformServiceResult<bool>> CanShowPromptAsync();

        /// <summary>
        /// Показывает диалог. Success со значением true, если игрок согласился;
        /// Success со значением false, если отказался или закрыл окно.
        /// </summary>
        Task<PlatformServiceResult<bool>> ShowPromptAsync();
    }
}
