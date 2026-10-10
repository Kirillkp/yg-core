// Файл установлен пакетом com.kirillpolosov.yg-core v0.0.0 (YgCore/Переустановить мосты Яндекса).
// Не правьте его в игре: при обновлении пакета он будет перезаписан.
// Папка без .asmdef: код компилируется в Assembly-CSharp рядом с PluginYG2 (см. YgSdkBridge.cs).
// Включается только при установленном PluginYG2 и включённом модуле Payments.
#if PLUGIN_YG_2 && Payments_yg
using System;
using System.Runtime.InteropServices;
using UnityEngine.Scripting;

namespace YgCore.YandexBridge
{
    /// <summary>
    /// Мост покупок с токенами. В отличие от штатного PluginYG2, покупка не гасится автоматически после оплаты:
    /// игра сначала выдаёт награду, затем явно вызывает <see cref="Consume"/> с токеном.
    /// Результаты приходят событиями; ответы JS-стороны возвращаются через YGSendMessage (нижний класс файла).
    /// </summary>
    [Preserve]
    public static class YgPaymentsBridge
    {
        [Preserve] public static event Action<string> PurchaseSucceeded;
        [Preserve] public static event Action<string> PurchaseFailed;
        [Preserve] public static event Action<string> PurchasesLoaded;
        [Preserve] public static event Action<string> PurchasesLoadFailed;
        [Preserve] public static event Action<string> ConsumeSucceeded;
        [Preserve] public static event Action<string> ConsumeFailed;

        /// <summary>Готов ли объект платежей на стороне JS.</summary>
        [Preserve]
        public static bool IsReady()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return YgCorePaymentsIsReady() != 0;
#else
            return false;
#endif
        }

        /// <summary>Запускает оплату товара. Итог придёт событием PurchaseSucceeded или PurchaseFailed.</summary>
        [Preserve]
        public static void Purchase(string productId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YgCorePaymentsPurchase(productId);
#else
            RaisePurchaseFailed("Мост покупок PluginYG2 работает только в WebGL.");
#endif
        }

        /// <summary>Запрашивает невыданные покупки. Итог придёт событием PurchasesLoaded или PurchasesLoadFailed.</summary>
        [Preserve]
        public static void LoadPurchases()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YgCorePaymentsLoadPurchases();
#else
            RaisePurchasesLoadFailed("Мост покупок PluginYG2 работает только в WebGL.");
#endif
        }

        /// <summary>Подтверждает выдачу покупки по токену. Итог придёт событием ConsumeSucceeded или ConsumeFailed.</summary>
        [Preserve]
        public static void Consume(string purchaseToken)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            YgCorePaymentsConsume(purchaseToken);
#else
            RaiseConsumeFailed("Мост покупок PluginYG2 работает только в WebGL.");
#endif
        }

        public static void RaisePurchaseSucceeded(string payload)
        {
            PurchaseSucceeded?.Invoke(payload);
        }

        public static void RaisePurchaseFailed(string message)
        {
            PurchaseFailed?.Invoke(message);
        }

        public static void RaisePurchasesLoaded(string payload)
        {
            PurchasesLoaded?.Invoke(payload);
        }

        public static void RaisePurchasesLoadFailed(string message)
        {
            PurchasesLoadFailed?.Invoke(message);
        }

        public static void RaiseConsumeSucceeded(string purchaseToken)
        {
            ConsumeSucceeded?.Invoke(purchaseToken);
        }

        public static void RaiseConsumeFailed(string message)
        {
            ConsumeFailed?.Invoke(message);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Функции из Plugins/YgCorePayments.jslib.
        [DllImport("__Internal")]
        private static extern int YgCorePaymentsIsReady();

        [DllImport("__Internal")]
        private static extern void YgCorePaymentsPurchase(string productId);

        [DllImport("__Internal")]
        private static extern void YgCorePaymentsLoadPurchases();

        [DllImport("__Internal")]
        private static extern void YgCorePaymentsConsume(string purchaseToken);
#endif
    }
}

namespace YG.Insides
{
    /// <summary>Приём ответов JS-стороны покупок. Имена методов используются в YgCorePayments.jslib.</summary>
    public partial class YGSendMessage
    {
        [Preserve]
        public void YgCorePaymentsPurchaseSucceeded(string payload)
        {
            YgCore.YandexBridge.YgPaymentsBridge.RaisePurchaseSucceeded(payload);
        }

        [Preserve]
        public void YgCorePaymentsPurchaseFailed(string message)
        {
            YgCore.YandexBridge.YgPaymentsBridge.RaisePurchaseFailed(message);
        }

        [Preserve]
        public void YgCorePaymentsPurchasesLoaded(string payload)
        {
            YgCore.YandexBridge.YgPaymentsBridge.RaisePurchasesLoaded(payload);
        }

        [Preserve]
        public void YgCorePaymentsPurchasesLoadFailed(string message)
        {
            YgCore.YandexBridge.YgPaymentsBridge.RaisePurchasesLoadFailed(message);
        }

        [Preserve]
        public void YgCorePaymentsConsumeSucceeded(string purchaseToken)
        {
            YgCore.YandexBridge.YgPaymentsBridge.RaiseConsumeSucceeded(purchaseToken);
        }

        [Preserve]
        public void YgCorePaymentsConsumeFailed(string message)
        {
            YgCore.YandexBridge.YgPaymentsBridge.RaiseConsumeFailed(message);
        }
    }
}
#endif
