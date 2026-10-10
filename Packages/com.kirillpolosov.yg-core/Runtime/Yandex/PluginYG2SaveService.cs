using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using YgCore.Contracts;
using YgCore.Core;

namespace YgCore.Yandex
{
    /// <summary>
    /// Хранилище «ключ → строка» поверх облачного сохранения PluginYG2 с явным подтверждением.
    ///
    /// Данные лежат в списке записей внутри <c>YG2.saves</c> (поле расширения SavesYG, имя задаёт
    /// <c>storageFieldName</c>). Запись и чтение идут через мост YgStorageBridge, который ждёт подтверждения
    /// от Яндекса: <see cref="SaveAsync"/> возвращает Success только после подтверждённой записи,
    /// а сбой загрузки возвращается как Error, а не как пустое сохранение.
    /// Операции выполняются строго по одной (блокировка), у каждой есть таймаут 15 секунд.
    /// </summary>
    public sealed class PluginYG2SaveService : ISaveService, IDisposable
    {
        /// <summary>Имя поля списка записей в SavesYG по умолчанию (см. YgSavesExtension).</summary>
        public const string DefaultStorageFieldName = "ygCoreStorage";

        private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(15d);

        private readonly string storageFieldName;
        private readonly Type yg2Type;
        private readonly FieldInfo savesField;
        private readonly FieldInfo sdkDataEventField;
        private readonly MethodInfo saveConfirmedMethod;
        private readonly MethodInfo loadConfirmedMethod;
        private readonly Action sdkDataHandler;
        private readonly AsyncGate operationLock = new AsyncGate();

        private TaskCompletionSource<bool> pendingSdkData;
        private bool isDisposed;

        /// <param name="storageFieldName">
        /// Имя поля со списком записей в SavesYG. По умолчанию <see cref="DefaultStorageFieldName"/>.
        /// Игра, у которой уже есть сохранения игроков под другим именем поля, передаёт его здесь,
        /// чтобы прогресс не потерялся.
        /// </param>
        public PluginYG2SaveService(string storageFieldName = DefaultStorageFieldName)
        {
            this.storageFieldName = string.IsNullOrWhiteSpace(storageFieldName)
                ? DefaultStorageFieldName
                : storageFieldName;
            yg2Type = PluginYG2BridgeReflection.ResolveType(PluginYG2BridgeReflection.Yg2TypeName);
            var storageBridgeType = PluginYG2BridgeReflection.ResolveType(
                PluginYG2BridgeReflection.StorageBridgeTypeName);
            savesField = yg2Type?.GetField("saves", BindingFlags.Public | BindingFlags.Static);
            sdkDataEventField = yg2Type?.GetField(
                "onGetSDKData",
                BindingFlags.Public | BindingFlags.Static);
            saveConfirmedMethod = storageBridgeType?.GetMethod(
                "SaveAsync",
                BindingFlags.Public | BindingFlags.Static);
            loadConfirmedMethod = storageBridgeType?.GetMethod(
                "LoadAsync",
                BindingFlags.Public | BindingFlags.Static);
            sdkDataHandler = HandleSdkDataReceived;
            AddSdkDataHandler();
        }

        public bool IsPlatformAvailable => !isDisposed
            && yg2Type != null
            && savesField != null
            && sdkDataEventField != null
            && saveConfirmedMethod != null
            && loadConfirmedMethod != null;

        public async Task<PlatformServiceResult<string>> LoadAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return PlatformServiceResult<string>.Error("Ключ хранилища пуст.");
            }

            await operationLock.WaitAsync();
            try
            {
                var readiness = await EnsureSdkDataReadyAsync();
                if (!readiness.IsSuccess)
                {
                    return ConvertFailure<string>(readiness);
                }

                // Загружаем данные из облака с подтверждением. Сбой здесь приведёт в catch ниже и станет Error.
                await InvokeStorageBridgeAsync(loadConfirmedMethod);

                var entry = FindEntry(key);
                var payload = entry == null ? null : ReadEntryString(entry, "payload");

                // Нет ключа: успех со значением null. Это не сбой, а «данных ещё не было».
                return PlatformServiceResult<string>.Success(
                    payload,
                    entry == null ? "Ключ хранилища не найден." : "Данные хранилища загружены.");
            }
            catch (Exception exception)
            {
                return PlatformServiceResult<string>.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
            finally
            {
                operationLock.Release();
            }
        }

