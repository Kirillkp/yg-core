namespace YgCore.Contracts
{
    /// <summary>Тип рекламы.</summary>
    public enum AdPlacementKind
    {
        /// <summary>Реклама с наградой: награда выдаётся только после полного просмотра.</summary>
        Rewarded = 0,

        /// <summary>Полноэкранная реклама между игровыми моментами.</summary>
        Interstitial = 1
    }
}
