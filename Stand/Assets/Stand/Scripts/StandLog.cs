using System;
using System.Collections.Generic;
using UnityEngine;

namespace YgCoreStand
{
    /// <summary>Журнал стенда: хранит строки и сообщает экрану, что пора перерисовать список.</summary>
    public static class StandLog
    {
        private const int MaxLines = 300;
        private static readonly List<string> Lines = new List<string>();

        /// <summary>Вызывается при каждой новой строке.</summary>
        public static event Action<string> LineAdded;

        public static int FailCount { get; private set; }
        public static int OkCount { get; private set; }

        public static void Info(string text) => Add("·", text);

        /// <summary>Самопроверка прошла. Считается в итоговой сводке.</summary>
        public static void Ok(string text)
        {
            OkCount++;
            Add("OK  ", text);
        }

        /// <summary>Самопроверка провалена. Считается в итоговой сводке.</summary>
        public static void Fail(string text)
        {
            FailCount++;
            Add("FAIL", text);
        }

        /// <summary>Записывает OK или FAIL по условию.</summary>
        public static void Check(bool condition, string text)
        {
            if (condition) Ok(text); else Fail(text);
        }

        private static void Add(string tag, string text)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {tag} {text}";
            if (Lines.Count >= MaxLines) Lines.RemoveAt(0);
            Lines.Add(line);
            Debug.Log("[Стенд] " + line);
            LineAdded?.Invoke(line);
        }
    }
}
