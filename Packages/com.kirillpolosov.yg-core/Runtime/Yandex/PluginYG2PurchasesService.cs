using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YgCore.Contracts;
using YgCore.Core;

namespace YgCore.Yandex
{
    /// <summary>
    /// Покупки через PluginYG2 и собственный мост с токенами покупок (YgPaymentsBridge).
    ///
    /// Зачем мост: штатный PluginYG2 гасит покупку (consume) сразу после оплаты, до того как игра выдала награду,
    /// и не ждёт результата. Здесь покупка гасится только по явному <see cref="ConsumeAsync"/>: игра сначала выдаёт
    /// и сохраняет награду, затем подтверждает. Невыданные покупки возвращает <see cref="RestorePurchasesAsync"/>.
    ///
    /// Все операции выполняются по одной за раз (блокировка), у каждой есть таймаут.
    /// Каталог берётся из <c>YG2.purchases</c>. Признак «расходуемый» PluginYG2 не сообщает,
    /// поэтому <see cref="PurchaseProduct.IsConsumable"/> здесь всегда false: тип товара знает игра.
    /// </summary>
    public sealed class PluginYG2PurchasesService : IPurchasesService, IDisposable
    {
        private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(20d);
        private static readonly TimeSpan PurchaseTimeout = TimeSpan.FromMinutes(3d);

        private readonly Type yg2Type;
        private readonly Type bridgeType;
        private readonly FieldInfo paymentsReadyEventField;
        private readonly MethodInfo bridgeIsReadyMethod;
        private readonly MethodInfo purchaseMethod;
        private readonly MethodInfo loadPurchasesMethod;
        private readonly MethodInfo consumeMethod;
        private readonly EventInfo purchaseSucceededEvent;
        private readonly EventInfo purchaseFailedEvent;
        private readonly EventInfo purchasesLoadedEvent;
        private readonly EventInfo purchasesLoadFailedEvent;
        private readonly EventInfo consumeSucceededEvent;
        private readonly EventInfo consumeFailedEvent;
        private readonly Action paymentsReadyHandler;
        private readonly Action<string> purchaseSucceededHandler;
        private readonly Action<string> purchaseFailedHandler;
        private readonly Action<string> purchasesLoadedHandler;
        private readonly Action<string> purchasesLoadFailedHandler;
        private readonly Action<string> consumeSucceededHandler;
        private readonly Action<string> consumeFailedHandler;
        private readonly AsyncGate operationLock = new AsyncGate();

        private TaskCompletionSource<bool> pendingReadiness;
        private TaskCompletionSource<PlatformServiceResult<PurchaseReceipt>> pendingPurchase;
        private TaskCompletionSource<PlatformServiceResult<List<PurchaseReceipt>>> pendingRestore;
        private TaskCompletionSource<PlatformServiceResult> pendingConsume;
        private string pendingConsumeToken;
        private bool isDisposed;

        public PluginYG2PurchasesService()
        {
            yg2Type = PluginYG2BridgeReflection.ResolveType(PluginYG2BridgeReflection.Yg2TypeName);
            bridgeType = PluginYG2BridgeReflection.ResolveType(
                PluginYG2BridgeReflection.PaymentsBridgeTypeName);
            paymentsReadyEventField = yg2Type?.GetField(
                "onGetPayments",
                BindingFlags.Public | BindingFlags.Static);
            bridgeIsReadyMethod = FindBridgeMethod("IsReady");
            purchaseMethod = FindBridgeMethod("Purchase");
            loadPurchasesMethod = FindBridgeMethod("LoadPurchases");
            consumeMethod = FindBridgeMethod("Consume");

            purchaseSucceededEvent = FindBridgeEvent("PurchaseSucceeded");
            purchaseFailedEvent = FindBridgeEvent("PurchaseFailed");
            purchasesLoadedEvent = FindBridgeEvent("PurchasesLoaded");
            purchasesLoadFailedEvent = FindBridgeEvent("PurchasesLoadFailed");
            consumeSucceededEvent = FindBridgeEvent("ConsumeSucceeded");
            consumeFailedEvent = FindBridgeEvent("ConsumeFailed");

            paymentsReadyHandler = HandlePaymentsReady;
            purchaseSucceededHandler = HandlePurchaseSucceeded;
            purchaseFailedHandler = HandlePurchaseFailed;
            purchasesLoadedHandler = HandlePurchasesLoaded;
            purchasesLoadFailedHandler = HandlePurchasesLoadFailed;
            consumeSucceededHandler = HandleConsumeSucceeded;
            consumeFailedHandler = HandleConsumeFailed;

            AddPaymentsReadyHandler();
            AddBridgeHandlers();
        }

