using System;
using System.Reflection;
using System.Threading.Tasks;
using YgCore.Contracts;
using YgCore.Core;

namespace YgCore.Yandex
{
    /// <summary>
    /// Реклама через PluginYG2. Одновременно показывается не больше одной рекламы.
    /// Результат собирается из событий моста: открыта, награда выдана, закрыта, ошибка.
    /// Если реклама не открылась за 20 секунд или за 3 минуты нет ответа, возвращается Error.
    /// </summary>
    public sealed class PluginYG2AdsService : IAdsService, IDisposable
    {
        private static readonly TimeSpan AdTimeout = TimeSpan.FromMinutes(3d);

        // Сколько ждать, пока платформа откроет рекламу (событие «открыта»). Без него возвращается Error.
        private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(20d);

        private readonly MethodInfo isSdkReadyMethod;
        private readonly MethodInfo showRewardedMethod;
        private readonly MethodInfo showInterstitialMethod;
        private readonly EventInfo rewardedOpenedEvent;
        private readonly EventInfo rewardedClosedEvent;
        private readonly EventInfo rewardedGrantedEvent;
        private readonly EventInfo rewardedFailedEvent;
        private readonly EventInfo interstitialOpenedEvent;
        private readonly EventInfo interstitialClosedEvent;
        private readonly EventInfo interstitialFailedEvent;
        private readonly Action rewardedOpenedHandler;
        private readonly Action rewardedClosedHandler;
        private readonly Action<string> rewardedGrantedHandler;
        private readonly Action rewardedFailedHandler;
        private readonly Action interstitialOpenedHandler;
        private readonly Action<bool> interstitialClosedHandler;
        private readonly Action interstitialFailedHandler;

        // Состояние текущего показа. null, пока никакая реклама не идёт.
        private TaskCompletionSource<PlatformServiceResult> pendingOperation;
        private AdPlacementKind pendingKind;
        private string pendingPlacementId;
        private bool pendingRewardGranted;
        private bool pendingAdOpened;
        private bool isDisposed;

