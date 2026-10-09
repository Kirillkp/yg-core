using System;
using YgCore.Core;

// Проверка поведения чистой логики Core вне Unity.
static class P
{
    static int fails;
    static void Check(string name, bool ok) { Console.WriteLine((ok ? "ок   " : "FAIL ") + name); if (!ok) fails++; }

    static int Main()
    {
        // Пауза: складывается из причин, снимается только когда сняты все.
        var pc = new PauseCoordinator();
        int events = 0; pc.PauseChanged += (s, e) => events++;
        pc.RequestPause(PauseReason.User); pc.RequestPause(PauseReason.Ad); pc.RequestPause(PauseReason.Ad);
        Check("пауза: две причины, повтор не шлёт событие", pc.IsPaused && events == 2);
        pc.ReleasePause(PauseReason.Ad);
        Check("пауза остаётся, пока есть User", pc.IsPaused && pc.HasReason(PauseReason.User));
        pc.ReleasePause(PauseReason.User);
        Check("пауза снята, когда сняты все", !pc.IsPaused);
        bool threw = false; try { pc.RequestPause(PauseReason.User | PauseReason.Ad); } catch (ArgumentException) { threw = true; }
        Check("составной флаг отклоняется", threw);
        threw = false; try { pc.RequestPause(PauseReason.None); } catch (ArgumentException) { threw = true; }
        Check("None отклоняется", threw);

        // Экономика: транзакции идемпотентны.
        var eco = new SessionEconomyService();
        Check("начисление", eco.TryCredit("t1", 100, "test") && eco.Balance == 100);
        Check("повтор транзакции игнорируется", !eco.TryCredit("t1", 100, "test") && eco.Balance == 100);
        Check("списание", eco.TrySpend("t2", 30, "test") && eco.Balance == 70);
        Check("нельзя уйти в минус", !eco.TrySpend("t3", 71, "test") && eco.Balance == 70);
        Check("пустой id отклоняется", !eco.TryCredit("", 5, "x") && !eco.TryCredit(null, 5, "x"));
        Check("ноль и минус отклоняются", !eco.TryCredit("t4", 0, "x") && !eco.TryCredit("t5", -5, "x"));
        eco.RestoreBalance(-10);
        Check("восстановление не даёт отрицательный баланс", eco.Balance == 0);

        // Состояния: старт из меню без отсчёта.
        var pc2 = new PauseCoordinator(); var gs = new GameplayStateService(pc2);
        gs.CompleteBoot();
        Check("Boot -> Menu", gs.CurrentState == AppState.Menu);
        gs.StartRun();
        Check("старт из меню: сразу Run без отсчёта", gs.CurrentState == AppState.Run && gs.IsGameplayActive && !pc2.IsPaused);
        pc2.RequestPause(PauseReason.User);
        Check("пауза переводит в Pause и останавливает геймплей", gs.CurrentState == AppState.Pause && !gs.IsGameplayActive);
        pc2.ReleasePause(PauseReason.User);
        Check("после снятия паузы ставится отсчёт Countdown", gs.CurrentState == AppState.Pause && pc2.HasReason(PauseReason.Countdown));
        pc2.ReleasePause(PauseReason.Countdown);
        Check("после отсчёта возврат в Run", gs.CurrentState == AppState.Run && gs.IsGameplayActive);
        gs.FinishRun();
        Check("FinishRun -> Result", gs.CurrentState == AppState.Result && !gs.IsGameplayActive);
        gs.StartRun();
        Check("рестарт не из меню: Pause с отсчётом", gs.CurrentState == AppState.Pause && pc2.HasReason(PauseReason.Countdown));
        pc2.ReleasePause(PauseReason.Countdown);
        Check("после отсчёта рестарта Run", gs.CurrentState == AppState.Run && gs.IsGameplayActive);

        // Игра, которой отсчёт не нужен, снимает Countdown сразу в обработчике события паузы.
        var pc3 = new PauseCoordinator(); var gs3 = new GameplayStateService(pc3);
        pc3.PauseChanged += (s, e) => { if (e.CurrentReasons.HasFlag(PauseReason.Countdown)) pc3.ReleasePause(PauseReason.Countdown); };
        gs3.CompleteBoot(); gs3.StartRun();
        pc3.RequestPause(PauseReason.User); pc3.ReleasePause(PauseReason.User);
        Check("без отсчёта: после паузы сразу Run", gs3.CurrentState == AppState.Run && gs3.IsGameplayActive && !pc3.IsPaused);
        gs3.FinishRun(); gs3.StartRun();
        Check("без отсчёта: рестарт сразу Run", gs3.CurrentState == AppState.Run && gs3.IsGameplayActive && !pc3.IsPaused);

        // Настройки.
        var st = new SessionGameSettingsService();
        Check("громкости по умолчанию 0.5 / 0.1", st.MusicVolume == 0.5f && st.SfxVolume == 0.1f);
        st.SetMusicVolume(2f);
        Check("громкость ограничивается 0..1", st.MusicVolume == 1f);
        var st2 = new SessionGameSettingsService(0.8f, 0.3f);
        Check("свои громкости по умолчанию", st2.MusicVolume == 0.8f && st2.SfxVolume == 0.3f);

        Console.WriteLine(fails == 0 ? "\nВсе проверки пройдены." : $"\nПровалено: {fails}");
        return fails == 0 ? 0 : 1;
    }
}
