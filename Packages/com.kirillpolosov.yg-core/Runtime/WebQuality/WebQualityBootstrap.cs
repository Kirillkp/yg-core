using System.Runtime.InteropServices;
using UnityEngine;

namespace YgCore.WebQuality
{
    /// <summary>
    /// Применяет настройки качества графики при запуске WebGL-сборки, до загрузки первой сцены: выбирает уровень качества
    /// для компьютера или телефона и частоту кадров. В редакторе и других платформах ничего не делает.
    ///
    /// Запускается сам (вызывать не нужно). Если файла настроек <see cref="WebQualitySettings"/> в Resources нет
    /// или нужного уровня качества нет в сборке, остаётся уровень по умолчанию и пишется предупреждение.
    /// </summary>
    public static class WebQualityBootstrap
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Функция из Plugins/YgCoreWebQuality.jslib.
        [DllImport("__Internal")]
        private static extern int YgCoreWebIsMobile();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var settings = Resources.Load<WebQualitySettings>(WebQualityPolicy.ResourceName);
            if (settings == null)
            {
                Debug.LogWarning("Настройки веб-качества не найдены (Resources/" + WebQualityPolicy.ResourceName
                    + "): используется уровень качества сборки по умолчанию.");
                return;
            }

            var isMobile = YgCoreWebIsMobile() != 0;
            var qualityIndex = WebQualityPolicy.FindQualityIndex(
                QualitySettings.names,
                isMobile,
                settings.DesktopQualityName,
                settings.MobileQualityName);
            if (qualityIndex < 0)
            {
                Debug.LogWarning("Уровня качества нет в сборке: "
                    + (isMobile ? settings.MobileQualityName : settings.DesktopQualityName));
                return;
            }

            QualitySettings.SetQualityLevel(qualityIndex, true);
            Application.targetFrameRate = settings.TargetFrameRate;
            Debug.Log("Веб-графика: " + QualitySettings.names[qualityIndex] + "; частота кадров: "
                + (settings.TargetFrameRate < 0 ? "решает браузер" : settings.TargetFrameRate.ToString()));
#endif
        }
    }
}
