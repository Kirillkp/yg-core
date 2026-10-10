using UnityEngine;

namespace YgCoreStand
{
    /// <summary>
    /// Версия проверяемого пакета для экрана. В редакторе читается из package.json пакета,
    /// при сборке её записывает StandVersionStamp в Resources, потому что в WebGL пакетов нет.
    /// </summary>
    public static class StandVersion
    {
        public const string ResourceName = "stand-package-version";

        public static string Read()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            return asset != null ? asset.text.Trim() : "?";
        }
    }
}
