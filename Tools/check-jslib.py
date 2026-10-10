#!/usr/bin/env python3
"""Проверяет согласованность имён между C# и JavaScript в пакете.

Компилятор не видит связь C# с .jslib: если имя функции написано по-разному, ошибка проявится
только в браузере. Скрипт сверяет:
  1. каждая внешняя функция C# ([DllImport("__Internal")] ... extern Имя) есть в каком-нибудь .jslib;
  2. каждая функция .jslib используется в C# (иначе она лишняя);
  3. каждое сообщение JS в Unity (YG2Instance('Имя', ...)) есть методом в YGSendMessage на стороне C#;
  4. каждый метод YGSendMessage вызывается либо из .jslib, либо описан как доработка PluginYG2
     в Documentation~/patches (то есть его вызывает чужой JS-файл).

Использование: python3 Tools/check-jslib.py   (код выхода 1 при расхождениях)
"""

import glob
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
PACKAGE = os.path.join(ROOT, "Packages", "com.kirillpolosov.yg-core")
RUNTIME = os.path.join(PACKAGE, "Runtime")
PATCHES = os.path.join(PACKAGE, "Documentation~", "patches")


def read(path):
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def main():
    cs_files = glob.glob(os.path.join(RUNTIME, "**", "*.cs"), recursive=True)
    js_files = glob.glob(os.path.join(RUNTIME, "**", "*.jslib"), recursive=True)
    errors = []

    # Внешние функции C#.
    externs = set()
    for path in cs_files:
        text = read(path)
        for match in re.finditer(
            r'\[DllImport\("__Internal"\)\]\s*(?:private|public|internal)?\s*static\s+extern\s+[\w<>\[\]]+\s+(\w+)\s*\(',
            text,
        ):
            externs.add(match.group(1))

    # Функции и сообщения в .jslib.
    js_functions = set()
    js_messages = set()
    for path in js_files:
        text = read(path)
        # Только функции верхнего уровня (отступ в 4 пробела): вложенные вспомогательные функции
        # вроде $YgCoreStorage.complete в Unity не экспортируются.
        for match in re.finditer(r"^    ([A-Za-z_]\w*)\s*:\s*function\s*\(", text, re.MULTILINE):
            js_functions.add(match.group(1))
        for match in re.finditer(r"YG2Instance\(\s*'(\w+)'", text):
            js_messages.add(match.group(1))

    # Методы-получатели сообщений на стороне C# (partial YGSendMessage).
    receivers = set()
    for path in cs_files:
        text = read(path)
        for block in re.finditer(r"partial class YGSendMessage\s*\{(.*?)\n    \}", text, re.DOTALL):
            for match in re.finditer(r"public void (\w+)\s*\(", block.group(1)):
                receivers.add(match.group(1))

    # Упомянутые в описаниях доработок PluginYG2.
    patched = ""
    if os.path.isdir(PATCHES):
        for path in glob.glob(os.path.join(PATCHES, "*.md")):
            patched += read(path)

    for name in sorted(externs - js_functions):
        errors.append("в C# объявлена внешняя функция '{}', но её нет ни в одном .jslib".format(name))
    for name in sorted(js_functions - externs):
        errors.append("в .jslib есть функция '{}', которую не вызывает C#".format(name))
    for name in sorted(js_messages - receivers):
        errors.append("JS шлёт сообщение '{}', а метода с таким именем в YGSendMessage нет".format(name))
    for name in sorted(receivers - js_messages):
        if name not in patched:
            errors.append(
                "метод YGSendMessage '{}' не вызывается ни из .jslib, ни описан в Documentation~/patches".format(name)
            )

    print("внешних функций C#: {}, функций .jslib: {}, сообщений JS: {}, получателей: {}".format(
        len(externs), len(js_functions), len(js_messages), len(receivers)))
    if errors:
        for item in errors:
            print("ОШИБКА: " + item)
        sys.exit(1)
    print("ок: имена C# и JavaScript согласованы")


if __name__ == "__main__":
    main()
