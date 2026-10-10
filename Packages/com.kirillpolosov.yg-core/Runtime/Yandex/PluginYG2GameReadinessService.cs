using System;
using System.Reflection;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Yandex
{
    /// <summary>
    /// Отметка «игра готова» через PluginYG2. Если SDK ещё не инициализирован, запрос запоминается
    /// и отправляется сразу после инициализации (результат при этом Success с пояснением «в очереди»).
    /// </summary>
    public sealed class PluginYG2GameReadinessService : IGameReadinessService,
        IDisposable
    {
        private readonly MethodInfo isSdkReadyMethod;
        private readonly MethodInfo markGameReadyMethod;
        private readonly EventInfo sdkReadyEvent;
        private readonly Action sdkReadyHandler;
        private bool readyRequested;
        private bool isDisposed;

        public PluginYG2GameReadinessService()
        {
            var bridgeType = PluginYG2BridgeReflection.ResolveBridgeType();
            isSdkReadyMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "IsSdkReady");
            markGameReadyMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "MarkGameReady");
            sdkReadyEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "SdkReady");
            sdkReadyHandler = HandleSdkReady;
            sdkReadyEvent?.AddEventHandler(null, sdkReadyHandler);
        }

        public bool IsPlatformAvailable => !isDisposed
            && PluginYG2BridgeReflection.InvokeBool(isSdkReadyMethod);

        public bool IsReadySent { get; private set; }

        public Task<PlatformServiceResult> MarkReadyAsync()
        {
            if (IsReadySent)
            {
                return Task.FromResult(
                    PlatformServiceResult.Success("Готовность уже была отправлена."));
            }

            if (isDisposed || markGameReadyMethod == null)
            {
                return Task.FromResult(
                    PlatformServiceResult.Unavailable(
                        "API готовности игры PluginYG2 недоступен."));
            }

            readyRequested = true;
            if (!IsPlatformAvailable)
            {
                return Task.FromResult(
                    PlatformServiceResult.Success(
                        "Готовность поставлена в очередь до инициализации SDK."));
            }

            return Task.FromResult(SendReady());
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            sdkReadyEvent?.RemoveEventHandler(null, sdkReadyHandler);
        }

        private void HandleSdkReady()
        {
            // SDK инициализировался после запроса: отправляем отложенную готовность.
            if (readyRequested && !IsReadySent)
            {
                SendReady();
            }
        }

        private PlatformServiceResult SendReady()
        {
            try
            {
                markGameReadyMethod.Invoke(null, null);
                IsReadySent = true;
                return PlatformServiceResult.Success("Готовность отправлена.");
            }
            catch (Exception exception)
            {
                return PlatformServiceResult.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
        }
    }
}
