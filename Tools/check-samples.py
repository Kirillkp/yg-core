#!/usr/bin/env python3
"""Проверяет, что образец кода в документации модуля Save совпадает с реальным файлом, который компилируется и проверяется.

Образец лежит в Tools/CoreChecks/SampleSave.cs, а в документации Documentation~/modules/save.md приведён его копия.
Если изменить только один из двух, образец в документации перестанет быть рабочим. Скрипт ловит такое расхождение.

Использование: python3 Tools/check-samples.py   (код выхода 1 при расхождении)
"""

import os
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SAMPLE = os.path.join(ROOT, "Tools", "CoreChecks", "SampleSave.cs")
DOC = os.path.join(ROOT, "Packages", "com.kirillpolosov.yg-core", "Documentation~", "modules", "save.md")


def main():
    with open(SAMPLE, encoding="utf-8") as handle:
        sample = handle.read()
    with open(DOC, encoding="utf-8") as handle:
        doc = handle.read()

    # В документацию попадает всё, начиная с первой строки using (служебный заголовок файла пропускается).
    body = "using System;" + sample.split("using System;", 1)[1]
    if body.rstrip() not in doc:
        print("ОШИБКА: образец в modules/save.md не совпадает с Tools/CoreChecks/SampleSave.cs")
        print("Обновите документ так, чтобы в блоке кода был весь текст образца, начиная с 'using System;'.")
        sys.exit(1)
    print("ок: образец в документации совпадает с проверяемым файлом")


if __name__ == "__main__":
    main()
