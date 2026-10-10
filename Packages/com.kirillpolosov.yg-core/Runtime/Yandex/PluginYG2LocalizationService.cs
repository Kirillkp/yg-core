using System;
using System.Reflection;
using System.Threading.Tasks;
using YgCore.Contracts;
using YgCore.Localization;

namespace YgCore.Yandex
{
    /// <summary>
    /// Локализация через PluginYG2: язык берётся у платформы и следит за её событиями смены языка,
    /// строки выдаются из каталога игры.
    /// </summary>
    public sealed class PluginYG2LocalizationService : ILocalizationService, IDisposable
    {
        private readonly ILocalizationCatalog catalog;
        private readonly Type yg2Type;
        private readonly FieldInfo switchLanguageEventField;
        private readonly FieldInfo correctLanguageEventField;
        private readonly MethodInfo getLanguageMethod;
        private readonly MethodInfo switchLanguageMethod;
        private readonly Action<string> languageHandler;
        private bool isDisposed;

        /// <param name="catalog">Каталог строк игры: он же нормализует код языка платформы.</param>
        public PluginYG2LocalizationService(ILocalizationCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            yg2Type = PluginYG2BridgeReflection.ResolveType(PluginYG2BridgeReflection.Yg2TypeName);
            switchLanguageEventField = FindStaticField("onSwitchLang");
            correctLanguageEventField = FindStaticField("onCorrectLang");
            getLanguageMethod = FindStaticMethod("GetLanguage");
            switchLanguageMethod = FindStaticMethod("SwitchLanguage");
            languageHandler = HandleLanguageChanged;
            CurrentLanguage = catalog.NormalizeLanguage(ReadLanguage());
            AddHandler(switchLanguageEventField);
            AddHandler(correctLanguageEventField);
        }

        public event EventHandler<LanguageChangedEventArgs> LanguageChanged;

        public bool IsPlatformAvailable => !isDisposed
            && yg2Type != null
            && switchLanguageEventField != null
            && ReadStaticBool("isSDKEnabled");

        public string CurrentLanguage { get; private set; }

        public Task<PlatformServiceResult<string>> DetectLanguageAsync()
        {
            if (isDisposed)
            {
                return Task.FromResult(
                    PlatformServiceResult<string>.Unavailable(
                        "Сервис локализации выгружен."));
            }

            try
            {
                if (IsPlatformAvailable && getLanguageMethod != null)
                {
                    getLanguageMethod.Invoke(null, null);
                }

                SetCurrentLanguage(ReadLanguage());
                return Task.FromResult(
                    PlatformServiceResult<string>.Success(CurrentLanguage));
            }
            catch (Exception exception)
            {
                // При сбое откатываемся на язык по умолчанию, чтобы игра осталась читаемой.
                SetCurrentLanguage(catalog.DefaultLanguage);
                return Task.FromResult(
                    PlatformServiceResult<string>.Error(
                        PluginYG2BridgeReflection.UnwrapMessage(exception)));
            }
        }

        public string Get(string key, params object[] arguments)
        {
            return catalog.Get(CurrentLanguage, key, arguments);
        }

        public void SetLanguage(string language)
        {
            var normalized = catalog.NormalizeLanguage(language);
            if (switchLanguageMethod != null && IsPlatformAvailable)
            {
                try
                {
                    // Просим PluginYG2 переключить язык. Он сам пришлёт событие, по которому мы обновимся.
                    switchLanguageMethod.Invoke(null, new object[] { normalized });
                    return;
                }
                catch
                {
                    // Платформа не смогла переключить язык: переключаем только у себя ниже.
                }
            }

            SetCurrentLanguage(normalized);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            RemoveHandler(switchLanguageEventField);
            RemoveHandler(correctLanguageEventField);
        }

        private void HandleLanguageChanged(string language)
        {
            SetCurrentLanguage(language);
        }

        private void SetCurrentLanguage(string language)
        {
            var normalized = catalog.NormalizeLanguage(language);
            if (normalized == CurrentLanguage)
            {
                return;
            }

            var previous = CurrentLanguage;
            CurrentLanguage = normalized;
            LanguageChanged?.Invoke(
                this,
                new LanguageChangedEventArgs(previous, CurrentLanguage));
        }

        private string ReadLanguage()
        {
            return FindStaticField("lang")?.GetValue(null) as string;
        }

        private bool ReadStaticBool(string memberName)
        {
            var value = yg2Type?.GetProperty(
                memberName,
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (value == null)
            {
                value = FindStaticField(memberName)?.GetValue(null);
            }

            return value is bool result && result;
        }

        private void AddHandler(FieldInfo eventField)
        {
            if (eventField == null)
            {
                return;
            }

            var handlers = eventField.GetValue(null) as Delegate;
            eventField.SetValue(null, Delegate.Combine(handlers, languageHandler));
        }

        private void RemoveHandler(FieldInfo eventField)
        {
            if (eventField == null)
            {
                return;
            }

            var handlers = eventField.GetValue(null) as Delegate;
            eventField.SetValue(null, Delegate.Remove(handlers, languageHandler));
        }

        private FieldInfo FindStaticField(string fieldName)
        {
            return yg2Type?.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static);
        }

        private MethodInfo FindStaticMethod(string methodName)
        {
            return yg2Type?.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
        }
    }
}
