using System;
using System.Collections.Generic;
using YgCore.Core;

namespace YgCore.Audio
{
    /// <summary>
    /// Правила звука в виде чистых функций (без Unity-объектов), чтобы их можно было проверять без редактора.
    /// Директор звука только применяет результат к источникам.
    /// </summary>
    public static class AudioPolicy
    {
        /// <summary>
        /// Громкость шины (музыки или эффектов) из настроек игрока. Громкость 0-1 возводится в степень кривой,
        /// чтобы середина ползунка звучала тише линейной: слух воспринимает громкость нелинейно.
        /// Если шина выключена, результат 0.
        /// </summary>
        /// <param name="isEnabled">Включена ли шина в настройках.</param>
        /// <param name="volume">Громкость игрока от 0 до 1.</param>
        /// <param name="curve">Степень кривой (1 даёт линейную громкость).</param>
        /// <param name="baseVolume">Базовая громкость шины, общий потолок.</param>
        public static float BusVolume(bool isEnabled, float volume, float curve, float baseVolume)
        {
            if (!isEnabled)
            {
                return 0f;
            }

            var clamped = volume < 0f ? 0f : (volume > 1f ? 1f : volume);
            return (float)Math.Pow(clamped, curve) * baseVolume;
        }

        /// <summary>
        /// Во сколько раз приглушить музыку в состоянии игры: на экране результата и на паузе музыка тише,
        /// чтобы заставка и меню паузы были слышны.
        /// </summary>
        public static float MusicDuck(AppState state, float resultDuck, float pauseDuck)
        {
            return state == AppState.Result
                ? resultDuck
                : (state == AppState.Pause ? pauseDuck : 1f);
        }

        /// <summary>
        /// Нужна ли «жёсткая» пауза всего звука. Платформа, реклама, покупка и потеря фокуса останавливают звук полностью.
        /// Пауза самим игроком только приглушает музыку и гасит часть зацикленных звуков. Отсчёт (Countdown) звук
        /// не останавливает: он идёт уже после того, как реклама или окно платформы закрылись, и музыка должна вернуться сразу.
        /// </summary>
        public static bool IsHardPause(PauseReason activeReasons)
        {
            return (activeReasons & ~(PauseReason.User | PauseReason.Countdown)) != PauseReason.None;
        }
    }

    /// <summary>
    /// Ограничитель частоты эффектов: один и тот же звук не играет чаще заданного интервала
    /// (иначе десяток одинаковых звуков за кадр сливается в треск).
    /// </summary>
    public sealed class SfxThrottle
    {
        private readonly Dictionary<int, float> lastPlayed = new Dictionary<int, float>();

        /// <summary>Разрешает звук и запоминает время, если с прошлого раза прошёл интервал. Иначе false.</summary>
        public bool TryAcquire(int id, float now, float minInterval)
        {
            if (lastPlayed.TryGetValue(id, out var last) && now - last < minInterval)
            {
                return false;
            }

            lastPlayed[id] = now;
            return true;
        }

        /// <summary>Играл ли звук за последние <paramref name="window"/> секунд (не запоминает ничего).</summary>
        public bool PlayedRecently(int id, float now, float window)
        {
            return lastPlayed.TryGetValue(id, out var last) && now - last < window;
        }
    }
}
