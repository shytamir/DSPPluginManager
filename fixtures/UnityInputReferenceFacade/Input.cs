using System.Collections.Generic;

namespace UnityEngine
{
    public static class Input
    {
        private static readonly HashSet<KeyCode> Down =
            new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> Held =
            new HashSet<KeyCode>();
        private static readonly List<KeyCode> DownQueries =
            new List<KeyCode>();
        private static readonly List<KeyCode> HeldQueries =
            new List<KeyCode>();
        private static readonly List<string> QueryLog =
            new List<string>();

        public static bool GetKeyDown(KeyCode key)
        {
            DownQueries.Add(key);
            QueryLog.Add("Down:" + key);
            return Down.Contains(key);
        }

        public static bool GetKey(KeyCode key)
        {
            HeldQueries.Add(key);
            QueryLog.Add("Held:" + key);
            return Held.Contains(key);
        }

        public static void SetState(KeyCode[] down, KeyCode[] held)
        {
            Down.Clear();
            Held.Clear();
            DownQueries.Clear();
            HeldQueries.Clear();
            QueryLog.Clear();
            foreach (KeyCode key in down)
            {
                Down.Add(key);
            }
            foreach (KeyCode key in held)
            {
                Held.Add(key);
            }
        }

        public static KeyCode[] GetDownQueries()
        {
            return DownQueries.ToArray();
        }

        public static KeyCode[] GetHeldQueries()
        {
            return HeldQueries.ToArray();
        }

        public static string[] GetQueryLog()
        {
            return QueryLog.ToArray();
        }
    }
}
