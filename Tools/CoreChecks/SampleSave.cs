// Образец подключения модуля Save: минимальная «игра» с монетами, рекордом, предметами и настройками.
// Этот же код приведён в Documentation~/modules/save.md. Он компилируется и проверяется в SaveChecks.cs,
// поэтому образец в документации всегда рабочий.
using System;
using System.Collections.Generic;
using UnityEngine;
using YgCore.Core;
using YgCore.Save;

// 1. Данные игры: наследник ProgressionData с публичными полями.
[Serializable]
public sealed class SampleProgress : ProgressionData
{
    public int balance;
    public int bestDistance;
    public List<string> ownedItemIds = new List<string>();
    public SettingsProgressData settings = new SettingsProgressData();
}

// 2. Схема: формат, версия и правила приведения, слияния и миграции.
public sealed class SampleSchema : IProgressionSchema<SampleProgress>
{
    public string FormatId => "sample-progress";

    public int CurrentSchemaVersion => 1;

    public SampleProgress Normalize(SampleProgress data)
    {
        data.balance = Math.Max(0, data.balance);
        data.bestDistance = Math.Max(0, data.bestDistance);
        data.ownedItemIds = ProgressionLists.Distinct(data.ownedItemIds);
        data.settings = SettingsProgressData.Normalized(data.settings);
        return data;
    }

    public SampleProgress Merge(SampleProgress guest, SampleProgress cloud)
    {
        // Числа берём большие, списки объединяем, настройки из более свежей записи.
        var preferenceSource = cloud.revision >= guest.revision ? cloud : guest;
        return new SampleProgress
        {
            balance = Math.Max(guest.balance, cloud.balance),
            bestDistance = Math.Max(guest.bestDistance, cloud.bestDistance),
            ownedItemIds = ProgressionLists.Union(guest.ownedItemIds, cloud.ownedItemIds),
            settings = SettingsProgressData.Normalized(preferenceSource.settings)
        };
    }

    public SampleProgress Migrate(int schemaVersion, string payload)
    {
        if (schemaVersion == 0)
        {
            var legacy = JsonUtility.FromJson<LegacyV0>(payload);
            return new SampleProgress
            {
                revision = legacy.revision,
                balance = legacy.coins,
                bestDistance = legacy.recordMeters,
                ownedItemIds = legacy.items
            };
        }

        throw new NotSupportedException("Неизвестная старая версия схемы: " + schemaVersion);
    }

    // Старый формат (версия 0), другие имена полей. Поля заполняет JsonUtility, поэтому предупреждение компилятора не нужно.
#pragma warning disable 0649
    [Serializable]
    private sealed class LegacyV0
    {
        public long revision;
        public int coins;
        public int recordMeters;
        public List<string> items = new List<string>();
    }
#pragma warning restore 0649
}

// 3. Связь с сервисами игры: что собрать для записи, что применить после загрузки, за чем следить.
public sealed class SampleBinding : IProgressionBinding<SampleProgress>
{
    private readonly SessionEconomyService economy;
    private readonly SessionGameSettingsService settings;
    private ProgressionService<SampleProgress> service;

    public SampleBinding(SessionEconomyService economy, SessionGameSettingsService settings)
    {
        this.economy = economy;
        this.settings = settings;
    }

    public int BestDistance { get; set; }

    public List<string> OwnedItems { get; private set; } = new List<string>();

    public SampleProgress Capture()
    {
        return new SampleProgress
        {
            balance = economy.Balance,
            bestDistance = BestDistance,
            ownedItemIds = new List<string>(OwnedItems),
            settings = SettingsProgressData.CaptureFrom(settings)
        };
    }

    public void Apply(SampleProgress data)
    {
        economy.RestoreBalance(data.balance);
        BestDistance = data.bestDistance;
        OwnedItems = new List<string>(data.ownedItemIds ?? new List<string>());
        (data.settings ?? new SettingsProgressData()).ApplyTo(settings);
    }

    public void Attach(ProgressionService<SampleProgress> progression)
    {
        service = progression;
        economy.BalanceChanged += HandleBalanceChanged;
        settings.SettingsChanged += HandleSettingsChanged;
    }

    public void Detach()
    {
        economy.BalanceChanged -= HandleBalanceChanged;
        settings.SettingsChanged -= HandleSettingsChanged;
        service = null;
    }

    public void AddItem(string itemId)
    {
        OwnedItems.Add(itemId);
        service?.RequestBatchSave();
    }

    private void HandleBalanceChanged(object sender, EconomyBalanceChangedEventArgs args)
    {
        // Награду за забег терять нельзя: пишем сразу. Остальные изменения баланса пишутся пакетом.
        if (args.Reason == "run_reward")
        {
            service?.RequestCriticalSave();
        }
        else
        {
            service?.RequestBatchSave();
        }
    }

    private void HandleSettingsChanged(object sender, GameSettingsChangedEventArgs args)
    {
        service?.RequestBatchSave();
    }
}
