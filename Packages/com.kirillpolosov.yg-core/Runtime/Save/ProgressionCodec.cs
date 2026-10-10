using System;
using UnityEngine;

namespace YgCore.Save
{
    /// <summary>
    /// Превращает данные прогресса в строку и обратно, определяя состояние загрузки.
    /// Разбор идёт в таком порядке: пусто, заголовок, версия схемы, данные. Любая ошибка разбора даёт
    /// состояние Corrupted и не выбрасывается наружу.
    /// </summary>
    public sealed class ProgressionCodec<TData> where TData : ProgressionData, new()
    {
        private readonly IProgressionSchema<TData> schema;

        public ProgressionCodec(IProgressionSchema<TData> schema)
        {
            this.schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        /// <summary>Приводит данные к допустимому виду и проставляет поля конверта.</summary>
        public TData Normalize(TData data)
        {
            data = schema.Normalize(data ?? new TData()) ?? new TData();

            // Конверт заполняем последним, чтобы игра не могла его испортить.
            data.format = schema.FormatId;
            data.schemaVersion = schema.CurrentSchemaVersion;
            data.revision = Math.Max(0, data.revision);
            return data;
        }

        /// <summary>Сериализует данные в JSON после приведения к допустимому виду.</summary>
        public string Serialize(TData data)
        {
            return JsonUtility.ToJson(Normalize(data));
        }

        /// <summary>
        /// Разбирает строку из хранилища. Возвращает true, если данные можно использовать
        /// (состояния Empty, Loaded, Migrated), иначе false (Corrupted, UnsupportedVersion).
        /// </summary>
        public bool TryDeserialize(
            string payload,
            out TData data,
            out ProgressionLoadState state,
            out string message)
        {
            data = new TData();
            if (string.IsNullOrWhiteSpace(payload))
            {
                state = ProgressionLoadState.Empty;
                message = "Данных прогресса нет.";
                return true;
            }

            try
            {
                var header = JsonUtility.FromJson<ProgressHeader>(payload);
                if (header == null || header.format != schema.FormatId || header.schemaVersion < 0)
                {
                    state = ProgressionLoadState.Corrupted;
                    message = "У данных прогресса неверный заголовок.";
                    return false;
                }

                if (header.schemaVersion > schema.CurrentSchemaVersion)
                {
                    state = ProgressionLoadState.UnsupportedVersion;
                    message = "Данные прогресса созданы более новой версией игры.";
                    return false;
                }

                if (header.schemaVersion < schema.CurrentSchemaVersion)
                {
                    data = Normalize(schema.Migrate(header.schemaVersion, payload));
                    state = ProgressionLoadState.Migrated;
                    message = "Данные прогресса перенесены со схемы v" + header.schemaVersion
                        + " на v" + schema.CurrentSchemaVersion + ".";
                    return true;
                }

                data = Normalize(JsonUtility.FromJson<TData>(payload));
                state = ProgressionLoadState.Loaded;
                message = "Данные прогресса загружены.";
                return true;
            }
            catch (Exception exception)
            {
                data = new TData();
                state = ProgressionLoadState.Corrupted;
                message = "Данные прогресса повреждены: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Объединяет прогресс гостя с облачным. Номер записи результата на единицу больше большего из двух,
        /// чтобы результат считался новее обоих источников.
        /// </summary>
        public TData Merge(TData guest, TData cloud)
        {
            var guestNormalized = Normalize(guest);
            var cloudNormalized = Normalize(cloud);
            var merged = schema.Merge(guestNormalized, cloudNormalized) ?? new TData();
            merged.revision = Math.Max(guestNormalized.revision, cloudNormalized.revision) + 1;
            return Normalize(merged);
        }

        // Заголовок читается отдельно и до полных данных, чтобы оценить пригодность данных,
        // не разбирая их целиком.
#pragma warning disable 0649
        [Serializable]
        private sealed class ProgressHeader
        {
            public string format;
            public int schemaVersion;
        }
#pragma warning restore 0649
    }
}
