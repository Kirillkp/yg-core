using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using YgCore.Contracts;

namespace YgCore.Yandex
{
    /// <summary>
    /// Общий вызов возможностей платформы через мост <c>YgPlatformFeaturesBridge</c> для трёх сервисов
    /// (оценка игры, ярлык, серверное время). Вынесено сюда, чтобы разбор ответа и обработка сбоев были в одном месте.
    /// </summary>
    internal sealed class PluginYG2FeatureClient
    {
        public const string CanReview = "canReview";
        public const string RequestReview = "requestReview";
        public const string CanShowPrompt = "canShowPrompt";
        public const string ShowPrompt = "showPrompt";
        public const string ServerTime = "serverTime";

        /// <summary>Разобранный ответ JS-стороны. Поля заполняются разбором JSON, поэтому предупреждение компилятора о них не нужно.</summary>
#pragma warning disable 0649
        [Serializable]
        private sealed class Reply
        {
            public string id;
            public bool ok;
            public bool supported;
            public bool flag;
            public double number;
            public string reason;
            public string error;
        }
#pragma warning restore 0649

        /// <summary>Значение ответа: логический признак и число (например, миллисекунды времени).</summary>
        internal readonly struct Answer
        {
            public Answer(bool flag, double number)
            {
                Flag = flag;
                Number = number;
            }

            public bool Flag { get; }

            public double Number { get; }
        }

        private readonly MethodInfo isSdkReadyMethod;
        private readonly MethodInfo isSupportedMethod;
        private readonly MethodInfo callMethod;

        public PluginYG2FeatureClient()
        {
            var sdkBridgeType = PluginYG2BridgeReflection.ResolveBridgeType();
            var featuresBridgeType = PluginYG2BridgeReflection.ResolveType(
                PluginYG2BridgeReflection.FeaturesBridgeTypeName);
            isSdkReadyMethod = PluginYG2BridgeReflection.FindMethod(sdkBridgeType, "IsSdkReady");
            isSupportedMethod = PluginYG2BridgeReflection.FindMethod(featuresBridgeType, "IsSupported");
            callMethod = PluginYG2BridgeReflection.FindMethod(featuresBridgeType, "CallAsync");
        }

        /// <summary>SDK готов, мост на месте, и сам SDK умеет вызывать все перечисленные функции.</summary>
        public bool IsAvailable(params string[] features)
        {
            if (isSupportedMethod == null || callMethod == null
                || !PluginYG2BridgeReflection.InvokeBool(isSdkReadyMethod))
            {
                return false;
            }

            foreach (var feature in features)
            {
                if (!PluginYG2BridgeReflection.InvokeBool(isSupportedMethod, feature))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Вызывает возможность. Нет моста, SDK не готов или у SDK нет функции: Unavailable.
        /// Сбой SDK или таймаут: Error. Иначе Success со значением ответа.
        /// </summary>
        public async Task<PlatformServiceResult<Answer>> CallAsync(string feature, TimeSpan timeout)
        {
            if (!IsAvailable(feature))
            {
                return PlatformServiceResult<Answer>.Unavailable(
                    "SDK платформы не поддерживает: " + feature + ".");
            }

            string json;
            try
            {
                var task = (Task<string>)callMethod.Invoke(
                    null,
                    new object[] { feature, (int)Math.Ceiling(timeout.TotalSeconds) });
                json = await task;
            }
            catch (Exception exception)
            {
                return PlatformServiceResult<Answer>.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }

            Reply reply;
            try
            {
                reply = JsonUtility.FromJson<Reply>(json);
            }
            catch (ArgumentException exception)
            {
                return PlatformServiceResult<Answer>.Error(
                    "Повреждённый ответ SDK: " + exception.Message);
            }

            if (reply == null)
            {
                return PlatformServiceResult<Answer>.Error("Пустой ответ SDK.");
            }

            if (!reply.ok)
            {
                return reply.supported
                    ? PlatformServiceResult<Answer>.Error(reply.error)
                    : PlatformServiceResult<Answer>.Unavailable(reply.error);
            }

            return PlatformServiceResult<Answer>.Success(
                new Answer(reply.flag, reply.number),
                reply.reason);
        }
    }
}
