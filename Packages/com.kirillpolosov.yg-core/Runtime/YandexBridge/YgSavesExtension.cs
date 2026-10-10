// Папка без .asmdef: класс SavesYG определён в PluginYG2 (Assembly-CSharp) как partial,
// и расширять его можно только из той же сборки.
#if PLUGIN_YG_2 && Storage_yg
using System;
using System.Collections.Generic;

namespace YG
{
    /// <summary>Одна запись хранилища: ключ и строка данных (формат определяет модуль Save).</summary>
    [Serializable]
    public sealed class YgCoreStorageEntry
    {
        public string key;
        public string payload;
    }

    /// <summary>
    /// Расширение сохранений PluginYG2: добавляет список записей «ключ → данные», в котором лежит
    /// всё сохранение игры. Имя поля попадает в JSON облака, поэтому менять его нельзя после выпуска игры:
    /// иначе прогресс игроков перестанет находиться.
    /// </summary>
    public partial class SavesYG
    {
        public List<YgCoreStorageEntry> ygCoreStorage = new List<YgCoreStorageEntry>();
    }
}
#endif
