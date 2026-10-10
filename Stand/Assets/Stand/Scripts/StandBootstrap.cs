using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace YgCoreStand
{
    /// <summary>
    /// Точка входа стенда: сама создаёт экран при старте сцены, ручная сборка сцены не нужна.
    /// Интерфейс на UI Toolkit, поэтому не зависит ни от uGUI, ни от Input System.
    /// </summary>
    public static class StandBootstrap
    {
        private static readonly Color Background = new Color(0.09f, 0.10f, 0.12f);
        private static readonly Color Panel = new Color(0.14f, 0.16f, 0.19f);
        private static readonly Color Accent = new Color(0.20f, 0.55f, 0.95f);

        private static StandApp app;
        private static ScrollView logView;
        private static string lastToken = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            var host = new GameObject("YgCoreStand");
            UnityEngine.Object.DontDestroyOnLoad(host);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(900, 1600);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;

            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;

            BuildScreen(document.rootVisualElement);

            app = new StandApp();
            app.Initialize();
            StandLog.Info("Версия пакета: " + StandVersion.Read());
            app.ReportAvailability();
        }

        private static void BuildScreen(VisualElement root)
        {
            root.style.flexGrow = 1;
            root.style.backgroundColor = Background;
            root.style.paddingTop = 24;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingBottom = 16;

            var title = new Label("yg-core · стенд  " + StandVersion.Read());
            title.style.fontSize = 34;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            root.Add(title);

            var buttons = new ScrollView { style = { flexGrow = 1, flexBasis = 0 } };
            root.Add(buttons);

            Section(buttons, "Готовность и геймплей",
                ("Готовность", () => app.MarkReadyAsync()),
                ("Геймплей: старт", () => { app.SetGameplay(true); return Task.CompletedTask; }),
                ("Геймплей: стоп", () => { app.SetGameplay(false); return Task.CompletedTask; }));

            Section(buttons, "Реклама",
                ("Interstitial", () => app.ShowInterstitialAsync()),
                ("Rewarded", () => app.ShowRewardedAsync()));

            Section(buttons, "Авторизация",
                ("Войти", () => app.SignInAsync()),
                ("Профиль", () => { app.ShowProfile(); return Task.CompletedTask; }));

            Section(buttons, "Лидерборд",
                ("Отправить результат", () => app.SubmitScoreAsync()),
                ("Топ-5", () => app.GetTopAsync()));

            Section(buttons, "Покупки",
                ("Каталог", () => app.ShowCatalogAsync()),
                ("Купить расходуемый", () => app.BuyAsync("stand_consumable")),
                ("Купить постоянный", () => app.BuyAsync("stand_permanent")),
                ("Невыданные", () => app.RestoreAsync()),
                ("Погасить последнюю", () => ConsumeLast()));

            Section(buttons, "Сохранение и язык (самопроверки)",
                ("Сохранение: запись/чтение", () => app.RunSaveCheckAsync()),
                ("Сохранение: порядок", () => app.RunSaveOrderCheckAsync()),
                ("Язык", () => app.RunLanguageCheckAsync()));

            logView = new ScrollView { style = { height = 520, backgroundColor = Panel, marginTop = 8, paddingLeft = 8, paddingTop = 4 } };
            root.Add(logView);
            StandLog.LineAdded += AppendLine;
        }

        private static Task ConsumeLast()
        {
            if (string.IsNullOrEmpty(lastToken))
            {
                StandLog.Info("Погашать нечего: сначала купите товар (токен берётся из журнала покупки).");
                return Task.CompletedTask;
            }

            return app.ConsumeAsync(lastToken);
        }

        /// <summary>Запоминает токен последней покупки для кнопки «Погасить последнюю».</summary>
        public static void RememberReceipt(string token)
        {
            lastToken = token;
        }

        private static void Section(VisualElement parent, string name, params (string label, Func<Task> action)[] items)
        {
            var header = new Label(name);
            header.style.color = Accent;
            header.style.fontSize = 24;
            header.style.marginTop = 10;
            parent.Add(header);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            foreach (var item in items)
            {
                var action = item.action;
                var button = new Button(async () =>
                {
                    try { await action(); }
                    catch (Exception e) { StandLog.Fail("Исключение: " + e.Message); }
                }) { text = item.label };
                button.style.fontSize = 24;
                button.style.height = 64;
                button.style.marginRight = 8;
                button.style.marginTop = 6;
                button.style.paddingLeft = 14;
                button.style.paddingRight = 14;
                row.Add(button);
            }

            parent.Add(row);
        }

        private static void AppendLine(string line)
        {
            if (logView == null) return;
            var label = new Label(line);
            label.style.fontSize = 20;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = line.Contains(" FAIL ") ? new Color(1f, 0.45f, 0.45f)
                : line.Contains(" OK  ") ? new Color(0.45f, 1f, 0.55f) : Color.white;
            logView.Add(label);
            logView.schedule.Execute(() => logView.scrollOffset = new Vector2(0, float.MaxValue));
        }
    }
}
