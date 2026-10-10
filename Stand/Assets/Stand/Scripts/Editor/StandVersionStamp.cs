using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace YgCoreStand.Editor
{
    /// <summary>Записывает версию пакета из его package.json в Resources перед сборкой и после перезагрузки скриптов.</summary>
    public sealed class StandVersionStamp : IPreprocessBuildWithReport
    {
        private const string PackageJson = "../Packages/com.kirillpolosov.yg-core/package.json";
        private const string Target = "Assets/Stand/Resources/stand-package-version.txt";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Stamp();

        [InitializeOnLoadMethod]
        private static void StampOnLoad() => Stamp();

        private static void Stamp()
        {
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", PackageJson));
            if (!File.Exists(path)) return;

            var version = JsonUtility.FromJson<PackageInfoLite>(File.ReadAllText(path)).version;
            if (File.Exists(Target) && File.ReadAllText(Target).Trim() == version) return;

            Directory.CreateDirectory(Path.GetDirectoryName(Target));
            File.WriteAllText(Target, version);
            AssetDatabase.ImportAsset(Target);
        }

        [System.Serializable]
        private sealed class PackageInfoLite
        {
            public string version;
        }
    }
}
