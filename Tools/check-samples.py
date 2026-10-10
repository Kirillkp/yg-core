#!/usr/bin/env python3
"""Проверяет, что образцы кода в документации модулей совпадают с реальными файлами, которые компилируются и проверяются.

Каждый образец лежит в Tools/CoreChecks/ (SampleSave.cs, SampleStore.cs), а в документации модуля приведена его копия.
Если изменить только один из двух, образец в документации перестанет быть рабочим. Скрипт ловит такое расхождение.

Использование: python3 Tools/check-samples.py   (код выхода 1 при расхождении)
"""

import os
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
MODULES = os.path.join(ROOT, "Packages", "com.kirillpolosov.yg-core", "Documentation~", "modules")

# (файл образца, документ модуля)
PAIRS = [
    (os.path.join(ROOT, "Tools", "CoreChecks", "SampleSave.cs"), os.path.join(MODULES, "save.md")),
    (os.path.join(ROOT, "Tools", "CoreChecks", "SampleStore.cs"), os.path.join(MODULES, "store.md")),
]


def main():
    failed = False
    for sample_path, doc_path in PAIRS:
        with open(sample_path, encoding="utf-8") as handle:
            sample = handle.read()
        with open(doc_path, encoding="utf-8") as handle:
            doc = handle.read()

        # В документацию попадает всё, начиная с первой строки using (служебный заголовок файла пропускается).
        body = "using System;" + sample.split("using System;", 1)[1]
        name = os.path.basename(sample_path) + " -> " + os.path.basename(doc_path)
        if body.rstrip() not in doc:
            print("ОШИБКА: образец в {} не совпадает с файлом {}".format(os.path.basename(doc_path), os.path.basename(sample_path)))
            failed = True
        else:
            print("ок: " + name)

    if failed:
        print("Обновите документ так, чтобы в блоке кода был весь текст образца, начиная с 'using System;'.")
        sys.exit(1)


if __name__ == "__main__":
    main()