        public bool IsPlatformAvailable => !isDisposed
            && yg2Type != null
            && bridgeType != null
            && bridgeIsReadyMethod != null
            && purchaseMethod != null
            && loadPurchasesMethod != null
            && consumeMethod != null
            && ReadStaticBool("isSDKEnabled");

        public async Task<PlatformServiceResult<List<PurchaseProduct>>> GetCatalogAsync()
        {
            var readiness = await EnsurePaymentsReadyAsync();
            if (!readiness.IsSuccess)
            {
                return ConvertFailure<List<PurchaseProduct>>(readiness);
            }

            try
            {
                var result = new List<PurchaseProduct>();
                var purchasesField = yg2Type.GetField(
                    "purchases",
                    BindingFlags.Public | BindingFlags.Static);
                var purchases = purchasesField?.GetValue(null) as IEnumerable;
                if (purchases != null)
                {
                    foreach (var purchase in purchases)
                    {
                        var id = ReadString(purchase, "id");
                        if (string.IsNullOrWhiteSpace(id))
                        {
                            continue;
                        }

                        result.Add(new PurchaseProduct(
                            id,
                            ReadString(purchase, "title"),
                            ReadString(purchase, "price"),
                            ReadString(purchase, "priceCurrencyCode"),
                            false,
                            ReadString(purchase, "currencyImageURL")));
                    }
                }

                return PlatformServiceResult<List<PurchaseProduct>>.Success(result);
            }
            catch (Exception exception)
            {
                return PlatformServiceResult<List<PurchaseProduct>>.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
        }

        public async Task<PlatformServiceResult<PurchaseReceipt>> PurchaseAsync(
            string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                return PlatformServiceResult<PurchaseReceipt>.Error(
                    "Идентификатор товара пуст.");
            }

            await operationLock.WaitAsync();
            try
            {
                var readiness = await EnsurePaymentsReadyAsync();
                if (!readiness.IsSuccess)
                {
                    return ConvertFailure<PurchaseReceipt>(readiness);
                }

                pendingPurchase =
                    new TaskCompletionSource<PlatformServiceResult<PurchaseReceipt>>();
                purchaseMethod.Invoke(null, new object[] { productId });
                return await WaitForResultAsync(
                    pendingPurchase,
                    PurchaseTimeout,
                    PlatformServiceResult<PurchaseReceipt>.Error(
                        "Ответ покупки не получен вовремя."));
            }
            catch (Exception exception)
            {
                return PlatformServiceResult<PurchaseReceipt>.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
            finally
            {
                pendingPurchase = null;
                operationLock.Release();
            }
        }

        public async Task<PlatformServiceResult<List<PurchaseReceipt>>> RestorePurchasesAsync()
        {
            await operationLock.WaitAsync();
            try
            {
                var readiness = await EnsurePaymentsReadyAsync();
                if (!readiness.IsSuccess)
                {
                    return ConvertFailure<List<PurchaseReceipt>>(readiness);
                }

                pendingRestore =
                    new TaskCompletionSource<PlatformServiceResult<List<PurchaseReceipt>>>();
                loadPurchasesMethod.Invoke(null, null);
                return await WaitForResultAsync(
                    pendingRestore,
                    OperationTimeout,
                    PlatformServiceResult<List<PurchaseReceipt>>.Error(
                        "Загрузка невыданных покупок не завершилась вовремя."));
            }
            catch (Exception exception)
            {
                return PlatformServiceResult<List<PurchaseReceipt>>.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
            finally
            {
                pendingRestore = null;
                operationLock.Release();
            }
        }

        public async Task<PlatformServiceResult> ConsumeAsync(string purchaseToken)
        {
            if (string.IsNullOrWhiteSpace(purchaseToken))
            {
                return PlatformServiceResult.Error("Токен покупки пуст.");
            }

            await operationLock.WaitAsync();
            try
            {
                var readiness = await EnsurePaymentsReadyAsync();
                if (!readiness.IsSuccess)
                {
                    return readiness;
                }

                pendingConsumeToken = purchaseToken;
                pendingConsume = new TaskCompletionSource<PlatformServiceResult>();
                consumeMethod.Invoke(null, new object[] { purchaseToken });
                return await WaitForResultAsync(
                    pendingConsume,
                    OperationTimeout,
                    PlatformServiceResult.Error("Подтверждение покупки не завершилось вовремя."));
            }
            catch (Exception exception)
            {
                return PlatformServiceResult.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
            finally
            {
                pendingConsume = null;
                pendingConsumeToken = null;
                operationLock.Release();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            RemovePaymentsReadyHandler();
            RemoveBridgeHandlers();
            pendingReadiness?.TrySetResult(false);
            pendingPurchase?.TrySetResult(
                PlatformServiceResult<PurchaseReceipt>.Unavailable(
                    "Сервис покупок выгружен."));
            pendingRestore?.TrySetResult(
                PlatformServiceResult<List<PurchaseReceipt>>.Unavailable(
                    "Сервис покупок выгружен."));
            pendingConsume?.TrySetResult(
                PlatformServiceResult.Unavailable("Сервис покупок выгружен."));
        }

        private async Task<PlatformServiceResult> EnsurePaymentsReadyAsync()
        {
            if (!IsPlatformAvailable)
            {
                return PlatformServiceResult.Unavailable(
                    "Покупки PluginYG2 недоступны.");
            }

            if (InvokeBridgeBool(bridgeIsReadyMethod))
            {
                return PlatformServiceResult.Success();
            }

            // Платежи ещё инициализируются: ждём событие готовности или таймаут.
            var completion = pendingReadiness ?? new TaskCompletionSource<bool>();
            pendingReadiness = completion;
            if (InvokeBridgeBool(bridgeIsReadyMethod))
            {
                completion.TrySetResult(true);
            }

            var completedTask = await Task.WhenAny(
                completion.Task,
                RealtimeDelay.Delay(OperationTimeout));
            if (ReferenceEquals(pendingReadiness, completion))
            {
                pendingReadiness = null;
            }

            return completedTask == completion.Task && await completion.Task
                ? PlatformServiceResult.Success()
                : PlatformServiceResult.Error(
                    "Инициализация покупок PluginYG2 не завершилась вовремя.");
        }

        private void HandlePaymentsReady()
        {
            pendingReadiness?.TrySetResult(InvokeBridgeBool(bridgeIsReadyMethod));
        }

        private void HandlePurchaseSucceeded(string payload)
        {
            var data = DeserializeReceipt(payload);

            // Квитанция без товара или токена бесполезна: без токена покупку нельзя подтвердить.
            if (data == null
                || string.IsNullOrWhiteSpace(data.productId)
                || string.IsNullOrWhiteSpace(data.token))
            {
                pendingPurchase?.TrySetResult(
                    PlatformServiceResult<PurchaseReceipt>.Error(
                        "PluginYG2 вернул некорректную квитанцию покупки."));
                return;
            }

            pendingPurchase?.TrySetResult(
                PlatformServiceResult<PurchaseReceipt>.Success(ToReceipt(data)));
        }

        private void HandlePurchaseFailed(string message)
        {
            pendingPurchase?.TrySetResult(
                PlatformServiceResult<PurchaseReceipt>.Denied(
                    string.IsNullOrWhiteSpace(message)
                        ? "Покупка отменена."
                        : message));
        }

        private void HandlePurchasesLoaded(string payload)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<ReceiptListPayload>(payload);
                var result = new List<PurchaseReceipt>();
                if (wrapper?.purchases != null)
                {
                    foreach (var data in wrapper.purchases)
                    {
                        if (data != null
                            && !string.IsNullOrWhiteSpace(data.productId)
                            && !string.IsNullOrWhiteSpace(data.token))
                        {
                            result.Add(ToReceipt(data));
                        }
                    }
                }

                pendingRestore?.TrySetResult(
                    PlatformServiceResult<List<PurchaseReceipt>>.Success(result));
            }
            catch (Exception exception)
            {
                pendingRestore?.TrySetResult(
                    PlatformServiceResult<List<PurchaseReceipt>>.Error(exception.Message));
            }
        }

        private void HandlePurchasesLoadFailed(string message)
        {
            pendingRestore?.TrySetResult(
                PlatformServiceResult<List<PurchaseReceipt>>.Error(message));
        }

        private void HandleConsumeSucceeded(string purchaseToken)
        {
            // Подтверждение принимаем только для того токена, который сейчас гасим:
            // запоздавший ответ по другой покупке не должен завершить текущую операцию.
            if (string.Equals(
                pendingConsumeToken,
                purchaseToken,
                StringComparison.Ordinal))
            {
                pendingConsume?.TrySetResult(PlatformServiceResult.Success());
            }
        }

        private void HandleConsumeFailed(string message)
        {
            pendingConsume?.TrySetResult(PlatformServiceResult.Error(message));
        }

        private void AddPaymentsReadyHandler()
        {
            if (paymentsReadyEventField == null)
            {
                return;
            }

            var handlers = paymentsReadyEventField.GetValue(null) as Delegate;
            paymentsReadyEventField.SetValue(
                null,
                Delegate.Combine(handlers, paymentsReadyHandler));
        }

        private void RemovePaymentsReadyHandler()
        {
            if (paymentsReadyEventField == null)
            {
                return;
            }

            var handlers = paymentsReadyEventField.GetValue(null) as Delegate;
            paymentsReadyEventField.SetValue(
                null,
                Delegate.Remove(handlers, paymentsReadyHandler));
        }

        private void AddBridgeHandlers()
        {
            purchaseSucceededEvent?.AddEventHandler(null, purchaseSucceededHandler);
            purchaseFailedEvent?.AddEventHandler(null, purchaseFailedHandler);
            purchasesLoadedEvent?.AddEventHandler(null, purchasesLoadedHandler);
            purchasesLoadFailedEvent?.AddEventHandler(null, purchasesLoadFailedHandler);
            consumeSucceededEvent?.AddEventHandler(null, consumeSucceededHandler);
            consumeFailedEvent?.AddEventHandler(null, consumeFailedHandler);
        }

        private void RemoveBridgeHandlers()
        {
            purchaseSucceededEvent?.RemoveEventHandler(null, purchaseSucceededHandler);
            purchaseFailedEvent?.RemoveEventHandler(null, purchaseFailedHandler);
            purchasesLoadedEvent?.RemoveEventHandler(null, purchasesLoadedHandler);
            purchasesLoadFailedEvent?.RemoveEventHandler(null, purchasesLoadFailedHandler);
            consumeSucceededEvent?.RemoveEventHandler(null, consumeSucceededHandler);
            consumeFailedEvent?.RemoveEventHandler(null, consumeFailedHandler);
        }

        private MethodInfo FindBridgeMethod(string methodName)
        {
            return bridgeType?.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
        }

        private EventInfo FindBridgeEvent(string eventName)
        {
            return bridgeType?.GetEvent(
                eventName,
                BindingFlags.Public | BindingFlags.Static);
        }

        private bool ReadStaticBool(string memberName)
        {
            var value = yg2Type?.GetProperty(
                memberName,
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (value == null)
            {
                value = yg2Type?.GetField(
                    memberName,
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            }

            return value is bool result && result;
        }

        private static bool InvokeBridgeBool(MethodInfo method)
        {
            return method?.Invoke(null, null) is bool result && result;
        }

        private static string ReadString(object instance, string fieldName)
        {
            return instance?.GetType().GetField(fieldName)?.GetValue(instance) as string
                ?? string.Empty;
        }

        private static ReceiptPayload DeserializeReceipt(string payload)
        {
            return string.IsNullOrWhiteSpace(payload)
                ? null
                : JsonUtility.FromJson<ReceiptPayload>(payload);
        }

        private static PurchaseReceipt ToReceipt(ReceiptPayload data)
        {
            // Если дату покупки не удалось разобрать, берём текущее время.
            var purchasedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(data.purchasedAt)
                && DateTime.TryParse(
                    data.purchasedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                purchasedAt = parsed.ToUniversalTime();
            }

            return new PurchaseReceipt(
                data.productId,
                data.token,
                false,
                purchasedAt);
        }

        private static async Task<T> WaitForResultAsync<T>(
            TaskCompletionSource<T> completion,
            TimeSpan timeout,
            T timeoutResult)
        {
            var completedTask = await Task.WhenAny(
                completion.Task,
                RealtimeDelay.Delay(timeout));
            return completedTask == completion.Task
                ? await completion.Task
                : timeoutResult;
        }

        private static PlatformServiceResult<T> ConvertFailure<T>(
            PlatformServiceResult result)
        {
            return result.Status == PlatformServiceStatus.PlatformUnavailable
                ? PlatformServiceResult<T>.Unavailable(result.Message)
                : PlatformServiceResult<T>.Error(result.Message);
        }

        // Формат JSON, который присылает мост покупок. Поля заполняются при разборе JSON,
        // поэтому предупреждение компилятора о них не нужно.
#pragma warning disable 0649
        [Serializable]
        private sealed class ReceiptPayload
        {
            public string productId;
            public string token;
            public string purchasedAt;
        }

        [Serializable]
        private sealed class ReceiptListPayload
        {
            public ReceiptPayload[] purchases;
        }
#pragma warning restore 0649
    }
}
