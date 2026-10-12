using System;
using YgCore.Core;

namespace YgCore.Save
{
    /// <summary>
    /// Готовый блок данных прогресса для настроек звука из модуля Core. Игра включает его полем
    /// в свой класс данных и вызывает <see cref="CaptureFrom"/> при записи и <see cref="ApplyTo"/> при загрузке.
    /// Имена полей совпадают с «Обби Потоп», поэтому его сохранения читаются без миграции.
    /// </summary>
    [Serializable]
    public sealed class SettingsProgressData
    {
        public bool musicEnabled = true;
        public bool sfxEnabled = true;
        public float musicVolume = SessionGameSettingsService.DefaultMusicVolume;
        public float sfxVolume = SessionGameSettingsService.DefaultSfxVolume;

        /// <summary>Собирает текущие настройки. Если сервиса нет, возвращает значения по умолчанию.</summary>
        public static SettingsProgressData CaptureFrom(IGameSettingsService settings)
        {
            return new SettingsProgressData
            {
                musicEnabled = settings?.IsMusicEnabled ?? true,
                sfxEnabled = settings?.IsSfxEnabled ?? true,
                musicVolume = settings?.MusicVolume ?? SessionGameSettingsService.DefaultMusicVolume,
                sfxVolume = settings?.SfxVolume ?? SessionGameSettingsService.DefaultSfxVolume
            };
        }

        /// <summary>
        /// Копия с громкостями, ограниченными диапазоном 0–1. Пустой источник даёт значения по умолчанию.
        /// Громкость <c>NaN</c> заменяется значением по умолчанию (<c>Mathf.Clamp01</c> её пропускает).
        /// </summary>
        public static SettingsProgressData Normalized(SettingsProgressData source)
        {
            source = source ?? new SettingsProgressData();
            return new SettingsProgressData
            {
                musicEnabled = source.musicEnabled,
                sfxEnabled = source.sfxEnabled,
                musicVolume = SessionGameSettingsService.SanitizeVolume(
                    source.musicVolume, SessionGameSettingsService.DefaultMusicVolume),
                sfxVolume = SessionGameSettingsService.SanitizeVolume(
                    source.sfxVolume, SessionGameSettingsService.DefaultSfxVolume)
            };
        }

        /// <summary>Применяет сохранённые настройки к сервису. Сервис всегда сообщает об изменении.</summary>
        public void ApplyTo(SessionGameSettingsService settings)
        {
            settings?.Restore(musicEnabled, sfxEnabled, musicVolume, sfxVolume);
        }
    }
}
