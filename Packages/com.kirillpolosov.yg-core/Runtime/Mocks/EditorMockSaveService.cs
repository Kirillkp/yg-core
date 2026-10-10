using System.Collections.Generic;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок хранилища в памяти. Включив <see cref="SimulateTimeout"/>, можно проверить, как игра
    /// переживает сбой запроса (все операции вернут Error).
    /// </summary>
    public sealed class EditorMockSaveService : EditorMockPlatformService, ISaveService
    {
        private readonly Dictionary<string, string> storage = new Dictionary<string, string>();

        /// <summary>Имитировать таймаут запросов: операции возвращают Error и ничего не меняют.</summary>
        public bool SimulateTimeout { get; set; }

        public async Task<PlatformServiceResult<string>> LoadAsync(string key)
        {
            if (SimulateTimeout)
            {
                await Task.Yield();
                return PlatformServiceResult<string>.Error("Мок-хранилище: запрос завершился по таймауту.");
            }

            // Нет ключа: успех со значением null, так же как у настоящего хранилища.
            storage.TryGetValue(key, out var payload);
            return BuildResult<string>(payload);
        }

        public async Task<PlatformServiceResult> SaveAsync(string key, string payload)
        {
            if (SimulateTimeout)
            {
                await Task.Yield();
                return PlatformServiceResult.Error("Мок-хранилище: запрос завершился по таймауту.");
            }

            var result = BuildResult("Мок-сохранение выполнено.");
            if (result.IsSuccess)
            {
                storage[key] = payload;
            }

            return result;
        }

        public async Task<PlatformServiceResult> DeleteAsync(string key)
        {
            if (SimulateTimeout)
            {
                await Task.Yield();
                return PlatformServiceResult.Error("Мок-хранилище: запрос завершился по таймауту.");
            }

            var result = BuildResult("Мок-сохранение удалено.");
            if (result.IsSuccess)
            {
                storage.Remove(key);
            }

            return result;
        }

        /// <summary>Кладёт данные в хранилище напрямую, минуя сценарий (подготовка проверки).</summary>
        public void SetPayload(string key, string payload)
        {
            storage[key] = payload;
        }

        /// <summary>Очищает хранилище.</summary>
        public void Clear()
        {
            storage.Clear();
        }
    }
}
