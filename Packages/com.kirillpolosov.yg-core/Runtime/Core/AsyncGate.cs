using System.Collections.Generic;
using System.Threading.Tasks;

namespace YgCore.Core
{
    /// <summary>
    /// Асинхронный замок «один за раз» с очередью, для основного потока Unity.
    ///
    /// Зачем вместо <c>SemaphoreSlim</c>: в WebGL один поток и нет пула потоков. Ожидание занятого семафора
    /// (<c>WaitAsync</c> при занятом замке) возобновляется через пул потоков, и в WebGL такое ожидание может не
    /// возобновиться никогда: например, две записи сохранения подряд, вторая ждёт первую. Этот замок возобновляет
    /// ждущих сразу, в порядке очереди, и не использует потоки.
    ///
    /// Не потокобезопасен: вызывайте только из основного потока Unity (так работают все сервисы пакета).
    /// Использование: <c>await gate.WaitAsync(); try { ... } finally { gate.Release(); }</c>.
    /// </summary>
    public sealed class AsyncGate
    {
        private readonly Queue<TaskCompletionSource<bool>> waiters = new Queue<TaskCompletionSource<bool>>();
        private bool isHeld;

        /// <summary>Занят ли замок сейчас.</summary>
        public bool IsHeld => isHeld;

        /// <summary>Сколько операций ждут своей очереди.</summary>
        public int WaitingCount => waiters.Count;

        /// <summary>Ждёт, пока замок освободится, и занимает его. Свободный замок занимается сразу.</summary>
        public Task WaitAsync()
        {
            if (!isHeld)
            {
                isHeld = true;
                return Task.CompletedTask;
            }

            var waiter = new TaskCompletionSource<bool>();
            waiters.Enqueue(waiter);
            return waiter.Task;
        }

        /// <summary>Освобождает замок: он сразу переходит к первому в очереди или становится свободным.</summary>
        public void Release()
        {
            if (!isHeld)
            {
                return;
            }

            if (waiters.Count > 0)
            {
                // Замок остаётся занятым и передаётся следующему в очереди.
                waiters.Dequeue().TrySetResult(true);
                return;
            }

            isHeld = false;
        }
    }
}
