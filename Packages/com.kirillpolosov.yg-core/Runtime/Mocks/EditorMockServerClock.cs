using System;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок серверного времени. По умолчанию это системные часы UTC, но время можно зафиксировать
    /// и двигать вручную: так проверяются границы суток и подмена времени.
    /// </summary>
    public sealed class EditorMockServerClock : EditorMockPlatformService, IServerClock
    {
        private DateTime? fixedUtcNow;

        /// <summary>Текущее время мока в UTC (фиксированное, если задано <see cref="SetUtcNow"/>).</summary>
        public DateTime UtcNow => fixedUtcNow ?? DateTime.UtcNow;

        /// <summary>Фиксирует время мока. Часы перестают идти до следующего вызова <see cref="Advance"/> или <see cref="Release"/>.</summary>
        public void SetUtcNow(DateTime utcNow)
        {
            fixedUtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }

        /// <summary>Сдвигает время мока вперёд (или назад при отрицательном значении), фиксируя его.</summary>
        public void Advance(TimeSpan delta)
        {
            fixedUtcNow = UtcNow + delta;
        }

        /// <summary>Возвращает мок к системным часам.</summary>
        public void Release()
        {
            fixedUtcNow = null;
        }

        public Task<PlatformServiceResult<DateTime>> GetUtcNowAsync()
        {
            return Task.FromResult(BuildResult(UtcNow, "Мок: серверное время."));
        }
    }
}
