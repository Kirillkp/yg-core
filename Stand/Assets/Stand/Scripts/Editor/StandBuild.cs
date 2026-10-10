using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YgCoreStand.Editor
{
    /// <summary>
    /// Подготовка и сборка стенда под Яндекс Игры одной командой из меню «YgCore Stand».
    /// Настройки собраны здесь, а не руками в Player Settings, чтобы сборка стенда была воспроизводимой.
    /// Unity сама не запускается: пункты меню выполняются только по команде разработчика.
    /// </summary>
    public static class StandBuild
    {
        // Набор символов, на котором проверяется пакет (Documentation~/install.md).
        private const string Defines =
            "YandexGamesPlatform_yg;PLUGIN_YG_2;TMP_YG2;NJSON_YG2;EnvirData_yg;Authorization_yg;Storage_yg;" +
            "Leaderboards_yg;InterstitialAdv_yg;RewardedAdv_yg;Payments_yg;Localization_yg;RU_YG2";

        private const string ScenePath = "Assets/Stand/Scenes/Stand.unity";
        private const string Template = "PROJECT:YandexGames";
        private const string OutputFolder = "Builds/WebGL";

        [MenuItem("YgCore Stand/1. Подготовить WebGL")]
        public static void Prepare()
        {
            EnsureScene();

            // Символы PluginYG2 включаются только для WebGL: в редакторе на Mac стенд работает на моках.
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, Defines);
            PlayerSettings.WebGL.template = Template;

            // Уровень обрезки кода не выше минимального: сервисы пакета ищут PluginYG2 по имени (install.md, п. 7).
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
            PlayerSettings.productName = "yg-core stand";
            PlayerSettings.companyName = "kirillpolosov";
            PlayerSettings.runInBackground = false;

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.WebGL, BuildTarget.WebGL);
            }

            Debug.Log("[Стенд] Подготовка к WebGL выполнена: символы, шаблон, обрезка кода, сцена.");
        }

        [MenuItem("YgCore Stand/2. Собрать WebGL")]
        public static void Build()
        {
            Prepare();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[Стенд] Сборка готова: {Path.GetFullPath(OutputFolder)} ({report.summary.totalSize / 1048576f:0.0} МБ). " +
                          "Архив для консоли Яндекса создаёт PluginYG2 рядом с папкой сборки.");
            }
            else
            {
                Debug.LogError($"[Стенд] Сборка не удалась: {report.summary.result}, ошибок: {report.summary.totalErrors}.");
            }
        }

        // Стенд строит экран кодом, поэтому сцена пустая. Но хотя бы одна сцена в сборке нужна Unity.
        private static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
