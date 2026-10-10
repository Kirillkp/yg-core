using UnityEngine;

namespace YgCore.WebQuality
{
    /// <summary>
    /// Настройки качества графики для браузера: какой уровень качества Unity включать на компьютере и на телефоне
    /// и какую частоту кадров просить. Это файл-настройка (ScriptableObject), который игра создаёт в меню
    /// «Assets / Create / YgCore / Web Quality Settings» и кладёт в любую папку <c>Resources</c> под именем
    /// <c>WebQualitySettings</c>. Применяет настройки <see cref="WebQualityBootstrap"/> при запуске WebGL-сборки.
    /// </summary>
    [CreateAssetMenu(menuName = "YgCore/Web Quality Settings", fileName = WebQualityPolicy.ResourceName)]
    public sealed class WebQualitySettings : ScriptableObject
    {
        // Поля заполняет инспектор Unity, поэтому предупреждение компилятора о них не нужно.
#pragma warning disable 0649
        [Tooltip("Имя уровня качества для компьютеров. Такой уровень должен существовать в Project Settings / Quality и быть включён для WebGL.")]
        [SerializeField] private string desktopQualityName = "Web Desktop";

        [Tooltip("Имя уровня качества для телефонов и планшетов. Такой уровень должен существовать в Project Settings / Quality и быть включён для WebGL.")]
        [SerializeField] private string mobileQualityName = "Web Mobile";

        [Tooltip("-1: частоту кадров выбирает браузер (рекомендуется). Положительное число просит явный предел кадров в секунду.")]
        [SerializeField] private int targetFrameRate = -1;
#pragma warning restore 0649

        public string DesktopQualityName => desktopQualityName;

        public string MobileQualityName => mobileQualityName;

        /// <summary>Частота кадров для Unity: положительное число или -1 («решает браузер»).</summary>
        public int TargetFrameRate => WebQualityPolicy.NormalizeFrameRate(targetFrameRate);
    }
}
