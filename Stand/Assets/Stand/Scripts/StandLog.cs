using System;
using System.Collections.Generic;
using UnityEngine;

namespace YgCoreStand
{
    /// <summary>Вид записи журнала: от него зависит цвет метки на экране.</summary>
    public enum StandLogKind
    {
        Info,
        Ok,
        Fail,
    }

    /// <summary>Журнал стенда: хранит записи и сообщает экрану, что пора добавить строку.</summary>
    public static class StandLog
    {
        private const int MaxLines = 300;
        private static readonly List<Entry> Lines = new List<Entry>();

        /// <summary>Одна запись журнала.</summary>
        public readonly struct Entry
        {
            public Entry(StandLogKind kind, string time, string text)
            {
                Kind = kind;
                Time = time;
                Text = text;
            }

            public StandLogKind Kind { get; }
            public string Time { get; }
            public string Text { get; }
        }

        /// <summary>Вызывается при каждой новой записи.</summary>
        public static event Action<Entry> LineAdded;

        /// <summary>Вызывается после очистки журнала.</summary>
        public static event Action Cleared;

        public static int FailCount { get; private set; }
        public static int OkCount { get; private set; }

        public static void Info(string text) => Add(StandLogKind.Info, text);

        /// <summary>Самопроверка прошла. Считается в итоговой сводке.</summary>
        public static void Ok(string text)
        {
            OkCount++;
            Add(StandLogKind.Ok, text);
        }

        /// <summary>Самопроверка провалена. Считается в итоговой сводке.</summary>
        public static void Fail(string text)
        {
            FailCount++;
            Add(StandLogKind.Fail, text);
        }

        /// <summary>Записывает OK или FAIL по условию.</summary>
        public static void Check(bool condition, string text)
        {
            if (condition) Ok(text); else Fail(text);
        }

        /// <summary>Стирает записи и счётчики OK/FAIL.</summary>
        public static void Clear()
        {
            Lines.Clear();
            OkCount = 0;
            FailCount = 0;
            Cleared?.Invoke();
        }

        private static void Add(StandLogKind kind, string text)
        {
            var entry = new Entry(kind, DateTime.Now.ToString("HH:mm:ss"), text);
            if (Lines.Count >= MaxLines) Lines.RemoveAt(0);
            Lines.Add(entry);
            Debug.Log($"[Стенд] {kind}: {text}");
            LineAdded?.Invoke(entry);
        }
    }
}
