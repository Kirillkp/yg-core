using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YgCore.Contracts;
using YgCore.Core;

namespace YgCore.Save
{
    /// <summary>
    /// Сохранение и загрузка прогресса игры поверх <see cref="ISaveService"/>.
    ///
    /// Главные правила (перенесены из «Обби Потоп»):
    /// 1. Прогресс, который не удалось прочитать, никогда не затирается. Любой сбой загрузки (ошибка хранилища,
    ///    повреждённые данные, версия новее нашей, нет платформы) блокирует автоматическую запись.
    /// 2. Старое сохранение переносится на текущую схему и сразу записывается заново.
    /// 3. Изменения объединяются в одну запись через <see cref="ProgressionOptions.BatchDelay"/>,
    ///    а важные (награда, покупка) записываются немедленно. После неудачной записи идёт повтор.
    /// 4. Слишком большая запись не отправляется.
    /// 5. После входа игрока прогресс гостя объединяется с облачным, а не затирает его.
    ///
    /// Что именно хранится и как объединяется, определяет игра: схема (<see cref="IProgressionSchema{TData}"/>)
    /// и связь с её сервисами (<see cref="IProgressionBinding{TData}"/>).
    /// </summary>
    public sealed class ProgressionService<TData> : IDisposable where TData : ProgressionData, new()
    {
        private readonly ISaveService saveService;
        private readonly IAuthorizationService authorizationService;
        private readonly IProgressionBinding<TData> binding;
        private readonly ProgressionCodec<TData> codec;
        private readonly ProgressionOptions options;
        private readonly SemaphoreSlim saveLock = new SemaphoreSlim(1, 1);

        private CancellationTokenSource batchSaveCancellation;
        private long revision;
        private bool wasAuthorized;
        private bool isApplying;
        private bool isSynchronizingProfile;
        private int suppressionDepth;
        private bool isInitialized;
        private bool isDisposed;

        /// <param name="saveService">Хранилище платформы (можно null: прогресс будет жить только в сессии).</param>
        /// <param name="authorizationService">Авторизация для слияния гостя с облаком после входа (можно null).</param>
        /// <param name="schema">Схема данных игры.</param>
        /// <param name="binding">Связь с сервисами игры.</param>
        /// <param name="options">Настройки. Если не заданы, берутся значения по умолчанию.</param>
        public ProgressionService(
            ISaveService saveService,
            IAuthorizationService authorizationService,
            IProgressionSchema<TData> schema,
            IProgressionBinding<TData> binding,
            ProgressionOptions options = null)
        {
            this.saveService = saveService;
            this.authorizationService = authorizationService;
            this.binding = binding ?? throw new ArgumentNullException(nameof(binding));
            this.options = options ?? new ProgressionOptions();
            codec = new ProgressionCodec<TData>(
                schema ?? throw new ArgumentNullException(nameof(schema)));
            wasAuthorized = authorizationService?.CurrentProfile?.IsAuthorized == true;
        }

        /// <summary>Итог последней загрузки.</summary>
        public ProgressionLoadState LoadState { get; private set; } = ProgressionLoadState.NotLoaded;

        /// <summary>Пояснение к последней операции для логов. Игроку не показывается.</summary>
        public string StatusMessage { get; private set; } = string.Empty;

        /// <summary>Заблокирована ли автоматическая запись (после сбоя загрузки). Снимается успешной загрузкой или сбросом.</summary>
        public bool IsWriteBlocked { get; private set; }

        /// <summary>Размер последней сформированной записи в байтах.</summary>
        public int LastPayloadBytes { get; private set; }

        /// <summary>Номер последней записи.</summary>
        public long Revision => revision;

        /// <summary>Отключены ли сейчас запросы на сохранение (идёт применение данных, слияние или составное действие).</summary>
        public bool IsChangeTrackingSuppressed =>
            isApplying || isSynchronizingProfile || suppressionDepth > 0;

        /// <summary>
        /// Загружает прогресс, применяет его к игре и подключает отслеживание изменений.
        /// Повторный вызов ничего не делает. Загрузка выполняется до подключения событий,
        /// поэтому применение данных не вызывает лишних сохранений.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (isInitialized || isDisposed)
            {
                return;
            }

            await LoadAndApplyAsync();
            if (isDisposed)
            {
                return;
            }

            if (authorizationService != null)
            {
                authorizationService.ProfileChanged += HandleProfileChanged;
            }

            binding.Attach(this);
            isInitialized = true;
        }

        /// <summary>
        /// Временно отключает запросы на сохранение, пока не освободят возвращённый объект.
        /// Нужно для составного действия из нескольких изменений, которое должно записаться одной записью:
        /// внутри действия изменения не вызывают сохранений, а после него игра сама вызывает
        /// <see cref="RequestCriticalSave"/>. Вызовы можно вкладывать друг в друга.
        /// </summary>
        public IDisposable SuppressChangeTracking()
        {
            suppressionDepth++;
            return new SuppressionScope(this);
        }

