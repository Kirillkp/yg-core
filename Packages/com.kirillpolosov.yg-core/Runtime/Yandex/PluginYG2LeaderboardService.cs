using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using YgCore.Contracts;
using YgCore.Core;

namespace YgCore.Yandex
{
    /// <summary>
    /// Таблицы лидеров через PluginYG2. Результат можно отправить только вошедшему игроку:
    /// иначе возвращается Denied. Чтение топа выполняется по одному запросу за раз.
    /// </summary>
    public sealed class PluginYG2LeaderboardService : ILeaderboardService, IDisposable
    {
        // Формат ответа моста (JSON). Поля должны совпадать с классами в YgSdkBridge.
        // Поля заполняются при разборе JSON, поэтому предупреждение компилятора о них не нужно.
#pragma warning disable 0649
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
#pragma warning restore 0649

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15d);

        private readonly MethodInfo isSdkReadyMethod;
        private readonly MethodInfo submitScoreMethod;
        private readonly MethodInfo requestLeaderboardMethod;
        private readonly EventInfo leaderboardReceivedEvent;
        private readonly Action<string> leaderboardReceivedHandler;
        private TaskCompletionSource<PlatformServiceResult<List<LeaderboardEntry>>>
            pendingRequest;
        private bool isDisposed;

        public PluginYG2LeaderboardService()
        {
            var bridgeType = PluginYG2BridgeReflection.ResolveBridgeType();
            isSdkReadyMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "IsSdkReady");
            submitScoreMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "SubmitScore");
            requestLeaderboardMethod = PluginYG2BridgeReflection.FindMethod(
                bridgeType,
                "RequestLeaderboard");
            leaderboardReceivedEvent = PluginYG2BridgeReflection.FindEvent(
                bridgeType,
                "LeaderboardReceived");
            leaderboardReceivedHandler = HandleLeaderboardReceived;
            leaderboardReceivedEvent?.AddEventHandler(null, leaderboardReceivedHandler);
        }

        public bool IsPlatformAvailable => !isDisposed
            && PluginYG2BridgeReflection.InvokeBool(isSdkReadyMethod)
            && submitScoreMethod != null
            && requestLeaderboardMethod != null;

        public Task<PlatformServiceResult> SubmitScoreAsync(
            string leaderboardId,
            long score)
        {
            if (!IsPlatformAvailable)
            {
                return Task.FromResult(
                    PlatformServiceResult.Unavailable(
                        "Таблицы лидеров PluginYG2 недоступны."));
            }

            if (string.IsNullOrWhiteSpace(leaderboardId))
            {
                return Task.FromResult(
                    PlatformServiceResult.Error("Идентификатор таблицы пуст."));
            }

            try
            {
                // PluginYG2 принимает очки как int: отрицательные превращаем в 0, большие обрезаем до int.MaxValue.
                var clampedScore = (int)Math.Max(0, Math.Min(int.MaxValue, score));
                var submitted = PluginYG2BridgeReflection.InvokeBool(
                    submitScoreMethod,
                    leaderboardId,
                    clampedScore);
                return Task.FromResult(
                    submitted
                        ? PlatformServiceResult.Success("Результат отправлен.")
                        : PlatformServiceResult.Denied(
                            "Для отправки результата нужна авторизация."));
            }
            catch (Exception exception)
            {
                return Task.FromResult(
                    PlatformServiceResult.Error(
                        PluginYG2BridgeReflection.UnwrapMessage(exception)));
            }
        }

        public async Task<PlatformServiceResult<List<LeaderboardEntry>>> GetTopAsync(
            string leaderboardId,
            int limit)
        {
            if (!IsPlatformAvailable)
            {
                return PlatformServiceResult<List<LeaderboardEntry>>.Unavailable(
                    "Таблицы лидеров PluginYG2 недоступны.");
            }

            if (pendingRequest != null)
            {
                return PlatformServiceResult<List<LeaderboardEntry>>.Error(
                    "Запрос таблицы лидеров уже выполняется.");
            }

            try
            {
                pendingRequest =
                    new TaskCompletionSource<PlatformServiceResult<List<LeaderboardEntry>>>();
                if (!PluginYG2BridgeReflection.InvokeBool(
                        requestLeaderboardMethod,
                        leaderboardId,
                        Math.Max(1, limit)))
                {
                    pendingRequest = null;
                    return PlatformServiceResult<List<LeaderboardEntry>>.Error(
                        "Запрос таблицы лидеров отклонён.");
                }

                var request = pendingRequest;
                var completed = await Task.WhenAny(
                    request.Task,
                    RealtimeDelay.Delay(RequestTimeout));
                if (completed != request.Task)
                {
                    return PlatformServiceResult<List<LeaderboardEntry>>.Error(
                        "Запрос таблицы лидеров не завершился вовремя.");
                }

                return await request.Task;
            }
            catch (Exception exception)
            {
                return PlatformServiceResult<List<LeaderboardEntry>>.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
            finally
            {
                pendingRequest = null;
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            leaderboardReceivedEvent?.RemoveEventHandler(
                null,
                leaderboardReceivedHandler);
            pendingRequest?.TrySetResult(
                PlatformServiceResult<List<LeaderboardEntry>>.Unavailable(
                    "Сервис таблиц лидеров выгружен."));
            pendingRequest = null;
        }

        private void HandleLeaderboardReceived(string json)
        {
            try
            {
                var payload = JsonUtility.FromJson<LeaderboardPayload>(json)
                    ?? new LeaderboardPayload();
                var entries = new List<LeaderboardEntry>();
                foreach (var item in payload.entries)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    entries.Add(new LeaderboardEntry(
                        item.playerId,
                        item.displayName,
                        item.score,
                        item.rank));
                }

                pendingRequest?.TrySetResult(
                    PlatformServiceResult<List<LeaderboardEntry>>.Success(entries));
            }
            catch (Exception exception)
            {
                pendingRequest?.TrySetResult(
                    PlatformServiceResult<List<LeaderboardEntry>>.Error(
                        exception.Message));
            }
        }
    }
}
