using System;

namespace YgCore.Contracts
{
    /// <summary>Данные событий «реклама началась» и «реклама закончилась».</summary>
    public sealed class AdPlaybackEventArgs : EventArgs
    {
        public AdPlaybackEventArgs(AdPlacementKind kind, string placementId)
        {
            Kind = kind;
            PlacementId = placementId;
        }

        public AdPlacementKind Kind { get; }

        /// <summary>Идентификатор места показа, заданный игрой (например, «revive» или «level_end»).</summary>
        public string PlacementId { get; }
    }
}