        /// <summary>
        /// Записать скоро: изменения объединяются в одну запись через <see cref="ProgressionOptions.BatchDelay"/>.
        /// Для частых и неважных изменений (громкость, пассивный доход).
        /// </summary>
        public void RequestBatchSave()
        {
            RequestBatchSave(null);
        }

        /// <summary>
        /// Записать немедленно, отменив ожидающую запись. Не ждёт завершения и ошибок не бросает.
        /// Для изменений, которые нельзя потерять (награда, найденное, рекорд).
        /// </summary>
        public void RequestCriticalSave()
        {
            if (!IsChangeTrackingSuppressed)
            {
                CancelBatchSave();
                _ = SaveImmediatelyAsync();
            }
        }

        /// <summary>
        /// Записать немедленно и дождаться результата. Возвращает true, если запись подтверждена хранилищем.
        /// Нужно, когда следующий шаг зависит от успешной записи (например, подтверждение покупки платформе
        /// только после того, как награда записана).
        /// </summary>
        public async Task<bool> SaveCriticalAsync()
        {
            CancelBatchSave();
            var result = await SaveImmediatelyAsync();
            return result.IsSuccess;
        }

        /// <summary>Записать немедленно и вернуть подробный результат.</summary>
        public Task<PlatformServiceResult> SaveNowAsync()
        {
            return SaveImmediatelyAsync();
        }

        /// <summary>Загрузить прогресс заново и применить его к игре.</summary>
        public Task ReloadAsync()
        {
            return LoadAndApplyAsync();
        }

        /// <summary>
        /// Явный сброс прогресса игрока: удаляет запись, применяет пустые данные и записывает их.
        /// Единственный способ снять блокировку записи без загрузки.
        /// </summary>
        public async Task<PlatformServiceResult> ResetProgressAsync()
        {
            if (saveService == null)
            {
                return PlatformServiceResult.Unavailable("Хранилище недоступно.");
            }

            var deleteResult = await saveService.DeleteAsync(options.StorageKey);
            if (!deleteResult.IsSuccess)
            {
                return deleteResult;
            }

            IsWriteBlocked = false;
            LoadState = ProgressionLoadState.Empty;
            StatusMessage = "Прогресс явно сброшен.";
            Apply(codec.Normalize(new TData()));
            return await SaveImmediatelyAsync();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            if (authorizationService != null)
            {
                authorizationService.ProfileChanged -= HandleProfileChanged;
            }

            binding.Detach();
            batchSaveCancellation?.Cancel();
            batchSaveCancellation?.Dispose();
            batchSaveCancellation = null;
        }

        private async Task LoadAndApplyAsync()
        {
            if (saveService == null || !saveService.IsPlatformAvailable)
            {
                LoadState = ProgressionLoadState.PlatformUnavailable;
                StatusMessage = "Хранилище недоступно: прогресс живёт только в этой сессии.";
                IsWriteBlocked = true;
                return;
            }

            var result = await saveService.LoadAsync(options.StorageKey);
            if (!result.IsSuccess)
            {
                LoadState = result.Status == PlatformServiceStatus.PlatformUnavailable
                    ? ProgressionLoadState.PlatformUnavailable
                    : ProgressionLoadState.Error;
                StatusMessage = result.Message ?? "Не удалось загрузить прогресс.";

                // Сбой сети не доказывает, что в облаке пусто: запись блокируем, чтобы не затереть прогресс игрока.
                IsWriteBlocked = true;
                return;
            }

            if (!codec.TryDeserialize(
                    result.Value,
                    out var data,
                    out var state,
                    out var message))
            {
                LoadState = state;
                StatusMessage = message;
                IsWriteBlocked = true;
                Debug.LogWarning(message + " Автоматическая запись прогресса заблокирована.");
                return;
            }

            LoadState = state;
            StatusMessage = message;
            IsWriteBlocked = false;
            Apply(data);

            // Перенесённое с прошлой схемы сохранение сразу записываем в новом формате.
            if (state == ProgressionLoadState.Migrated)
            {
                await SaveImmediatelyAsync();
            }
        }

        private void Apply(TData data)
        {
            data = data ?? new TData();
            isApplying = true;
            try
            {
                revision = Math.Max(0, data.revision);
                binding.Apply(data);
            }
            finally
            {
                isApplying = false;
            }
        }

