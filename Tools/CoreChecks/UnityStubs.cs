// Заглушки Unity только для запуска проверок вне редактора.
// В пакет и в игры этот файл не попадает: он лежит в Tools/ и не входит в сборки Unity.
namespace UnityEngine
{
    public static class Mathf
    {
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static bool Approximately(float a, float b) => System.Math.Abs(a - b) < 1e-6f;
    }
}
