using System;
using System.Reflection;

namespace YgCore.Yandex
{
    /// <summary>
    /// Общие приёмы доступа к PluginYG2 и к мостам пакета через рефлексию.
    ///
    /// Почему рефлексия: PluginYG2 компилируется в Assembly-CSharp, а сборки с .asmdef (как эта)
    /// не могут ссылаться на Assembly-CSharp напрямую. Мосты пакета лежат в Assembly-CSharp
    /// (папка Runtime/YandexBridge без .asmdef), поэтому доступ к ним тоже идёт по имени типа.
    /// </summary>
    internal static class PluginYG2BridgeReflection
    {
        /// <summary>Главный тип PluginYG2.</summary>
        internal const string Yg2TypeName = "YG.YG2";

        /// <summary>Мост событий и вызовов SDK (реклама, лидерборд, готовность, жизненный цикл).</summary>
        internal const string TypeName = "YgCore.YandexBridge.YgSdkBridge";

        /// <summary>Мост покупок с подтверждением (токены покупок).</summary>
        internal const string PaymentsBridgeTypeName = "YgCore.YandexBridge.YgPaymentsBridge";

        /// <summary>Мост хранилища с подтверждением записи.</summary>
        internal const string StorageBridgeTypeName = "YgCore.YandexBridge.YgStorageBridge";

        /// <summary>Находит тип моста SDK среди загруженных сборок или возвращает null, если его нет.</summary>
        public static Type ResolveBridgeType()
        {
            return ResolveType(TypeName);
        }

        /// <summary>Ищет тип по полному имени во всех загруженных сборках.</summary>
        public static Type ResolveType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        public static MethodInfo FindMethod(Type bridgeType, string methodName)
        {
            return bridgeType?.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
        }

        public static EventInfo FindEvent(Type bridgeType, string eventName)
        {
            return bridgeType?.GetEvent(
                eventName,
                BindingFlags.Public | BindingFlags.Static);
        }

        public static bool InvokeBool(MethodInfo method, params object[] arguments)
        {
            return method?.Invoke(null, arguments) is bool result && result;
        }

        /// <summary>Достаёт настоящее сообщение из исключения, обёрнутого рефлексией.</summary>
        public static string UnwrapMessage(Exception exception)
        {
            return exception is TargetInvocationException invocationException
                && invocationException.InnerException != null
                    ? invocationException.InnerException.Message
                    : exception.Message;
        }
    }
}
