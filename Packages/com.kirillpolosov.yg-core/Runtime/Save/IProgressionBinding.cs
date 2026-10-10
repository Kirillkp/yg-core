namespace YgCore.Save
{
    /// <summary>
    /// Связь сервиса прогресса с живыми сервисами игры (экономика, магазин, коллекции и т.д.):
    /// как собрать из них данные для записи, как применить загруженные данные и как следить за изменениями.
    /// Реализует игра.
    /// </summary>
    /// <typeparam name="TData">Класс данных игры.</typeparam>
    public interface IProgressionBinding<TData> where TData : ProgressionData, new()
    {
        /// <summary>Собирает текущее состояние игры в объект данных. Поля конверта заполнять не нужно.</summary>
        TData Capture();

        /// <summary>
        /// Применяет загруженные данные к сервисам игры. Данные уже приведены к допустимому виду.
        /// Во время вызова отслеживание изменений отключено: восстановление не вызывает сохранений.
        /// </summary>
        void Apply(TData data);

        /// <summary>
        /// Подключается к событиям сервисов игры. Вызывается один раз после первой загрузки.
        /// При изменении данных вызывайте <see cref="ProgressionService{TData}.RequestBatchSave()"/>
        /// (изменение можно записать чуть позже) или <see cref="ProgressionService{TData}.RequestCriticalSave"/>
        /// (изменение нельзя потерять: награда, покупка, найденное сокровище).
        /// </summary>
        void Attach(ProgressionService<TData> service);

        /// <summary>Отключается от событий. Вызывается при выгрузке сервиса прогресса, в том числе когда <see cref="Attach"/> не вызывался.</summary>
        void Detach();
    }
}
