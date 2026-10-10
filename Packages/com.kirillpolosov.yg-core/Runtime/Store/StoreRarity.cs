namespace YgCore.Store
{
    /// <summary>
    /// Визуальная редкость товара: серый, синий, фиолетовый, золотой. На логику магазина не влияет,
    /// нужна экранам для рамок и подписей. Числовые значения не менять.
    /// </summary>
    public enum StoreRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }
}
