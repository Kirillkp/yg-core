using System;
using System.Reflection;
using YgCore.Contracts;

namespace YgCore.Yandex
{
    /// <summary>
    /// Жизненный цикл геймплея через PluginYG2: отметки начала и конца геймплея и пауза от платформы.
    /// Если SDK ещё не готов, последнее запрошенное состояние запоминается и отправляется после инициализации.
    /// </summary>
    public sealed class PluginYG2GameplayLifecycleService : IGameplayLifecycleService,
        IDisposable
    {
        private readonly MethodInfo isSdkReadyMethod;
        private readonly MethodInfo isGameplayActiveMethod;
        private readonly MethodInfo setGameplayActiveMethod;
        private readonly EventInfo platformPauseEvent;
        private readonly EventInfo sdkReadyEvent;
        private readonly Action<bool> platformPauseHandler;
        private readonly Action sdkReadyHandler;

        // Последнее состояние, которое запросила игра. Нужно для повторной отправки после готовности SDK.
        private bool? requestedGameplayActive;
        private bool isDisposed;

        public PluginYG2GameplayLifecycleService()
        {
            var bridgeType = PluginYG2BridgeReflection.ResolveBridgeType();
            isSdkReadyMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "IsSdkReady");
            isGameplayActiveMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "IsGameplayActive");
            setGameplayActiveMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "SetGameplayActive");
            platformPauseEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "PlatformPauseChanged");
            sdkReadyEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "SdkReady");
            platformPauseHandler = HandlePlatformPauseChanged;
            sdkReadyHandler = HandleSdkReady;
            platformPauseEvent?.AddEventHandler(null, platformPauseHandler);
            sdkReadyEvent?.AddEventHandler(null, sdkReadyHandler);
        }

        public event EventHandler<PlatformPauseChangedEventArgs> PlatformPauseChanged;

        public bool IsPlatformAvailable => !isDisposed
            && PluginYG2BridgeReflection.InvokeBool(isSdkReadyMethod)
            && setGameplayActiveMethod != null;

        public bool IsGameplayActive => !isDisposed
            && PluginYG2BridgeReflection.InvokeBool(isGameplayActiveMethod);

        public PlatformServiceResult SetGameplayActive(bool isActive)
        {
            requestedGameplayActive = isActive;
            if (!IsPlatformAvailable)
            {
                return setGameplayActiveMethod != null
                    ? PlatformServiceResult.Success(
                        "Состояние геймплея поставлено в очередь до инициализации SDK.")
                    : PlatformServiceResult.Unavailable(
                        "API геймплея PluginYG2 недоступен.");
            }

            try
            {
                setGameplayActiveMethod.Invoke(null, new object[] { isActive });
                return PlatformServiceResult.Success(
                    isActive ? "Геймплей начат." : "Геймплей остановлен.");
            }
            catch (Exception exception)
            {
                return PlatformServiceResult.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            platformPauseEvent?.RemoveEventHandler(null, platformPauseHandler);
            sdkReadyEvent?.RemoveEventHandler(null, sdkReadyHandler);
        }

        private void HandlePlatformPauseChanged(bool isPaused)
        {
            PlatformPauseChanged?.Invoke(
                this,
                new PlatformPauseChangedEventArgs(isPaused));
        }

        private void HandleSdkReady()
        {
            if (requestedGameplayActive.HasValue)
            {
                SetGameplayActive(requestedGameplayActive.Value);
            }
        }
    }
}
