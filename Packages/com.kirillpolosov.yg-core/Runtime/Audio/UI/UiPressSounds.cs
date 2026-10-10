using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace YgCore.Audio.UI
{
    /// <summary>
    /// Звук нажатия на элементы интерфейса (uGUI). При касании или нажатии мыши определяет верхний доступный
    /// элемент под указателем и проигрывает для него звук через <see cref="AudioDirector"/>.
    ///
    /// Звук выбирается так: переключатель получает звук переключателя, ползунок звук ползунка, остальные элементы
    /// проходят через <see cref="Resolver"/> (правила игры по имени или типу кнопки), а если он не нашёл особого звука,
    /// играет общий звук нажатия. Идентификатор меньше нуля означает «без звука».
    ///
    /// Эта сборка подключается только когда в проекте есть uGUI и Input System (символы YGCORE_UGUI и YGCORE_INPUT_SYSTEM).
    /// В остальных проектах звуки интерфейса включает сама игра.
    /// </summary>
    public sealed class UiPressSounds : MonoBehaviour
    {
        /// <summary>Значение «звука нет»: возвращается правилами игры, чтобы использовать звук по умолчанию.</summary>
        public const int NoSound = -1;

        [SerializeField, Tooltip("Звук переключателя. Меньше нуля: без звука.")]
        private int toggleSfxId = NoSound;

        [SerializeField, Tooltip("Звук ползунка. Меньше нуля: без звука.")]
        private int sliderSfxId = NoSound;

        [SerializeField, Tooltip("Общий звук нажатия на остальные элементы. Меньше нуля: без звука.")]
        private int defaultSfxId = NoSound;

        private readonly List<RaycastResult> raycastBuffer = new List<RaycastResult>();
        private AudioDirector director;

        /// <summary>
        /// Правила игры: по элементу возвращают идентификатор его звука или <see cref="NoSound"/>,
        /// если нужен общий звук по умолчанию. Задаётся игрой (например, по имени кнопки).
        /// </summary>
        public Func<Selectable, int> Resolver { get; set; }

        public void Initialize(AudioDirector audioDirector)
        {
            director = audioDirector;
        }

        private void Update()
        {
            if (director == null || !PressedThisFrame(out var position))
            {
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            raycastBuffer.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, raycastBuffer);
            foreach (var hit in raycastBuffer)
            {
                // Звук только для доступных элементов; недоступные пропускаем и смотрим, что лежит под ними.
                var selectable = hit.gameObject.GetComponentInParent<Selectable>();
                if (selectable == null || !selectable.IsInteractable())
                {
                    continue;
                }

                PlayForSelectable(selectable);
                return;
            }
        }

        private void PlayForSelectable(Selectable selectable)
        {
            var id = ResolveSfxId(selectable);
            if (id >= 0)
            {
                director.Play(id);
            }
        }

        private int ResolveSfxId(Selectable selectable)
        {
            if (selectable is Toggle)
            {
                return toggleSfxId;
            }

            if (selectable is Slider)
            {
                return sliderSfxId;
            }

            var custom = Resolver != null ? Resolver(selectable) : NoSound;
            return custom >= 0 ? custom : defaultSfxId;
        }

        private static bool PressedThisFrame(out Vector2 position)
        {
            position = default;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                position = touch.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            return false;
        }
    }
}
