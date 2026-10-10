using System;
using System.Reflection;
using System.Threading.Tasks;
using YgCore.Contracts;
using YgCore.Core;

namespace YgCore.Yandex
{
    /// <summary>
    /// Авторизация через PluginYG2. Профиль читается из <c>YG2.player</c> и обновляется по событию
    /// получения данных SDK.
    ///
    /// Чтобы отличить «игрок закрыл окно входа» от «ответа нет», нужна небольшая правка PluginYG2
    /// (JS-функция OpenAuthDialog сообщает игре о закрытии окна): см. Documentation~/patches.
    /// Без правки отмена входа определится только по таймауту (30 секунд, результат Error).
    /// </summary>
    public sealed class PluginYG2AuthorizationService : IAuthorizationService, IDisposable
    {
        private static readonly TimeSpan SignInTimeout = TimeSpan.FromSeconds(30d);

        private readonly Type yg2Type;
        private readonly FieldInfo sdkDataEventField;
        private readonly Action sdkDataHandler;
        private readonly EventInfo authDialogClosedEvent;
        private readonly Action authDialogClosedHandler;
        private AuthorizationProfile currentProfile;
        private TaskCompletionSource<AuthorizationProfile> pendingSignIn;
        private bool isDisposed;

        public PluginYG2AuthorizationService()
        {
            yg2Type = PluginYG2BridgeReflection.ResolveType(PluginYG2BridgeReflection.Yg2TypeName);
            sdkDataEventField = yg2Type?.GetField(
                "onGetSDKData",
                BindingFlags.Public | BindingFlags.Static);
            sdkDataHandler = HandleSdkDataReceived;
            AddSdkDataHandler();
            authDialogClosedEvent = PluginYG2BridgeReflection.FindEvent(
                PluginYG2BridgeReflection.ResolveBridgeType(), "AuthDialogClosed");
            authDialogClosedHandler = HandleAuthDialogClosed;
            authDialogClosedEvent?.AddEventHandler(null, authDialogClosedHandler);
            currentProfile = ReadProfile();
        }

        public event EventHandler<AuthorizationProfileChangedEventArgs> ProfileChanged;

        public bool IsPlatformAvailable => yg2Type != null
            && ReadStaticBool("isSDKEnabled")
            && ReadStaticMember("iPlatform") != null;

        public AuthorizationProfile CurrentProfile => currentProfile;

        public async Task<PlatformServiceResult<AuthorizationProfile>> SignInAsync()
        {
            if (isDisposed)
            {
                return PlatformServiceResult<AuthorizationProfile>.Unavailable(
                    "Сервис авторизации выгружен.");
            }

            // Уже вошли: повторно окно не открываем.
            if (currentProfile.IsAuthorized)
            {
                return PlatformServiceResult<AuthorizationProfile>.Success(currentProfile);
            }

            if (!IsPlatformAvailable)
            {
                return PlatformServiceResult<AuthorizationProfile>.Unavailable(
                    "Авторизация Яндекс Игр недоступна.");
            }

            if (pendingSignIn != null)
            {
                return PlatformServiceResult<AuthorizationProfile>.Error(
                    "Авторизация уже выполняется.");
            }

            var openAuthDialog = yg2Type.GetMethod(
                "OpenAuthDialog",
                BindingFlags.Public | BindingFlags.Static);
            if (openAuthDialog == null)
            {
                return PlatformServiceResult<AuthorizationProfile>.Unavailable(
                    "Модуль авторизации PluginYG2 недоступен.");
            }

            var signInCompletion = new TaskCompletionSource<AuthorizationProfile>();
            pendingSignIn = signInCompletion;
            try
            {
                openAuthDialog.Invoke(null, null);
            }
            catch (Exception exception)
            {
                pendingSignIn = null;
                return PlatformServiceResult<AuthorizationProfile>.Error(exception.Message);
            }

            var timeoutTask = RealtimeDelay.Delay(SignInTimeout);
            var completedTask = await Task.WhenAny(signInCompletion.Task, timeoutTask);
            if (completedTask == timeoutTask)
            {
                if (ReferenceEquals(pendingSignIn, signInCompletion))
                {
                    pendingSignIn = null;
                }

                return PlatformServiceResult<AuthorizationProfile>.Error(
                    "Ответ авторизации не получен вовремя.");
            }

            var profile = await signInCompletion.Task;
            if (ReferenceEquals(pendingSignIn, signInCompletion))
            {
                pendingSignIn = null;
            }

            return profile.IsAuthorized
                ? PlatformServiceResult<AuthorizationProfile>.Success(profile)
                : PlatformServiceResult<AuthorizationProfile>.Denied(
                    "Авторизация отменена.");
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            RemoveSdkDataHandler();
            authDialogClosedEvent?.RemoveEventHandler(null, authDialogClosedHandler);
            pendingSignIn?.TrySetResult(currentProfile);
            pendingSignIn = null;
        }

        private void HandleSdkDataReceived()
        {
            var profile = ReadProfile();
            if (!currentProfile.HasSameIdentity(profile))
            {
                currentProfile = profile;
                ProfileChanged?.Invoke(this, new AuthorizationProfileChangedEventArgs(currentProfile));
            }

            // Вход завершён успешно, как только профиль стал авторизованным.
            if (currentProfile.IsAuthorized)
            {
                pendingSignIn?.TrySetResult(currentProfile);
            }
        }

        private void HandleAuthDialogClosed()
        {
            // Окно закрыто (вошли или отменили): обновляем профиль и завершаем ожидание.
            HandleSdkDataReceived();
            pendingSignIn?.TrySetResult(currentProfile);
        }

        private AuthorizationProfile ReadProfile()
        {
            var player = ReadStaticMember("player");
            if (player == null || !ReadInstanceBool(player, "auth"))
            {
                return AuthorizationProfile.Guest();
            }

            return new AuthorizationProfile(
                true,
                ReadInstanceString(player, "id"),
                ReadInstanceString(player, "name"),
                ReadInstanceString(player, "photo"));
        }

        private void AddSdkDataHandler()
        {
            if (sdkDataEventField == null)
            {
                return;
            }

            var handlers = sdkDataEventField.GetValue(null) as Delegate;
            sdkDataEventField.SetValue(null, Delegate.Combine(handlers, sdkDataHandler));
        }

        private void RemoveSdkDataHandler()
        {
            if (sdkDataEventField == null)
            {
                return;
            }

            var handlers = sdkDataEventField.GetValue(null) as Delegate;
            sdkDataEventField.SetValue(null, Delegate.Remove(handlers, sdkDataHandler));
        }

        private bool ReadStaticBool(string memberName)
        {
            var value = ReadStaticMember(memberName);
            return value is bool result && result;
        }

        private object ReadStaticMember(string memberName)
        {
            var field = yg2Type?.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
            if (field != null)
            {
                return field.GetValue(null);
            }

            var property = yg2Type?.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
            return property?.GetValue(null);
        }

        private static bool ReadInstanceBool(object instance, string fieldName)
        {
            var value = instance.GetType().GetField(fieldName)?.GetValue(instance);
            return value is bool result && result;
        }

        private static string ReadInstanceString(object instance, string fieldName)
        {
            return instance.GetType().GetField(fieldName)?.GetValue(instance) as string
                ?? string.Empty;
        }
    }
}
