using System;
using UnityEngine;
using UnityEngine.UI;
using YgCore.Contracts;

namespace YgCore.Localization.UI
{
    /// <summary>
    /// Подставляет переведённые строки в надписи экрана (uGUI <c>Text</c>) и обновляет их при смене языка.
    /// Повесьте компонент на корень экрана, в инспекторе добавьте пары «надпись и ключ строки», а в коде экрана
    /// вызовите <see cref="InitializeChildren"/> один раз со своим сервисом локализации.
    ///
    /// Эта сборка подключается только когда в проекте есть uGUI (символ YGCORE_UGUI). В проектах на UI Toolkit
    /// или TextMeshPro строки подставляет сама игра через <c>ILocalizationService.Get</c>.
    /// </summary>
    public sealed class LocalizedTextView : MonoBehaviour
    {
        /// <summary>Одна надпись и ключ её строки.</summary>
        [Serializable]
        public sealed class Entry
        {
            public Text target;
            public string key;
        }

        // Заполняется в инспекторе Unity, поэтому предупреждение компилятора не нужно.
#pragma warning disable 0649
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
#pragma warning restore 0649

        private ILocalizationService localizationService;

        /// <summary>Подключает компонент к сервису локализации и сразу обновляет надписи. Можно вызывать повторно с другим сервисом.</summary>
        public void Initialize(ILocalizationService service)
        {
            if (ReferenceEquals(localizationService, service))
            {
                Refresh();
                return;
            }

            if (localizationService != null)
            {
                localizationService.LanguageChanged -= HandleLanguageChanged;
            }

            localizationService = service;
            if (localizationService != null)
            {
                localizationService.LanguageChanged += HandleLanguageChanged;
            }

            Refresh();
        }

        /// <summary>Подключает все компоненты <see cref="LocalizedTextView"/> внутри объекта, включая неактивные.</summary>
        public static void InitializeChildren(
            Component root,
            ILocalizationService service)
        {
            if (root == null)
            {
                return;
            }

            foreach (var view in root.GetComponentsInChildren<LocalizedTextView>(true))
            {
                view.Initialize(service);
            }
        }

        private void OnDestroy()
        {
            if (localizationService != null)
            {
                localizationService.LanguageChanged -= HandleLanguageChanged;
            }
        }

        private void HandleLanguageChanged(
            object sender,
            LanguageChangedEventArgs args)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (localizationService == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (entry?.target != null && !string.IsNullOrWhiteSpace(entry.key))
                {
                    // Включаем надпись: в сцене её могли отключить, пока строка не была подставлена.
                    entry.target.enabled = true;
                    entry.target.text = localizationService.Get(entry.key);
                }
            }
        }
    }
}
