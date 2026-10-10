using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Заглушка хранилища без платформы: все операции возвращают PlatformUnavailable.
    /// Загрузка при этом не создаёт «пустое сохранение»: игра узнаёт, что хранилища нет.
    /// </summary>
    public sealed class UnavailableSaveService : ISaveService
    {
        public bool IsPlatformAvailable => false;

        public Task<PlatformServiceResult<string>> LoadAsync(string key)
        {
            return Task.FromResult(
                PlatformServiceResult<string>.Unavailable(
                    "Хранилище недоступно на этой платформе."));
        }

        public Task<PlatformServiceResult> SaveAsync(string key, string payload)
        {
            return Task.FromResult(
                PlatformServiceResult.Unavailable(
                    "Хранилище недоступно на этой платформе."));
        }

        public Task<PlatformServiceResult> DeleteAsync(string key)
        {
            return Task.FromResult(
                PlatformServiceResult.Unavailable(
                    "Хранилище недоступно на этой платформе."));
        }
    }
}
