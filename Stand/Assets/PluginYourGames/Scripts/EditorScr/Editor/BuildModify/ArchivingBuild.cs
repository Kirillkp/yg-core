using System;
using System.IO;
using System.IO.Compression;
using YG.Insides;

namespace YG.EditorScr.BuildModify
{
    public static class ArchivingBuild
    {
        public static void Archiving(string pathToBuiltProject)
        {
#if PLATFORM_WEBGL
            var archiveFileName = $"{pathToBuiltProject}_{PlatformSettings.currentPlatformBaseName}_Build({BuildLog.GetBuildNumber() + 1})";

            if (File.Exists($"{archiveFileName}.zip"))
            {
                string dateNow = $"{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day}";
                string timeNow = $"{DateTime.Now.Hour}-{DateTime.Now.Minute}-{DateTime.Now.Second}";
                archiveFileName += $"_{dateNow}_{timeNow}";
            }

            archiveFileName += ".zip";

            CreateCleanArchive(pathToBuiltProject, archiveFileName);
#endif
        }

        public static void CreateCleanArchive(string sourceDirectory, string archivePath)
        {
            var root = Path.GetFullPath(sourceDirectory).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    var entryName = file.Substring(root.Length).Replace('\\', '/');
                    var components = entryName.Split('/');
                    if (Array.Exists(components, part => part == ".DS_Store"
                        || part == "__MACOSX" || part.StartsWith("._", StringComparison.Ordinal)))
                        continue;
                    archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }
        }
    }
}
