// Файл установлен пакетом com.kirillpolosov.yg-core v0.0.0 (YgCore/Переустановить мосты Яндекса).
// Не правьте его в игре: при обновлении пакета он будет перезаписан.
// ВАЖНО: в этой папке НЕТ файла .asmdef. Поэтому код компилируется в Assembly-CSharp рядом с PluginYG2
// и может обращаться к его типам (YG.YG2) напрямую. Сборка YgCore.Yandex (с .asmdef) на Assembly-CSharp
// ссылаться не может и находит этот мост по имени типа через рефлексию.
//
// Файл включается только если в проекте установлен PluginYG2 (его define-символ PLUGIN_YG_2),
// иначе пакет не должен ломать компиляцию проекта без плагина.
#if PLUGIN_YG_2
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;
using YG;

namespace YgCore.YandexBridge
{
    /// <summary>
    /// Мост между сервисами YgCore.Yandex и PluginYG2: превращает вызовы и события YG2 в простые
    /// статические методы и события, которые сервисы находят по имени.
    /// Модули PluginYG2 (реклама, лидерборд) учитываются через define-символы <c>RewardedAdv_yg</c>,
    /// <c>InterstitialAdv_yg</c>, <c>Leaderboards_yg</c>: если модуль не включён, его методы возвращают false.
    /// </summary>
    [Preserve]
    public static class YgSdkBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Сообщает загрузочному экрану страницы, что игра готова (см. Plugins/YgCoreLoading.jslib).
        [DllImport("__Internal")]
        private static extern void YgCoreLoadingGameReady_js();
#endif

        // Формат JSON с таблицей лидеров. Должен совпадать с классами в PluginYG2LeaderboardService.
        [Serializable]
        private sealed class LeaderboardPayload
        {
            public List<LeaderboardEntryPayload> entries =
                new List<LeaderboardEntryPayload>();
        }

        [Serializable]
        private sealed class LeaderboardEntryPayload
        {
            public string playerId;
            public string displayName;
            public long score;
            public int rank;
        }

        /// <summary>SDK инициализирован и данные игрока получены.</summary>
        [Preserve] public static event Action SdkReady;

        /// <summary>Окно входа закрыто (после входа или отмены). Приходит из доработанной JS-функции PluginYG2.</summary>
        [Preserve] public static event Action AuthDialogClosed;

        /// <summary>Платформа просит поставить игру на паузу (true) или снять паузу (false).</summary>
        [Preserve] public static event Action<bool> PlatformPauseChanged;

        [Preserve] public static event Action RewardedOpened;
        [Preserve] public static event Action RewardedClosed;
        [Preserve] public static event Action<string> RewardedGranted;
        [Preserve] public static event Action RewardedFailed;
        [Preserve] public static event Action InterstitialOpened;
        [Preserve] public static event Action<bool> InterstitialClosed;
        [Preserve] public static event Action InterstitialFailed;
        [Preserve] public static event Action<string> LeaderboardReceived;

        /// <summary>Вызывается из JS-доработки PluginYG2 через YGSendMessage (см. нижний класс файла).</summary>
        [Preserve]
        public static void NotifyAuthDialogClosed() => AuthDialogClosed?.Invoke();

        static YgSdkBridge()
        {
            // Подписываемся на события PluginYG2 один раз, при первом обращении к мосту.
            YG2.onGetSDKData += HandleSdkReady;
            YG2.onPauseGame += HandlePlatformPause;
#if RewardedAdv_yg
            YG2.onOpenRewardedAdv += HandleRewardedOpened;
            YG2.onCloseRewardedAdv += HandleRewardedClosed;
            YG2.onRewardAdv += HandleRewardedGranted;
            YG2.onErrorRewardedAdv += HandleRewardedFailed;
#endif
#if InterstitialAdv_yg
            YG2.onOpenInterAdv += HandleInterstitialOpened;
            YG2.onCloseInterAdvWasShow += HandleInterstitialClosed;
            YG2.onErrorInterAdv += HandleInterstitialFailed;
#endif
#if Leaderboards_yg
            YG2.onGetLeaderboard += HandleLeaderboardReceived;
#endif
        }

        [Preserve]
        public static bool IsSdkReady()
        {
            return YG2.isSDKEnabled && YG2.iPlatform != null;
        }

        [Preserve]
        public static bool IsGameplayActive()
        {
            return YG2.isGameplaying;
        }

        /// <summary>Сообщает платформе и загрузочному экрану страницы, что игра готова.</summary>
        [Preserve]
        public static void MarkGameReady()
        {
            YG2.GameReadyAPI();
#if UNITY_WEBGL && !UNITY_EDITOR
            YgCoreLoadingGameReady_js();
#endif
        }

