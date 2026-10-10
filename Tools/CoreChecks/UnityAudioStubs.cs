// Заглушки Unity для проверок звука вне редактора. Источник звука запоминает всё, что с ним делают,
// поэтому код AudioDirector выполняется по-настоящему и его поведение можно проверять.
// В пакет этот файл не входит.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    // Атрибуты инспектора: для проверок значения не важны.
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string tooltip) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float min, float max) { } }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    public class Object { public string name; }

    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
    }

    public class Transform : Component
    {
        public Transform parent;
        public readonly List<Transform> children = new List<Transform>();

        public void SetParent(Transform newParent, bool worldPositionStays)
        {
            parent = newParent;
            newParent?.children.Add(this);
        }
    }

    public class GameObject : Object
    {
        public readonly Transform transform;
        private readonly List<Component> components = new List<Component>();

        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform { gameObject = this };
        }

        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            components.Add(component);
            return component;
        }

        public T GetComponent<T>() where T : Component
        {
            foreach (var component in components)
            {
                if (component is T match) return match;
            }

            return null;
        }

        public T GetComponentInParent<T>() where T : Component
        {
            for (var current = this; current != null; current = current.transform.parent?.gameObject)
            {
                var found = current.GetComponent<T>();
                if (found != null) return found;
            }

            return null;
        }

        public List<T> GetComponentsInChildren<T>() where T : Component
        {
            var result = new List<T>();
            Collect(this, result);
            return result;
        }

        private static void Collect<T>(GameObject root, List<T> result) where T : Component
        {
            foreach (var component in root.components)
            {
                if (component is T match) result.Add(match);
            }

            foreach (var child in root.transform.children)
            {
                Collect(child.gameObject, result);
            }
        }
    }

    public class MonoBehaviour : Component { }

    public class AudioClip : Object { }

    public class AudioSource : Component
    {
        public bool playOnAwake, loop, ignoreListenerPause, mute;
        public float spatialBlend, volume = 1f, pitch = 1f;
        public AudioClip clip;
        public bool isPlaying { get; private set; }

        // Журнал проигранных разово звуков: клип, громкость при запуске, высота тона при запуске.
        public readonly List<(AudioClip clip, float scale, float volume, float pitch)> OneShots =
            new List<(AudioClip, float, float, float)>();

        public void Play() { isPlaying = true; }
        public void Stop() { isPlaying = false; }
        public void Pause() { isPlaying = false; }
        // Общий журнал по порядку: источник, клип, громкость и высота тона источника в момент запуска.
        public static readonly List<(string source, AudioClip clip, float volume, float pitch)> Log =
            new List<(string, AudioClip, float, float)>();

        public void PlayOneShot(AudioClip oneShotClip, float volumeScale = 1f)
        {
            OneShots.Add((oneShotClip, volumeScale, volume, pitch));
            Log.Add((gameObject.name, oneShotClip, volume, pitch));
        }
    }

    public static class AudioListener { public static bool pause; }

    // Детерминированно: середина диапазона, чтобы случайный разброс не мешал проверкам.
    public static class Random { public static float Range(float min, float max) => (min + max) / 2f; }
}

namespace UnityEngine.UI
{
    public class Selectable : MonoBehaviour
    {
        public bool interactable = true;
        public virtual bool IsInteractable() => interactable;
    }

    public class Toggle : Selectable { }

    public class Slider : Selectable { }
}

namespace UnityEngine.EventSystems
{
    public struct RaycastResult { public GameObject gameObject; }

    public class EventSystem : MonoBehaviour
    {
        public static EventSystem current;
        public readonly List<RaycastResult> Hits = new List<RaycastResult>();

        public void RaycastAll(PointerEventData data, List<RaycastResult> result) { result.AddRange(Hits); }
    }

    public class PointerEventData
    {
        public Vector2 position;
        public PointerEventData(EventSystem eventSystem) { }
    }
}

namespace UnityEngine.InputSystem
{
    public class ButtonControl { public bool wasPressedThisFrame; }

    public class Vector2Control
    {
        public Vector2 value;
        public Vector2 ReadValue() => value;
    }

    public class TouchControl
    {
        public ButtonControl press = new ButtonControl();
        public Vector2Control position = new Vector2Control();
    }

    public class Touchscreen
    {
        public static Touchscreen current;
        public TouchControl primaryTouch = new TouchControl();
    }

    public class Mouse
    {
        public static Mouse current;
        public ButtonControl leftButton = new ButtonControl();
        public Vector2Control position = new Vector2Control();
    }
}
