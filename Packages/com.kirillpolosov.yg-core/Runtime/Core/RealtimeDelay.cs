using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YgCore.Core
{
    /// <summary>
    /// Асинхронная задержка в реальном времени, не зависящая от паузы игры.
    /// Нужна для таймаутов запросов к платформе (например, подтверждение сохранения).
    /// </summary>
    public static class RealtimeDelay
    {
        public static Task Delay(TimeSpan delay, CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WaitForFramesAsync(delay, cancellationToken);
#else
            return Task.Delay(delay, cancellationToken);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static async Task WaitForFramesAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deadline = Time.realtimeSinceStartupAsDouble + delay.TotalSeconds;

            // В WebGL нет управляемых потоков-таймеров, поэтому ждём по кадрам.
            // Используем реальное время, чтобы задержка шла и во время паузы игры.
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                await Awaitable.NextFrameAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
#endif
    }
}
