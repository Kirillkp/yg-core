using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace YgCoreStand
{
    /// <summary>
    /// Точка входа стенда: сама создаёт экран при старте сцены, ручная сборка сцены не нужна.
    /// Интерфейс на UI Toolkit без темы: все стили заданы здесь явно, поэтому не зависят ни от uGUI,
    /// ни от Input System, ни от темы панели.
    /// </summary>
    public static class StandBootstrap
    {
        // Нейтральная тёмная палитра: поверхности различаются по яркости, акцент один.
        private static readonly Color Background = Hex("#16181C");
        private static readonly Color Card = Hex("#22252B");
        private static readonly Color Button = Hex("#30353D");
        private static readonly Color ButtonHover = Hex("#3C424C");
        private static readonly Color Border = Hex("#3A3F48");
        private static readonly Color TextMain = Hex("#E8EAED");
        private static readonly Color TextMuted = Hex("#98A0AB");
        private static readonly Color Accent = Hex("#6AA6FF");
        private static readonly Color OkColor = Hex("#5BD08A");
        private static readonly Color FailColor = Hex("#FF7A7A");
        private static readonly Color OkTint = Hex("#1D2B24");
        private static readonly Color FailTint = Hex("#32201F");

        private static StandApp app;
        private static ScrollView logView;
        private static Label counters;
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
            root.style.paddingTop = 20;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingBottom = 16;

            var title = new Label("yg-core · стенд  " + StandVersion.Read());
            title.style.fontSize = 32;
            title.style.color = TextMain;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            title.style.flexShrink = 0;
            root.Add(title);

            // Кнопки и журнал делят экран поровну и не вытесняют друг друга.
            var buttons = NewScroll();
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

            var audioCard = Section(buttons, "Звук",
                ("Звук вкл/выкл", () => { app.ToggleSound(); return Task.CompletedTask; }),
                ("Клик", () => { app.PlayClick(); return Task.CompletedTask; }));
            AddSoundStatus(audioCard);

            Section(buttons, "Покупки",
                ("Каталог", () => app.ShowCatalogAsync()),
                ("Купить расходуемый", () => app.BuyAsync("stand_consumable")),
                ("Купить постоянный", () => app.BuyAsync("stand_permanent")),
                ("Невыданные", () => app.RestoreAsync()),
                ("Погасить последнюю", () => ConsumeLast()));

            Section(buttons, "Самопроверки",
                ("Сохранение: запись/чтение", () => app.RunSaveCheckAsync()),
                ("Сохранение: порядок", () => app.RunSaveOrderCheckAsync()),
                ("Язык", () => app.RunLanguageCheckAsync()));

            BuildLog(root);
        }

        private static void BuildLog(VisualElement root)
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.flexShrink = 1;
            panel.style.flexBasis = 0;
            panel.style.minHeight = 220;
            panel.style.marginTop = 10;
            panel.style.backgroundColor = Card;
            Round(panel, 10);
            Outline(panel);
            root.Add(panel);

            // Шапка журнала: название, счётчики OK/FAIL и кнопка очистки.
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.flexShrink = 0;
            head.style.paddingLeft = 14;
            head.style.paddingRight = 8;
            head.style.paddingTop = 6;
            head.style.paddingBottom = 6;
            head.style.borderBottomWidth = 1;
            head.style.borderBottomColor = Border;
            panel.Add(head);

            var name = new Label("ЖУРНАЛ");
            name.style.color = TextMuted;
            name.style.fontSize = 20;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.letterSpacing = 3;
            name.style.flexGrow = 1;
            head.Add(name);

            counters = new Label();
            counters.style.fontSize = 22;
            counters.style.marginRight = 14;
            head.Add(counters);
            RefreshCounters();

            head.Add(MakeButton("Очистить", () => { StandLog.Clear(); return Task.CompletedTask; }, small: true));

            logView = NewScroll();
            logView.style.marginTop = 0;
            panel.Add(logView);

            StandLog.LineAdded += AppendLine;
            StandLog.Cleared += () =>
            {
                logView.contentContainer.Clear();
                RefreshCounters();
            };
        }

        private static void RefreshCounters()
        {
            counters.text = $"OK {StandLog.OkCount}   FAIL {StandLog.FailCount}";
            counters.style.color = StandLog.FailCount > 0 ? FailColor : StandLog.OkCount > 0 ? OkColor : TextMuted;
        }

        private static void AppendLine(StandLog.Entry entry)
        {
            if (logView == null) return;

            var (tag, color, tint) = entry.Kind switch
            {
                StandLogKind.Ok => ("OK", OkColor, OkTint),
                StandLogKind.Fail => ("FAIL", FailColor, FailTint),
                _ => ("INFO", TextMuted, Color.clear),
            };

            // Подстрока ответа (начинается с пробелов) отступает вправо и выглядит вложенной.
            var isSub = entry.Text.StartsWith("  ");

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexShrink = 0;
            row.style.paddingLeft = 12;
            row.style.paddingRight = 10;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.backgroundColor = tint;
            row.style.borderLeftWidth = 4;
            row.style.borderLeftColor = entry.Kind == StandLogKind.Info ? Color.clear : color;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Border;

            var time = new Label(entry.Time);
            time.style.color = TextMuted;
            time.style.fontSize = 18;
            time.style.width = 96;
            time.style.flexShrink = 0;
            row.Add(time);

            var badge = new Label(tag);
            badge.style.color = color;
            badge.style.fontSize = 18;
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.width = 64;
            badge.style.flexShrink = 0;
            row.Add(badge);

            var text = new Label(entry.Text.TrimStart());
            text.style.color = entry.Kind == StandLogKind.Info ? TextMain : color;
            text.style.fontSize = 21;
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.flexGrow = 1;
            text.style.flexShrink = 1;
            text.style.flexBasis = 0;
            text.style.marginLeft = isSub ? 24 : 0;
            if (isSub) text.style.color = TextMuted;
            row.Add(text);

            logView.Add(row);
            RefreshCounters();
            logView.schedule.Execute(() => logView.scrollOffset = new Vector2(0, float.MaxValue));
        }

        private static Task ConsumeLast()
        {
            if (string.IsNullOrEmpty(lastToken))
            {
                StandLog.Info("Погашать нечего: сначала купите товар (токен берётся из последней покупки).");
                return Task.CompletedTask;
            }

            return app.ConsumeAsync(lastToken);
        }

        /// <summary>Запоминает токен последней покупки для кнопки «Погасить последнюю».</summary>
        public static void RememberReceipt(string token) => lastToken = token;

        // ScrollView без темы сжимает содержимое: явно запрещаем сжатие, иначе элементы налезают друг на друга.
        private static ScrollView NewScroll()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.flexBasis = 0;
            scroll.style.minHeight = 160;
            scroll.contentContainer.style.flexShrink = 0;
            scroll.contentContainer.style.flexGrow = 0;

            // Без темы список не обрезает содержимое по своей рамке: включаем обрезку сами.
            scroll.style.overflow = Overflow.Hidden;
            scroll.contentViewport.style.overflow = Overflow.Hidden;
            scroll.contentViewport.style.flexGrow = 1;
            scroll.contentViewport.style.flexShrink = 1;

            // Полоса прокрутки без темы нулевой ширины: задаём ширину и цвета, кнопки со стрелками прячем.
            var bar = scroll.verticalScroller;
            bar.style.width = 14;
            bar.style.flexShrink = 0;
            bar.lowButton.style.display = DisplayStyle.None;
            bar.highButton.style.display = DisplayStyle.None;
            bar.slider.style.flexGrow = 1;
            bar.slider.style.backgroundColor = Background;
            var thumb = bar.slider.Q(className: "unity-base-slider__dragger");
            if (thumb != null)
            {
                thumb.style.backgroundColor = Border;
                Round(thumb, 6);
            }

            return scroll;
        }

        private static VisualElement Section(VisualElement parent, string name, params (string label, Func<Task> action)[] items)
        {
            var card = new VisualElement();
            card.style.flexShrink = 0;
            card.style.backgroundColor = Card;
            card.style.marginBottom = 10;
            card.style.paddingLeft = 14;
            card.style.paddingRight = 10;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 8;
            Round(card, 10);
            Outline(card);

            var header = new Label(name.ToUpperInvariant());
            header.style.color = Accent;
            header.style.fontSize = 20;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.letterSpacing = 3;
            header.style.flexShrink = 0;
            card.Add(header);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.flexShrink = 0;
            foreach (var item in items) row.Add(MakeButton(item.label, item.action, small: false));
            card.Add(row);

            parent.Add(card);
            return card;
        }

        // Строка состояния звука обновляется несколько раз в секунду: так видно, как пауза рекламы глушит и возвращает звук.
        private static void AddSoundStatus(VisualElement card)
        {
            var status = new Label();
            status.style.color = TextMuted;
            status.style.fontSize = 19;
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginTop = 8;
            status.style.flexShrink = 0;
            card.Add(status);
            status.schedule.Execute(() => status.text = app.DescribeSound()).Every(250);
        }

        private static Button MakeButton(string label, Func<Task> action, bool small)
        {
            var button = new Button(async () =>
            {
                try { await action(); }
                catch (Exception e) { StandLog.Fail("Исключение: " + e.Message); }
            }) { text = label };

            button.style.flexShrink = 0;
            button.style.fontSize = small ? 20 : 24;
            button.style.height = small ? 44 : 64;
            button.style.marginRight = 8;
            button.style.marginTop = small ? 0 : 8;
            button.style.paddingLeft = 16;
            button.style.paddingRight = 16;
            button.style.color = TextMain;
            button.style.backgroundColor = Button;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 1;
            button.style.borderTopColor = button.style.borderBottomColor = button.style.borderLeftColor = button.style.borderRightColor = Border;
            Round(button, 8);
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.backgroundColor = ButtonHover);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.backgroundColor = Button);
            return button;
        }

        private static void Round(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius =
                element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
        }

        private static void Outline(VisualElement element)
        {
            element.style.borderTopWidth = element.style.borderBottomWidth = element.style.borderLeftWidth = element.style.borderRightWidth = 1;
            element.style.borderTopColor = element.style.borderBottomColor = element.style.borderLeftColor = element.style.borderRightColor = Border;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out var color);
            return color;
        }
    }
}
