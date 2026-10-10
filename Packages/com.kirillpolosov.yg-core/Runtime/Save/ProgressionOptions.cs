using System;

namespace YgCore.Save
{
    /// <summary>Настройки сервиса прогресса. Значения по умолчанию совпадают с «Обби Потоп».</summary>
    public sealed class ProgressionOptions
    {
        /// <summary>Ключ записи в хранилище. После выпуска игры менять нельзя: прогресс перестанет находиться.</summary>
        public string StorageKey = "player_progress";

        /// <summary>
        /// Через сколько собранные изменения записываются одной записью. Изменения за это время
        /// объединяются, чтобы не писать в облако на каждый чих.
        /// </summary>
        public TimeSpan BatchDelay = TimeSpan.FromSeconds(1.5d);

        /// <summary>Через сколько повторять запись после неудачи.</summary>
        public TimeSpan RetryDelay = TimeSpan.FromSeconds(10d);

        /// <summary>
        /// Предельный размер записи в байтах (UTF-8). Платформа ограничивает объём данных игрока,
        /// поэтому слишком большая запись не отправляется, чтобы не потерять сохранение целиком.
        /// По умолчанию 190 КБ при лимите платформы 200 КБ.
        /// </summary>
        public int MaximumPayloadBytes = 190 * 1024;
    }
}
