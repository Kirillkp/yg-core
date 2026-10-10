#!/usr/bin/env python3
"""Создаёт недостающие .meta-файлы для пакета Unity.

Зачем это нужно: пакет, подключённый в игру по git-адресу, Unity считает неизменяемым и
игнорирует в нём все файлы без .meta. Обычно .meta создаёт сам редактор, но мы пишем код
без запуска Unity, поэтому создаём их скриптом.

GUID вычисляется из пути внутри пакета. Это даёт стабильные одинаковые значения при любом
запуске и на любой машине, а значит ссылки между файлами не ломаются.

Использование:
    python3 Tools/generate-meta.py           создать недостающие .meta
    python3 Tools/generate-meta.py --check   только проверить, ничего не менять (код 1 при проблемах)

Уже существующие .meta-файлы не перезаписываются: если Unity создала свой, он остаётся.
"""

import hashlib
import os
import sys

PACKAGE_NAME = "com.kirillpolosov.yg-core"
ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PACKAGE_DIR = os.path.join(ROOT, "Packages", PACKAGE_NAME)

# Папки с тильдой Unity не импортирует, .meta для них не нужны.
SKIP_SUFFIX = "~"

MONO_IMPORTER = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER_IMPORTER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

ASMDEF_IMPORTER = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_IMPORTER = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

# JavaScript-плагин для WebGL (.jslib): включён только для WebGL, в редакторе и других платформах выключен.
JSLIB_IMPORTER = """fileFormatVersion: 2
guid: {guid}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any: 
    second:
      enabled: 0
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      WebGL: WebGL
    second:
      enabled: 1
      settings: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

DEFAULT_IMPORTER = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def make_guid(relative_path):
    """Стабильный GUID из пути внутри пакета (32 символа hex, как требует Unity)."""
    key = "{}/{}".format(PACKAGE_NAME, relative_path.replace(os.sep, "/"))
    return hashlib.md5(key.encode("utf-8")).hexdigest()


def template_for(path, is_dir):
    """Выбирает тип импортёра по расширению."""
    if is_dir:
        return FOLDER_IMPORTER
    ext = os.path.splitext(path)[1].lower()
    if ext == ".cs":
        return MONO_IMPORTER
    if ext == ".asmdef":
        return ASMDEF_IMPORTER
    if ext == ".jslib":
        return JSLIB_IMPORTER
    if ext in (".md", ".txt", ".json", ".xml", ".yaml", ".yml"):
        return TEXT_IMPORTER
    return DEFAULT_IMPORTER


def walk_assets():
    """Возвращает (путь, это_папка) для всего, что Unity должна импортировать в пакете."""
    for current, dirs, files in os.walk(PACKAGE_DIR):
        # Не заходим в скрытые папки и в папки с тильдой.
        dirs[:] = sorted(d for d in dirs if not d.endswith(SKIP_SUFFIX) and not d.startswith("."))
        for name in dirs:
            yield os.path.join(current, name), True
        for name in sorted(files):
            if name.endswith(".meta") or name.startswith("."):
                continue
            yield os.path.join(current, name), False


def main():
    check_only = "--check" in sys.argv
    created = 0
    missing = []

    for path, is_dir in walk_assets():
        meta_path = path + ".meta"
        if os.path.exists(meta_path):
            continue
        if check_only:
            missing.append(os.path.relpath(meta_path, ROOT))
            continue
        relative = os.path.relpath(path, PACKAGE_DIR)
        with open(meta_path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(template_for(path, is_dir).format(guid=make_guid(relative)))
        created += 1

    # Лишние .meta без файла Unity удаляет сама и ругается, поэтому тоже сообщаем о них.
    orphans = []
    for current, dirs, files in os.walk(PACKAGE_DIR):
        dirs[:] = [d for d in dirs if not d.endswith(SKIP_SUFFIX) and not d.startswith(".")]
        for name in files:
            if name.endswith(".meta"):
                target = os.path.join(current, name[:-5])
                if not os.path.exists(target):
                    orphans.append(os.path.relpath(os.path.join(current, name), ROOT))

    if check_only:
        for item in missing:
            print("нет .meta: {}".format(item))
        for item in orphans:
            print("лишний .meta без файла: {}".format(item))
        if missing or orphans:
            sys.exit(1)
        print("ок: у всех файлов пакета есть .meta")
        return

    print("создано .meta-файлов: {}".format(created))
    for item in orphans:
        print("ВНИМАНИЕ, лишний .meta без файла: {}".format(item))


if __name__ == "__main__":
    main()
