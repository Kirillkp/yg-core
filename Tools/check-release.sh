#!/usr/bin/env bash
# Проверка готовности версии пакета к выпуску.
# Использование: Tools/check-release.sh X.Y.Z
# Ничего не меняет в репозитории: только читает и сообщает об ошибках.
# Если найдена хотя бы одна ошибка, скрипт завершается с кодом 1,
# и ставить тег версии нельзя.

set -u

version="${1:-}"
root="$(cd "$(dirname "$0")/.." && pwd)"
pkg="$root/Packages/com.kirillpolosov.yg-core"
errors=0

fail() { echo "ОШИБКА: $1"; errors=$((errors + 1)); }
warn() { echo "ВНИМАНИЕ: $1"; }
ok()   { echo "ок: $1"; }

# Версия обязательна и должна быть в формате X.Y.Z
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Использование: $0 X.Y.Z (например, 0.1.0)"
  exit 2
fi

# 1. Версия в package.json совпадает с проверяемой
manifest_version="$(grep -E '"version"' "$pkg/package.json" | head -1 | sed -E 's/.*"version"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/')"
if [[ "$manifest_version" == "$version" ]]; then
  ok "версия в package.json: $manifest_version"
else
  fail "в package.json версия '$manifest_version', ожидалась '$version'"
fi

# 2. В CHANGELOG есть запись версии
if grep -qE "^## \[$version\]" "$pkg/CHANGELOG.md"; then
  ok "запись [$version] есть в CHANGELOG.md"
else
  fail "в CHANGELOG.md нет заголовка '## [$version]'"
fi

# 3. Есть подробная запись версии
version_file="$pkg/Documentation~/versions/v$version.md"
if [[ -f "$version_file" ]]; then
  ok "файл версии существует: versions/v$version.md"

  # 3а. Обязательные разделы
  for section in "## Что сделано" "## Зачем" "## Затронутые модули" "## Проверено на стенде" "## Перенос для игр" "## Риски и известные ограничения"; do
    if ! grep -qF "$section" "$version_file"; then
      fail "в versions/v$version.md нет раздела '$section'"
    fi
  done

  # 3б. Не осталось заготовок от шаблона
  if grep -qE "ГГГГ-ММ-ДД|vX\.Y\.Z|ЗАПОЛНИТЬ|TODO" "$version_file"; then
    fail "в versions/v$version.md остались заготовки шаблона (ГГГГ-ММ-ДД, vX.Y.Z, ЗАПОЛНИТЬ или TODO)"
  fi
else
  fail "нет файла Documentation~/versions/v$version.md"
fi

# 4. Совместимость упоминает версию (только предупреждение)
if ! grep -qF "$version" "$pkg/Documentation~/compatibility.md"; then
  warn "версия $version не упомянута в compatibility.md: обновите таблицу совместимости"
fi

# 5. Рабочее дерево чистое и такого тега ещё нет
if git -C "$root" rev-parse --git-dir >/dev/null 2>&1; then
  if [[ -n "$(git -C "$root" status --porcelain)" ]]; then
    fail "в рабочем дереве есть незакоммиченные изменения"
  else
    ok "рабочее дерево чистое"
  fi

  if git -C "$root" rev-parse "v$version" >/dev/null 2>&1; then
    fail "тег v$version уже существует"
  else
    ok "тега v$version ещё нет"
  fi
else
  warn "это не git-репозиторий: проверки дерева и тега пропущены"
fi

echo
if [[ $errors -gt 0 ]]; then
  echo "Итог: найдено ошибок: $errors. Тег ставить нельзя."
  exit 1
fi
echo "Итог: проверка пройдена. Прогон стенда на Яндексе и чек-лист нужно пройти отдельно."
