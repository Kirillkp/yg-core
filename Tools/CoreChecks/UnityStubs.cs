// Заглушки Unity только для запуска проверок вне редактора.
// В пакет и в игры этот файл не попадает: он лежит в Tools/ и не входит в сборки Unity.
using System;
using System.Text.Json;

namespace UnityEngine
{
    // Упрощённый JsonUtility: публичные поля простых классов. Некорректный JSON даёт ArgumentException, как в Unity.
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };

        public static T FromJson<T>(string json)
        {
            try { return JsonSerializer.Deserialize<T>(json, Options); }
            catch (JsonException e) { throw new ArgumentException("Ошибка разбора JSON: " + e.Message); }
        }

        public static string ToJson(object value) => JsonSerializer.Serialize(value, value.GetType(), Options);
    }

    public static class Time
    {
        // Время, которым управляют проверки.
        public static float unscaledTime;
    }

    public static class Debug
    {
        public static void LogWarning(object message) => Console.WriteLine("  [Debug.LogWarning] " + message);
    }

    public static class Mathf
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static bool Approximately(float a, float b) => System.Math.Abs(a - b) < 1e-6f;
    }
}
