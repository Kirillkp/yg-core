using UnityEngine;
using YgCore.Core;

namespace YgCore.Audio
{
    /// <summary>
    /// Простой способ подчинить настройкам игрока источники звука, которые лежат прямо в сцене:
    /// перечислите их в двух списках (музыка и эффекты), и компонент будет приглушать их и менять громкость
    /// по настройкам. Для звука игры используйте <see cref="AudioDirector"/>: он делает то же самое сам.
    /// </summary>
    public sealed class AudioSettingsApplier : MonoBehaviour
    {
        // Заполняются в инспекторе Unity, поэтому предупреждение компилятора не нужно.
#pragma warning disable 0649
        [SerializeField] private AudioSource[] musicSources;
        [SerializeField] private AudioSource[] sfxSources;
#pragma warning restore 0649

        private IGameSettingsService settingsService;

        /// <summary>Подключает компонент к настройкам и сразу применяет их. Повторный вызов подключает другой сервис.</summary>
        public void Initialize(IGameSettingsService gameSettingsService)
        {
            if (settingsService != null)
            {
                settingsService.SettingsChanged -= HandleSettingsChanged;
            }

            settingsService = gameSettingsService;
            if (settingsService != null)
            {
                settingsService.SettingsChanged += HandleSettingsChanged;
            }

            ApplySettings();
        }

        private void OnDestroy()
        {
            if (settingsService != null)
            {
                settingsService.SettingsChanged -= HandleSettingsChanged;
            }
        }

        private void HandleSettingsChanged(object sender, GameSettingsChangedEventArgs args)
        {
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (settingsService == null)
            {
                return;
            }

            ApplySources(musicSources, settingsService.IsMusicEnabled, settingsService.MusicVolume);
            ApplySources(sfxSources, settingsService.IsSfxEnabled, settingsService.SfxVolume);
        }

        private static void ApplySources(AudioSource[] sources, bool isEnabled, float volume)
        {
            if (sources == null)
            {
                return;
            }

            foreach (var source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                // Выключенная шина заглушается (mute), а не обнуляется громкостью: при включении громкость сохраняется.
                source.mute = !isEnabled;
                source.volume = Mathf.Clamp01(volume);
            }
        }
    }
}
