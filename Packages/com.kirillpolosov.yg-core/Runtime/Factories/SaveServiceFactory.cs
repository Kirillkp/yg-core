using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию хранилища.
    /// В редакторе возвращает мок, в WebGL с модулем Storage_yg реализацию на PluginYG2, иначе заглушку «недоступно».
    /// </summary>
    public static class SaveServiceFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с заранее заполненными данными).</param>
        /// <param name="storageFieldName">
        /// Имя поля со списком записей в SavesYG. Если не задано, используется имя по умолчанию.
        /// Игре, у которой уже есть облачные сохранения игроков под другим именем поля, нужно указать его,
        /// чтобы прогресс не потерялся.
        /// </param>
        public static ISaveService Create(
            ISaveService editorService = null,
            string storageFieldName = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockSaveService();
#elif UNITY_WEBGL && Storage_yg
            return new Yandex.PluginYG2SaveService(storageFieldName);
#else
            return new UnavailableSaveService();
#endif
        }
    }
}