        [Preserve]
        public static void SetGameplayActive(bool isActive)
        {
            if (isActive)
            {
                YG2.GameplayStart();
            }
            else
            {
                YG2.GameplayStop();
            }
        }

        /// <summary>Показывает рекламу с наградой. Возвращает false, если модуль выключен или другая реклама уже идёт.</summary>
        [Preserve]
        public static bool ShowRewarded(string placementId)
        {
#if RewardedAdv_yg
            if (YG2.nowAdsShow)
            {
                return false;
            }

            YG2.RewardedAdvShow(placementId);
            return true;
#else
            return false;
#endif
        }

        /// <summary>Показывает полноэкранную рекламу. Возвращает false, если модуль выключен, реклама уже идёт
        /// или ещё не прошёл интервал между показами.</summary>
        [Preserve]
        public static bool ShowInterstitial()
        {
#if InterstitialAdv_yg
            if (YG2.nowAdsShow || !YG2.isTimerAdvCompleted)
            {
                return false;
            }

            YG2.InterstitialAdvShow();
            return true;
#else
            return false;
#endif
        }

        /// <summary>Отправляет результат. Возвращает false, если модуль выключен или игрок не вошёл.</summary>
        [Preserve]
        public static bool SubmitScore(string leaderboardId, int score)
        {
#if Leaderboards_yg
            if (!YG2.isSDKEnabled || YG2.player == null || !YG2.player.auth)
            {
                return false;
            }

            YG2.SetLeaderboard(leaderboardId, score);
            return true;
#else
            return false;
#endif
        }

        /// <summary>Запрашивает таблицу лидеров. Ответ придёт событием <see cref="LeaderboardReceived"/>.</summary>
        [Preserve]
        public static bool RequestLeaderboard(string leaderboardId, int limit)
        {
#if Leaderboards_yg
            if (!YG2.isSDKEnabled)
            {
                return false;
            }

            YG2.GetLeaderboard(leaderboardId, Mathf.Max(1, limit), 0, "small");
            return true;
#else
            return false;
#endif
        }

        private static void HandlePlatformPause(bool isPaused)
        {
            PlatformPauseChanged?.Invoke(isPaused);
        }

        private static void HandleSdkReady()
        {
            SdkReady?.Invoke();
        }

#if RewardedAdv_yg
        private static void HandleRewardedOpened()
        {
            RewardedOpened?.Invoke();
        }

        private static void HandleRewardedClosed()
        {
            RewardedClosed?.Invoke();
        }

        private static void HandleRewardedGranted(string placementId)
        {
            RewardedGranted?.Invoke(placementId);
        }

        private static void HandleRewardedFailed()
        {
            RewardedFailed?.Invoke();
        }
#endif

#if InterstitialAdv_yg
        private static void HandleInterstitialOpened()
        {
            InterstitialOpened?.Invoke();
        }

        private static void HandleInterstitialClosed(bool wasShown)
        {
            InterstitialClosed?.Invoke(wasShown);
        }

        private static void HandleInterstitialFailed()
        {
            InterstitialFailed?.Invoke();
        }
#endif

#if Leaderboards_yg
        private static void HandleLeaderboardReceived(YG.Utils.LB.LBData data)
        {
            // Переводим данные PluginYG2 в простой JSON, чтобы сервис не зависел от его типов.
            var payload = new LeaderboardPayload();
            if (data?.players != null)
            {
                foreach (var player in data.players)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    payload.entries.Add(new LeaderboardEntryPayload
                    {
                        playerId = player.uniqueID ?? string.Empty,
                        displayName = player.name ?? string.Empty,
                        score = player.score,
                        rank = player.rank
                    });
                }
            }

            LeaderboardReceived?.Invoke(JsonUtility.ToJson(payload));
        }
#endif
    }
}

namespace YG.Insides
{
    /// <summary>
    /// Расширение компонента-получателя сообщений PluginYG2. JS-код страницы вызывает методы этого класса
    /// функцией <c>YG2Instance('Имя', аргумент)</c>.
    /// </summary>
    public partial class YGSendMessage
    {
        /// <summary>Вызывается доработанной функцией OpenAuthDialog в PluginYG2 (см. Documentation~/patches).</summary>
        [UnityEngine.Scripting.Preserve]
        public void YgCoreAuthDialogClosed() =>
            YgCore.YandexBridge.YgSdkBridge.NotifyAuthDialogClosed();
    }
}
#endif
