using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>Заглушка ярлыка без платформы.</summary>
    public sealed class UnavailableShortcutService : IShortcutService
    {
        public bool IsPlatformAvailable => false;

        public Task<PlatformServiceResult<bool>> CanShowPromptAsync()
        {
            return Task.FromResult(
                PlatformServiceResult<bool>.Unavailable(
                    "Ярлык недоступен на этой платформе."));
        }

        public Task<PlatformServiceResult<bool>> ShowPromptAsync()
        {
            return Task.FromResult(
                PlatformServiceResult<bool>.Unavailable(
                    "Ярлык недоступен на этой платформе."));
        }
    }
}
