// Папка без .asmdef: код компилируется в Assembly-CSharp рядом с PluginYG2 (см. YgSdkBridge.cs).
// Включается только при установленном PluginYG2 и включённом модуле Storage на платформе Яндекс Игр.
#if PLUGIN_YG_2 && Storage_yg && YandexGamesPlatform_yg
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using YgCore.Core;
#if NJSON_STORAGE_YG2
using Newtonsoft.Json;
#endif

namespace YgCore.YandexBridge
{
    /// <summary>
    /// Мост хранилища с подтверждением. Сохраняет и загружает ВЕСЬ конверт <c>YG2.saves</c> (формат PluginYG2
    /// не меняется), но в отличие от штатного <c>SaveCloud</c> дожидается ответа Яндекса:
    /// каждый запрос получает идентификатор, запись идёт через очередь на стороне JS, а ответ ищется по идентификатору.
    /// Запоздавший ответ на другой запрос не может подтвердить текущий.
    /// Таймаут 15 секунд; сбой загрузки возвращается ошибкой, а не пустым сохранением.
    /// </summary>
    [Preserve]
    public static class YgStorageBridge
    {
        private static readonly Dictionary<string, TaskCompletionSource<string>> Pending =
            new Dictionary<string, TaskCompletionSource<string>>();

        // Ответ JS-стороны. Поля заполняются при разборе JSON, поэтому предупреждение компилятора о них не нужно.
#pragma warning disable 0649
        [Serializable]
        private sealed class Reply
        {
            public string id;
            public string payload;
            public string error;
        }
#pragma warning restore 0649

        /// <summary>Загружает данные из облака и заменяет ими <c>YG2.saves</c>. Возвращает исходную строку.</summary>
        [Preserve]
        public static async Task<string> LoadAsync()
        {
            var payload = await RequestAsync(false);

            // Отсутствие данных допустимо (новый игрок), а вот сбой запроса не должен стать пустым сохранением:
            // он вылетит исключением в RequestAsync и сюда не дойдёт.
#if NJSON_STORAGE_YG2
            var saves = string.IsNullOrEmpty(payload)
                ? new YG.SavesYG() : JsonConvert.DeserializeObject<YG.SavesYG>(payload);
#else
            var saves = string.IsNullOrEmpty(payload)
                ? new YG.SavesYG() : JsonUtility.FromJson<YG.SavesYG>(payload);
#endif
            YG.YG2.saves = saves ?? throw new InvalidOperationException("Данные хранилища повреждены.");
            return payload;
        }

        /// <summary>Сохраняет текущий <c>YG2.saves</c> в облако и ждёт подтверждения Яндекса.</summary>
        [Preserve]
        public static Task<string> SaveAsync() => RequestAsync(true);

        private static async Task<string> RequestAsync(bool save)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<string>();
            Pending.Add(id, completion);
            try
            {
                if (save)
                {
                    // idSave растёт при каждой записи: по нему PluginYG2 выбирает более свежее сохранение.
                    YG.YG2.saves.idSave++;
#if NJSON_STORAGE_YG2
                    var payload = JsonConvert.SerializeObject(YG.YG2.saves);
#else
                    var payload = JsonUtility.ToJson(YG.YG2.saves);
#endif
                    YgCoreStorageSave(id, payload);
                }
                else
                {
                    YgCoreStorageLoad(id);
                }

                if (await Task.WhenAny(completion.Task, RealtimeDelay.Delay(TimeSpan.FromSeconds(15))) != completion.Task)
                {
                    throw new TimeoutException("Подтверждение хранилища не получено вовремя.");
                }

                return await completion.Task;
            }
            finally
            {
                // Запрос закончен (успехом, ошибкой или таймаутом): забываем идентификатор,
                // чтобы запоздавший ответ уже не мог ничего подтвердить.
                Pending.Remove(id);
            }
#else
            await Task.CompletedTask;
            throw new InvalidOperationException("Мост хранилища работает только в WebGL.");
#endif
        }

        /// <summary>Принимает ответ JS-стороны (JSON с полями id, payload, error).</summary>
        [Preserve]
        public static void Complete(string json)
        {
            Reply reply;
            try
            {
                reply = JsonUtility.FromJson<Reply>(json);
            }
            catch (ArgumentException)
            {
                // Посторонний или повреждённый ответ: никакому запросу он принадлежать не может, игнорируем.
                return;
            }

            if (reply == null || string.IsNullOrEmpty(reply.id)
                || !Pending.TryGetValue(reply.id, out var completion))
            {
                return;
            }

            if (!string.IsNullOrEmpty(reply.error))
            {
                completion.TrySetException(new InvalidOperationException(reply.error));
            }
            else
            {
                completion.TrySetResult(reply.payload);
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Функции из Plugins/YgCoreStorage.jslib.
        [DllImport("__Internal")] private static extern void YgCoreStorageSave(string id, string payload);
        [DllImport("__Internal")] private static extern void YgCoreStorageLoad(string id);
#endif
    }
}

namespace YG.Insides
{
    /// <summary>Приём ответа JS-стороны хранилища. Имя метода используется в YgCoreStorage.jslib.</summary>
    public partial class YGSendMessage
    {
        [Preserve]
        public void YgCoreStorageCompleted(string payload) =>
            YgCore.YandexBridge.YgStorageBridge.Complete(payload);
    }
}
#endif
