# Модели и Effort по уровням

Единственное место с названиями моделей. Процесс (`Docs/ORCHESTRATION.md`) говорит об уровнях L1, L2, L3 и не меняется при смене моделей. Актуально на 2026-10-11.

## Claude

| Уровень | Модель | Идентификатор | Effort по умолчанию | Повышать до |
|---|---|---|---|---|
| L3 (только Blender) | Opus 5.5 | `claude-opus-5-5` | `high` | `xhigh` |
| L2 | Sonnet 5.5 | `claude-sonnet-5-5` | `medium` | `high` |
| L1 | Haiku 5.5 | `claude-haiku-5-5` | `low` | `medium` |
| Оркестратор | Sonnet 5.5 | `claude-sonnet-5-5` | `medium` | `high` |

Значения Effort: `low`, `medium`, `high`, `xhigh`, `max`. `max` только по решению владельца.

## Codex

| Уровень | Модель | Идентификатор | Effort по умолчанию | Повышать до |
|---|---|---|---|---|
| L3 (только Blender) | GPT-6 Astra | `gpt-6-astra` | `high` | `xhigh` |
| L2 | GPT-6.1 Sol | `gpt-6.1-sol` | `medium` | `high` |
| L1 | GPT-6 Luna | `gpt-6-luna` | `low` | `medium` |

Шкала в CLI: `low`, `medium`, `high`, `xhigh`, `max`, `ultra`. У GPT-6 Luna максимум `max`. `max` и `ultra` только по решению владельца. GPT-5.5 снимается с Codex 2026-10-14, не использовать. Перед работой сверить идентификатор командой `/model`.

Источник: официальная страница моделей Codex (https://learn.chatgpt.com/docs/models).