        public PluginYG2AdsService()
        {
            var bridgeType = PluginYG2BridgeReflection.ResolveBridgeType();
            isSdkReadyMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "IsSdkReady");
            showRewardedMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "ShowRewarded");
            showInterstitialMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "ShowInterstitial");

            rewardedOpenedEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "RewardedOpened");
            rewardedClosedEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "RewardedClosed");
            rewardedGrantedEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "RewardedGranted");
            rewardedFailedEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "RewardedFailed");
            interstitialOpenedEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "InterstitialOpened");
            interstitialClosedEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "InterstitialClosed");
            interstitialFailedEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "InterstitialFailed");

            rewardedOpenedHandler = HandleRewardedOpened;
            rewardedClosedHandler = HandleRewardedClosed;
            rewardedGrantedHandler = HandleRewardedGranted;
            rewardedFailedHandler = HandleRewardedFailed;
            interstitialOpenedHandler = HandleInterstitialOpened;
            interstitialClosedHandler = HandleInterstitialClosed;
            interstitialFailedHandler = HandleInterstitialFailed;
            AddHandlers();
        }

        public event EventHandler<AdPlaybackEventArgs> AdStarted;

        public event EventHandler<AdPlaybackEventArgs> AdFinished;

        public bool IsPlatformAvailable => !isDisposed
            && PluginYG2BridgeReflection.InvokeBool(isSdkReadyMethod)
            && showRewardedMethod != null
            && showInterstitialMethod != null;

        public Task<PlatformServiceResult> ShowRewardedAsync(string placementId)
        {
            return ShowAsync(
                AdPlacementKind.Rewarded,
                placementId,
                showRewardedMethod,
                new object[] { placementId });
        }

        public Task<PlatformServiceResult> ShowInterstitialAsync(string placementId)
        {
            return ShowAsync(
                AdPlacementKind.Interstitial,
                placementId,
                showInterstitialMethod,
                null);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            RemoveHandlers();
            CompletePending(
                PlatformServiceResult.Unavailable("Сервис рекламы выгружен."));
        }

        private async Task<PlatformServiceResult> ShowAsync(
            AdPlacementKind kind,
            string placementId,
            MethodInfo showMethod,
            object[] arguments)
        {
            if (!IsPlatformAvailable || showMethod == null)
            {
                return PlatformServiceResult.Unavailable(
                    "Реклама PluginYG2 недоступна.");
            }

            // Пока идёт одна реклама, вторую не запускаем.
            if (pendingOperation != null)
            {
                return PlatformServiceResult.Denied(
                    "Другая реклама уже показывается.");
            }

            pendingKind = kind;
            pendingPlacementId = placementId ?? string.Empty;
            pendingRewardGranted = false;
            pendingAdOpened = false;
            pendingOperation = new TaskCompletionSource<PlatformServiceResult>();
            var operation = pendingOperation;

            try
            {
                // Мост возвращает false, если реклама ещё не готова (например, не прошёл интервал).
                if (!PluginYG2BridgeReflection.InvokeBool(showMethod, arguments))
                {
                    pendingOperation = null;
                    return PlatformServiceResult.Denied(
                        "Реклама ещё не готова.");
                }

                // Если платформа за короткое время вообще не открыла рекламу (блокировщик, аккаунт без рекламы, молчание SDK),
                // не ждём полные три минуты: игрок и игра не должны зависать в ожидании.
                var opened = await Task.WhenAny(
                    operation.Task,
                    RealtimeDelay.Delay(OpenTimeout));
                if (opened != operation.Task && !pendingAdOpened && ReferenceEquals(pendingOperation, operation))
                {
                    CompletePending(
                        PlatformServiceResult.Error(
                            "Реклама не открылась вовремя."));
                }

                var completed = await Task.WhenAny(
                    operation.Task,
                    RealtimeDelay.Delay(AdTimeout));
                if (completed != operation.Task)
                {
                    CompletePending(
                        PlatformServiceResult.Error(
                            "Ответ рекламы не получен вовремя."));
                }

                return await operation.Task;
            }
            catch (Exception exception)
            {
                var result = PlatformServiceResult.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
                CompletePending(result);
                return result;
            }
        }

        private void HandleRewardedOpened()
        {
            NotifyStarted(AdPlacementKind.Rewarded);
        }

        private void HandleRewardedClosed()
        {
            if (pendingKind != AdPlacementKind.Rewarded)
            {
                return;
            }

            // Успех только если перед закрытием платформа засчитала награду.
            CompletePending(
                pendingRewardGranted
                    ? PlatformServiceResult.Success("Реклама с наградой завершена.")
                    : PlatformServiceResult.Denied(
                        "Рекламу закрыли без награды."));
        }

        private void HandleRewardedGranted(string placementId)
        {
            // Награда засчитывается только для того места показа, которое мы запрашивали.
            if (pendingKind == AdPlacementKind.Rewarded
                && string.Equals(
                    pendingPlacementId,
                    placementId ?? string.Empty,
                    StringComparison.Ordinal))
            {
                pendingRewardGranted = true;
            }
        }

        private void HandleRewardedFailed()
        {
            if (pendingKind == AdPlacementKind.Rewarded)
            {
                CompletePending(
                    PlatformServiceResult.Error("Ошибка рекламы с наградой."));
            }
        }

        private void HandleInterstitialOpened()
        {
            NotifyStarted(AdPlacementKind.Interstitial);
        }

        private void HandleInterstitialClosed(bool wasShown)
        {
            if (pendingKind != AdPlacementKind.Interstitial)
            {
                return;
            }

            CompletePending(
                wasShown
                    ? PlatformServiceResult.Success("Полноэкранная реклама завершена.")
                    : PlatformServiceResult.Denied(
                        "Полноэкранная реклама не была показана."));
        }

        private void HandleInterstitialFailed()
        {
            if (pendingKind == AdPlacementKind.Interstitial)
            {
                CompletePending(
                    PlatformServiceResult.Error("Ошибка полноэкранной рекламы."));
            }
        }

        private void NotifyStarted(AdPlacementKind kind)
        {
            // Событие начала шлём один раз на показ и только для запрошенного нами вида рекламы.
            if (pendingOperation == null || pendingKind != kind || pendingAdOpened)
            {
                return;
            }

            pendingAdOpened = true;
            AdStarted?.Invoke(
                this,
                new AdPlaybackEventArgs(pendingKind, pendingPlacementId));
        }

        private void CompletePending(PlatformServiceResult result)
        {
            var operation = pendingOperation;
            if (operation == null)
            {
                return;
            }

            var args = new AdPlaybackEventArgs(pendingKind, pendingPlacementId);
            pendingOperation = null;

            // Событие конца шлём только если реклама открывалась: так начало и конец всегда парные,
            // и игра не останется на паузе из-за «закрытия» того, что не открывалось.
            if (pendingAdOpened)
            {
                AdFinished?.Invoke(this, args);
            }

            pendingAdOpened = false;
            pendingRewardGranted = false;
            pendingPlacementId = string.Empty;
            operation.TrySetResult(result);
        }

        private void AddHandlers()
        {
            rewardedOpenedEvent?.AddEventHandler(null, rewardedOpenedHandler);
            rewardedClosedEvent?.AddEventHandler(null, rewardedClosedHandler);
            rewardedGrantedEvent?.AddEventHandler(null, rewardedGrantedHandler);
            rewardedFailedEvent?.AddEventHandler(null, rewardedFailedHandler);
            interstitialOpenedEvent?.AddEventHandler(null, interstitialOpenedHandler);
            interstitialClosedEvent?.AddEventHandler(null, interstitialClosedHandler);
            interstitialFailedEvent?.AddEventHandler(null, interstitialFailedHandler);
        }

        private void RemoveHandlers()
        {
            rewardedOpenedEvent?.RemoveEventHandler(null, rewardedOpenedHandler);
            rewardedClosedEvent?.RemoveEventHandler(null, rewardedClosedHandler);
            rewardedGrantedEvent?.RemoveEventHandler(null, rewardedGrantedHandler);
            rewardedFailedEvent?.RemoveEventHandler(null, rewardedFailedHandler);
            interstitialOpenedEvent?.RemoveEventHandler(null, interstitialOpenedHandler);
            interstitialClosedEvent?.RemoveEventHandler(null, interstitialClosedHandler);
            interstitialFailedEvent?.RemoveEventHandler(null, interstitialFailedHandler);
        }
    }
}