        private async Task<PlatformServiceResult> SaveImmediatelyAsync()
        {
            if (isDisposed)
            {
                return PlatformServiceResult.Unavailable(
                    "Сервис прогресса выгружен.");
            }

            if (IsWriteBlocked)
            {
                return PlatformServiceResult.Error(
                    "Автоматическая запись заблокирована, чтобы сохранить непрочитанный прогресс.");
            }

            if (saveService == null || !saveService.IsPlatformAvailable)
            {
                return PlatformServiceResult.Unavailable(
                    "Хранилище недоступно: прогресс остаётся в этой сессии.");
            }

            await saveLock.WaitAsync();
            try
            {
                // Пока ждали очередь, запись могли заблокировать или сервис выгрузили.
                if (isDisposed || IsWriteBlocked)
                {
                    return PlatformServiceResult.Error("Запись прогресса заблокирована.");
                }

                revision++;
                var data = binding.Capture() ?? new TData();
                data.revision = revision;
                var payload = codec.Serialize(data);
                LastPayloadBytes = Encoding.UTF8.GetByteCount(payload);
                if (LastPayloadBytes > options.MaximumPayloadBytes)
                {
                    return PlatformServiceResult.Error(
                        "Запись прогресса превышает безопасный предел размера хранилища.");
                }

                var result = await saveService.SaveAsync(options.StorageKey, payload);
                StatusMessage = result.Message ?? string.Empty;
                if (!result.IsSuccess)
                {
                    RequestBatchSave(options.RetryDelay);
                }

                return result;
            }
            catch (Exception exception)
            {
                StatusMessage = exception.Message;
                RequestBatchSave(options.RetryDelay);
                return PlatformServiceResult.Error(exception.Message);
            }
            finally
            {
                saveLock.Release();
            }
        }

        private async void HandleProfileChanged(
            object sender,
            AuthorizationProfileChangedEventArgs args)
        {
            // Слияние нужно только при входе гостя в аккаунт.
            var isAuthorized = args.Profile?.IsAuthorized == true;
            if (!wasAuthorized && isAuthorized)
            {
                await MergeGuestWithCloudAsync();
            }

            wasAuthorized = isAuthorized;
        }

        private async Task MergeGuestWithCloudAsync()
        {
            if (saveService == null || IsWriteBlocked || isDisposed)
            {
                return;
            }

            var guest = binding.Capture() ?? new TData();
            guest.revision = revision;
            CancelBatchSave();
            isSynchronizingProfile = true;
            var shouldSaveMergedProgress = false;
            await saveLock.WaitAsync();
            try
            {
                var result = await saveService.LoadAsync(options.StorageKey);
                if (!result.IsSuccess)
                {
                    StatusMessage = result.Message ?? "Не удалось загрузить облачный прогресс.";
                    return;
                }

                if (!codec.TryDeserialize(
                        result.Value,
                        out var cloud,
                        out var state,
                        out var message))
                {
                    // Облачный прогресс нечитаем: не затираем его прогрессом гостя.
                    LoadState = state;
                    StatusMessage = message;
                    IsWriteBlocked = true;
                    Debug.LogWarning(message + " Облачный прогресс не перезаписан.");
                    return;
                }

                var merged = codec.Merge(guest, cloud);
                Apply(merged);
                revision = merged.revision;
                LoadState = state == ProgressionLoadState.Empty
                    ? ProgressionLoadState.Loaded
                    : state;
                StatusMessage = "Прогресс гостя и облака объединены.";
                shouldSaveMergedProgress = true;
            }
            finally
            {
                saveLock.Release();
                isSynchronizingProfile = false;
            }

            if (shouldSaveMergedProgress)
            {
                await SaveImmediatelyAsync();
            }
        }

        private void RequestBatchSave(TimeSpan? delay)
        {
            if (IsChangeTrackingSuppressed || IsWriteBlocked || isDisposed)
            {
                return;
            }

            // Объединяем изменения, не сдвигая срок записи на каждое новое изменение:
            // иначе непрерывный поток мелких изменений (пассивный доход) откладывал бы запись бесконечно.
            if (batchSaveCancellation != null)
            {
                return;
            }

            batchSaveCancellation = new CancellationTokenSource();
            _ = SaveAfterDelayAsync(batchSaveCancellation.Token, delay ?? options.BatchDelay);
        }

        private async Task SaveAfterDelayAsync(CancellationToken cancellationToken, TimeSpan delay)
        {
            try
            {
                await RealtimeDelay.Delay(delay, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                CancelBatchSave();
                await SaveImmediatelyAsync();
            }
            catch (OperationCanceledException)
            {
                // Запись отменена (важное изменение или выгрузка сервиса): это не ошибка.
            }
        }

        private void CancelBatchSave()
        {
            batchSaveCancellation?.Cancel();
            batchSaveCancellation?.Dispose();
            batchSaveCancellation = null;
        }

        private sealed class SuppressionScope : IDisposable
        {
            private ProgressionService<TData> owner;

            public SuppressionScope(ProgressionService<TData> owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                // Повторное освобождение безопасно.
                if (owner == null)
                {
                    return;
                }

                owner.suppressionDepth--;
                owner = null;
            }
        }
    }
}
