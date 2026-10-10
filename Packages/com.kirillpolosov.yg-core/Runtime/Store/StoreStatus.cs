namespace YgCore.Store
{
    /// <summary>
    /// Результат действия в магазине для показа игроку. Сервис сообщает его событием
    /// <see cref="SessionStoreService.StatusChanged"/>. Текст на нужном языке игра берёт из своих строк по ключу
    /// из <see cref="StoreStatusKeys.ToKey"/> (например, «store.status.success»).
    /// </summary>
    public enum StoreStatus
    {
        /// <summary>Покупка выполнена, награда выдана и записана.</summary>
        Success,

        /// <summary>Товар нельзя купить за монеты (его продают только за реальные деньги или он неизвестен).</summary>
        NotForCoins,

        /// <summary>Товар уже куплен: он просто надет.</summary>
        AlreadyOwned,

        /// <summary>Не хватает монет.</summary>
        InsufficientCoins,

        /// <summary>Предмет снят (для необязательных категорий).</summary>
        AccessoryRemoved,

        /// <summary>Предмет надет.</summary>
        OutfitUpdated,

        /// <summary>Покупки на платформе недоступны или товара нет в каталоге платформы.</summary>
        Unavailable,

        /// <summary>Покупка идёт.</summary>
        InProgress,

        /// <summary>Покупка отменена или не прошла.</summary>
        Canceled,

        /// <summary>Платформа вернула некорректную квитанцию (нет токена).</summary>
        Invalid,

        /// <summary>Платформа вернула товар, которого нет в каталоге игры.</summary>
        UnknownProduct,

        /// <summary>Покупка уже обработана ранее, но запись прогресса не удалась: запись будет повторена.</summary>
        SaveRetry,

        /// <summary>Покупка уже обработана ранее.</summary>
        AlreadyProcessed,

        /// <summary>Награда выдана, но платформе не удалось подтвердить покупку: подтверждение будет повторено.</summary>
        ConsumeRetry,

        /// <summary>Награду выдать не удалось (например, нулевая сумма пакета). Покупка не помечена обработанной.</summary>
        NotGranted,

        /// <summary>Награда выдана, но прогресс записать не удалось: запись будет повторена, покупка пока не подтверждена платформе.</summary>
        GrantedSaveRetry
    }

    /// <summary>Ключи строк для результатов магазина. Совпадают с ключами «Обби Потоп», поэтому его строки подходят без изменений.</summary>
    public static class StoreStatusKeys
    {
        /// <summary>Ключ строки для статуса, например «store.status.insufficient_coins».</summary>
        public static string ToKey(StoreStatus status)
        {
            switch (status)
            {
                case StoreStatus.Success: return "store.status.success";
                case StoreStatus.NotForCoins: return "store.status.coins_only";
                case StoreStatus.AlreadyOwned: return "store.status.already_owned";
                case StoreStatus.InsufficientCoins: return "store.status.insufficient_coins";
                case StoreStatus.AccessoryRemoved: return "store.status.accessory_removed";
                case StoreStatus.OutfitUpdated: return "store.status.outfit_updated";
                case StoreStatus.Unavailable: return "store.status.unavailable";
                case StoreStatus.InProgress: return "store.status.in_progress";
                case StoreStatus.Canceled: return "store.status.canceled";
                case StoreStatus.Invalid: return "store.status.invalid";
                case StoreStatus.UnknownProduct: return "store.status.unknown_product";
                case StoreStatus.SaveRetry: return "store.status.save_retry";
                case StoreStatus.AlreadyProcessed: return "store.status.already_processed";
                case StoreStatus.ConsumeRetry: return "store.status.consume_retry";
                case StoreStatus.NotGranted: return "store.status.not_granted";
                case StoreStatus.GrantedSaveRetry: return "store.status.granted_save_retry";
                default: return "store.status.unavailable";
            }
        }
    }
}