        public async Task<PlatformServiceResult> SaveAsync(string key, string payload)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return PlatformServiceResult.Error("Ключ хранилища пуст.");
            }

            await operationLock.WaitAsync();
            try
            {
                var readiness = await EnsureSdkDataReadyAsync();
                if (!readiness.IsSuccess)
                {
                    return readiness;
                }

                var entries = GetOrCreateEntries();
                var entry = FindEntry(entries, key) ?? CreateEntry(entries, key);
                WriteEntryString(entry, "payload", payload ?? string.Empty);
                await InvokeStorageBridgeAsync(saveConfirmedMethod);
                return PlatformServiceResult.Success("Запись подтверждена Яндексом.");
            }
            catch (Exception exception)
            {
                return PlatformServiceResult.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
            finally
            {
                operationLock.Release();
            }
        }

        public async Task<PlatformServiceResult> DeleteAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return PlatformServiceResult.Error("Ключ хранилища пуст.");
            }

            await operationLock.WaitAsync();
            try
            {
                var readiness = await EnsureSdkDataReadyAsync();
                if (!readiness.IsSuccess)
                {
                    return readiness;
                }

                var entries = GetOrCreateEntries();
                var entry = FindEntry(entries, key);
                if (entry != null)
                {
                    entries.Remove(entry);
                }

                // Записываем всегда, даже если записи уже не было: прошлая неудачная
                // попытка удаления могла убрать её из памяти, но не из облака.
                await InvokeStorageBridgeAsync(saveConfirmedMethod);

                return PlatformServiceResult.Success("Данные хранилища удалены.");
            }
            catch (Exception exception)
            {
                return PlatformServiceResult.Error(
                    PluginYG2BridgeReflection.UnwrapMessage(exception));
            }
            finally
            {
                operationLock.Release();
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            RemoveSdkDataHandler();
            pendingSdkData?.TrySetResult(false);
            pendingSdkData = null;
        }

        private async Task<PlatformServiceResult> EnsureSdkDataReadyAsync()
        {
            if (!IsPlatformAvailable)
            {
                return PlatformServiceResult.Unavailable(
                    "Хранилище PluginYG2 недоступно.");
            }

            if (ReadStaticBool("isSDKEnabled") && savesField.GetValue(null) != null)
            {
                return PlatformServiceResult.Success();
            }

            return await WaitForSdkDataAsync(
                "Инициализация PluginYG2 не завершилась вовремя.",
                true);
        }

        private static async Task InvokeStorageBridgeAsync(MethodInfo method)
        {
            var operation = method.Invoke(null, null) as Task<string>;
            if (operation == null)
            {
                throw new InvalidOperationException("Мост хранилища не вернул операцию.");
            }

            await operation;
        }

        private async Task<PlatformServiceResult> WaitForSdkDataAsync(
            string timeoutMessage,
            bool completeIfSdkReady)
        {
            var completion = pendingSdkData ?? new TaskCompletionSource<bool>();
            pendingSdkData = completion;

            if (completeIfSdkReady
                && ReadStaticBool("isSDKEnabled")
                && savesField.GetValue(null) != null)
            {
                completion.TrySetResult(true);
            }

            var completedTask = await Task.WhenAny(completion.Task, RealtimeDelay.Delay(OperationTimeout));
            if (ReferenceEquals(pendingSdkData, completion))
            {
                pendingSdkData = null;
            }

            if (completedTask != completion.Task)
            {
                return PlatformServiceResult.Error(timeoutMessage);
            }

            return await completion.Task
                ? PlatformServiceResult.Success()
                : PlatformServiceResult.Unavailable("Хранилище PluginYG2 выгружено.");
        }

        private IList GetOrCreateEntries()
        {
            var saves = savesField.GetValue(null);
            if (saves == null)
            {
                throw new InvalidOperationException("Сохранения PluginYG2 недоступны.");
            }

            var storageField = saves.GetType().GetField(storageFieldName);
            if (storageField == null)
            {
                throw new MissingFieldException(saves.GetType().FullName, storageFieldName);
            }

            var entries = storageField.GetValue(saves) as IList;
            if (entries == null)
            {
                entries = Activator.CreateInstance(storageField.FieldType) as IList;
                storageField.SetValue(saves, entries);
            }

            return entries;
        }

        private object FindEntry(string key)
        {
            return FindEntry(GetOrCreateEntries(), key);
        }

        private static object FindEntry(IList entries, string key)
        {
            foreach (var entry in entries)
            {
                if (string.Equals(ReadEntryString(entry, "key"), key, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private static object CreateEntry(IList entries, string key)
        {
            var entryType = entries.GetType().GetGenericArguments()[0];
            var entry = Activator.CreateInstance(entryType);
            WriteEntryString(entry, "key", key);
            WriteEntryString(entry, "payload", string.Empty);
            entries.Add(entry);
            return entry;
        }

        private static string ReadEntryString(object entry, string fieldName)
        {
            return entry?.GetType().GetField(fieldName)?.GetValue(entry) as string;
        }

        private static void WriteEntryString(object entry, string fieldName, string value)
        {
            var field = entry?.GetType().GetField(fieldName);
            if (field == null)
            {
                throw new MissingFieldException(entry?.GetType().FullName, fieldName);
            }

            field.SetValue(entry, value);
        }

        private void HandleSdkDataReceived()
        {
            pendingSdkData?.TrySetResult(true);
        }

        private void AddSdkDataHandler()
        {
            if (sdkDataEventField == null)
            {
                return;
            }

            var handlers = sdkDataEventField.GetValue(null) as Delegate;
            sdkDataEventField.SetValue(null, Delegate.Combine(handlers, sdkDataHandler));
        }

        private void RemoveSdkDataHandler()
        {
            if (sdkDataEventField == null)
            {
                return;
            }

            var handlers = sdkDataEventField.GetValue(null) as Delegate;
            sdkDataEventField.SetValue(null, Delegate.Remove(handlers, sdkDataHandler));
        }

        private bool ReadStaticBool(string memberName)
        {
            var property = yg2Type?.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
            var value = property?.GetValue(null);
            if (value == null)
            {
                value = yg2Type?.GetField(memberName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            }

            return value is bool result && result;
        }

        private static PlatformServiceResult<T> ConvertFailure<T>(PlatformServiceResult result)
        {
            return result.Status == PlatformServiceStatus.PlatformUnavailable
                ? PlatformServiceResult<T>.Unavailable(result.Message)
                : PlatformServiceResult<T>.Error(result.Message);
        }
    }
}
