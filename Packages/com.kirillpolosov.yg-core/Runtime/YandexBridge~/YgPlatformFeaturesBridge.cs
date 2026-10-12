// Папка без .asmdef: код компилируется в Assembly-CSharp рядом с PluginYG2 (см. YgSdkBridge.cs).
// Включается только при установленном PluginYG2 на платформе Яндекс Игр.
#if PLUGIN_YG_2 && YandexGamesPlatform_yg
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using YgCore.Core;

namespace YgCore.YandexBridge
{
    /// <summary>
    /// Мост возможностей платформы, для которых в PluginYG2 нет нужного модуля или он не подходит:
    /// оценка игры, ярлык и серверное время. Идёт напрямую к SDK через Plugins/YgCorePlatformFeatures.jslib.
    /// Каждый запрос получает идентификатор, ответ ищется по нему: запоздавший ответ на другой запрос
    /// не может завершить текущий. Результат возвращается строкой JSON, разбирают её сервисы YgCore.Yandex.
    /// </summary>
    [Preserve]
    public static class YgPlatformFeaturesBridge
    {
        private static readonly Dictionary<string, TaskCompletionSource<string>> Pending =
            new Dictionary<string, TaskCompletionSource<string>>();

        // Только идентификатор: остальное разбирает сервис. Поле заполняется разбором JSON.
#pragma warning disable 0649
        [Serializable]
        private sealed class ReplyHeader
        {
            public string id;
        }
#pragma warning restore 0649

        /// <summary>
        /// Только для проверок без Unity: вместо вызова JS-функции вызывается этот обработчик (идентификатор, имя возможности),
        /// а ответ он отправляет сам через <see cref="Complete"/>. В игре не задаётся.
        /// </summary>
        public static Action<string, string> EditorRequestHandler;

        /// <summary>Только для проверок без Unity: какие возможности «есть у SDK». В игре не задаётся.</summary>
        public static Func<string, bool> EditorSupportOverride;

        /// <summary>Есть ли у SDK нужная функция. Имена возможностей: canReview, requestReview, canShowPrompt, showPrompt, serverTime.</summary>
        [Preserve]
        public static bool IsSupported(string feature)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return YgCoreFeatureSupported(feature) != 0;
#else
            return EditorSupportOverride != null && EditorSupportOverride(feature);
#endif
        }

        /// <summary>
        /// Вызывает возможность и ждёт ответа не дольше <paramref name="timeoutSeconds"/>.
        /// Возвращает JSON ответа (поля id, ok, supported, flag, number, reason, error).
        /// Сбой вызова SDK приходит как ответ с ok=false, а не исключением; исключение только по таймауту.
        /// </summary>
        [Preserve]
        public static async Task<string> CallAsync(string feature, int timeoutSeconds)
        {
            var id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<string>();
            Pending.Add(id, completion);
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                YgCoreFeatureCall(id, feature);
#else
                if (EditorRequestHandler == null)
                {
                    throw new InvalidOperationException("Мост возможностей платформы работает только в WebGL.");
                }

                EditorRequestHandler(id, feature);
#endif
                var timeout = RealtimeDelay.Delay(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
                if (await Task.WhenAny(completion.Task, timeout) != completion.Task)
                {
                    throw new TimeoutException("Платформа не ответила вовремя: " + feature);
                }

                return await completion.Task;
            }
            finally
            {
                // Запрос закончен (ответом, ошибкой или таймаутом): забываем идентификатор.
                Pending.Remove(id);
            }
        }

        /// <summary>Принимает ответ JS-стороны (JSON с полем id). Посторонние и повреждённые ответы игнорируются.</summary>
        [Preserve]
        public static void Complete(string json)
        {
            ReplyHeader header;
            try
            {
                header = JsonUtility.FromJson<ReplyHeader>(json);
            }
            catch (ArgumentException)
            {
                return;
            }

            if (header == null || string.IsNullOrEmpty(header.id)
                || !Pending.TryGetValue(header.id, out var completion))
            {
                return;
            }

            completion.TrySetResult(json);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Функции из Plugins/YgCorePlatformFeatures.jslib.
        [DllImport("__Internal")] private static extern int YgCoreFeatureSupported(string feature);
        [DllImport("__Internal")] private static extern void YgCoreFeatureCall(string id, string feature);
#endif
    }
}

namespace YG.Insides
{
    /// <summary>Приём ответа JS-стороны возможностей платформы. Имя метода используется в YgCorePlatformFeatures.jslib.</summary>
    public partial class YGSendMessage
    {
        [Preserve]
        public void YgCoreFeatureReply(string payload) =>
            YgCore.YandexBridge.YgPlatformFeaturesBridge.Complete(payload);
    }
}
#endif
