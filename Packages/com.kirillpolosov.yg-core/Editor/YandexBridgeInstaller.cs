using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace YgCore.Editor
{
    /// <summary>
    /// Устанавливает мосты к PluginYG2 в проект игры.
    ///
    /// Зачем это нужно: PluginYG2 лежит в <c>Assembly-CSharp</c> (у него нет .asmdef), а сборка пакета не может
    /// ссылаться на <c>Assembly-CSharp</c>. Скрипты пакета без .asmdef Unity в сборки игры не берёт. Поэтому
    /// исходники мостов хранятся в пакете в папке <c>Runtime/YandexBridge~</c> (Unity её не импортирует), а этот
    /// установщик копирует их в <c>Assets/YgCore/YandexBridge</c> игры. Там они компилируются рядом с PluginYG2
    /// (под символом <c>PLUGIN_YG_2</c>) и сервисы пакета находят их по имени типа.
    ///
    /// Копия обновляется сама, когда меняется версия пакета или содержимое мостов. Папку назначения не правьте
    /// руками: файлы в ней перезаписываются. Закоммитьте её в репозиторий игры, чтобы сборка на другой машине
    /// не зависела от запуска редактора.
    /// </summary>
    [InitializeOnLoad]
    public static class YandexBridgeInstaller
    {
        /// <summary>Куда устанавливаются мосты в проекте игры.</summary>
        public const string TargetFolder = "Assets/YgCore/YandexBridge";

        private const string SourceFolder = "Runtime/YandexBridge~";
        private const string StampFile = ".yg-core-bridge-stamp";

        static YandexBridgeInstaller()
        {
            // Откладываем до конца загрузки редактора: AssetDatabase в статическом конструкторе недоступна.
            EditorApplication.delayCall += () => Install(force: false);
        }

        [MenuItem("YgCore/Переустановить мосты Яндекса")]
        private static void Reinstall() => Install(force: true);

        /// <summary>Копирует мосты в проект, если их нет или они устарели. Возвращает true, если файлы изменены.</summary>
        public static bool Install(bool force)
        {
            var package = PackageInfo.FindForAssembly(typeof(YandexBridgeInstaller).Assembly);
            if (package == null)
            {
                Debug.LogWarning("[yg-core] Не удалось определить папку пакета, мосты Яндекса не установлены.");
                return false;
            }

            var sourceRoot = Path.Combine(package.resolvedPath, SourceFolder);
            if (!Directory.Exists(sourceRoot))
            {
                Debug.LogWarning($"[yg-core] В пакете нет папки с мостами: {sourceRoot}");
                return false;
            }

            var files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.Ordinal))
                .Select(path => Relative(sourceRoot, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            var stamp = BuildStamp(package.version, sourceRoot, files);
            var stampPath = Path.Combine(TargetFolder, StampFile);
            if (!force && IsInstalled(stampPath, stamp, files))
            {
                return false;
            }

            Directory.CreateDirectory(TargetFolder);
            DeleteStale(files);
            foreach (var relative in files)
            {
                var target = Path.Combine(TargetFolder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                var text = File.ReadAllText(Path.Combine(sourceRoot, relative), Encoding.UTF8);
                File.WriteAllText(target, WithHeader(relative, text, package.version), new UTF8Encoding(false));
            }

            File.WriteAllText(stampPath, stamp);
            AssetDatabase.Refresh();
            Debug.Log($"[yg-core] Мосты Яндекса v{package.version} установлены в {TargetFolder} ({files.Length} файлов). " +
                      "Закоммитьте эту папку в репозиторий игры.");
            return true;
        }

        private static bool IsInstalled(string stampPath, string stamp, string[] files)
        {
            if (!File.Exists(stampPath) || File.ReadAllText(stampPath) != stamp)
            {
                return false;
            }

            return files.All(relative => File.Exists(Path.Combine(TargetFolder, relative)));
        }

        // Убирает файлы, которых в новой версии пакета больше нет, чтобы старые мосты не остались в проекте.
        private static void DeleteStale(string[] files)
        {
            if (!Directory.Exists(TargetFolder))
            {
                return;
            }

            var keep = files.Select(relative => Path.GetFullPath(Path.Combine(TargetFolder, relative))).ToHashSet();
            foreach (var path in Directory.GetFiles(TargetFolder, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(path);
                if (name == StampFile || name.EndsWith(".meta", StringComparison.Ordinal) || keep.Contains(Path.GetFullPath(path)))
                {
                    continue;
                }

                File.Delete(path);
                var meta = path + ".meta";
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }
            }
        }

        // Метка = версия пакета + хеш содержимого мостов: так обновляется и при правке мостов без смены версии.
        private static string BuildStamp(string version, string sourceRoot, string[] files)
        {
            using (var sha = SHA1.Create())
            {
                var builder = new StringBuilder();
                foreach (var relative in files)
                {
                    builder.Append(relative).Append('\n').Append(File.ReadAllText(Path.Combine(sourceRoot, relative), Encoding.UTF8)).Append('\n');
                }

                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return version + "|" + BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        // Предупреждение в начале C#-файла: файл создан установщиком, а не написан в игре.
        private static string WithHeader(string relative, string text, string version)
        {
            if (!relative.EndsWith(".cs", StringComparison.Ordinal))
            {
                return text;
            }

            return "// Файл установлен пакетом com.kirillpolosov.yg-core v" + version + " (YgCore/Переустановить мосты Яндекса).\n" +
                   "// Не правьте его в игре: при обновлении пакета он будет перезаписан.\n" + text;
        }

        private static string Relative(string root, string path) => path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/');
    }
}
