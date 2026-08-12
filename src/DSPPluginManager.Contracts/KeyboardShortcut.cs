using System;
using System.Collections.Generic;
using UnityEngine;

namespace DSPPluginManager.Contracts
{
    public readonly struct KeyboardShortcut : IEquatable<KeyboardShortcut>
    {
        private static readonly KeyboardShortcut UnsetValue =
            new KeyboardShortcut(KeyCode.None, Array.Empty<KeyCode>(), true);
        private static Func<KeyboardShortcut, bool> poll;

        private readonly KeyCode mainKey;
        private readonly KeyCode[] heldKeys;

        public KeyboardShortcut(KeyCode mainKey, params KeyCode[] heldKeys)
            : this(mainKey, heldKeys, false)
        {
        }

        private KeyboardShortcut(
            KeyCode mainKey,
            KeyCode[] heldKeys,
            bool creatingUnset
        )
        {
            if (heldKeys == null)
            {
                throw new ArgumentNullException("heldKeys");
            }
            if (mainKey == KeyCode.None)
            {
                if (heldKeys.Length != 0)
                {
                    throw new ArgumentException(
                        "An unset shortcut cannot contain held keys.",
                        "heldKeys"
                    );
                }
                this.mainKey = KeyCode.None;
                this.heldKeys = Array.Empty<KeyCode>();
                return;
            }
            if (creatingUnset)
            {
                throw new ArgumentException(
                    "The internal unset shortcut must use KeyCode.None.",
                    "mainKey"
                );
            }

            RequireKeyboardKey(mainKey, "mainKey");
            SortedSet<KeyCode> normalized = new SortedSet<KeyCode>();
            foreach (KeyCode heldKey in heldKeys)
            {
                RequireKeyboardKey(heldKey, "heldKeys");
                if (heldKey != mainKey)
                {
                    normalized.Add(heldKey);
                }
            }

            this.mainKey = mainKey;
            this.heldKeys = new KeyCode[normalized.Count];
            normalized.CopyTo(this.heldKeys);
        }

        public static KeyboardShortcut Unset
        {
            get { return UnsetValue; }
        }

        public bool IsDown()
        {
            if (mainKey == KeyCode.None)
            {
                return false;
            }
            Func<KeyboardShortcut, bool> current = poll;
            if (current == null)
            {
                throw new InvalidOperationException(
                    "The host has not prepared shortcut polling."
                );
            }
            return current(this);
        }

        public bool Equals(KeyboardShortcut other)
        {
            if (mainKey != other.mainKey)
            {
                return false;
            }
            KeyCode[] left = heldKeys ?? Array.Empty<KeyCode>();
            KeyCode[] right = other.heldKeys ?? Array.Empty<KeyCode>();
            if (left.Length != right.Length)
            {
                return false;
            }
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is KeyboardShortcut other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)mainKey;
                foreach (KeyCode heldKey in heldKeys ?? Array.Empty<KeyCode>())
                {
                    hash = (hash * 397) ^ (int)heldKey;
                }
                return hash;
            }
        }

        public override string ToString()
        {
            if (mainKey == KeyCode.None)
            {
                return "Not set";
            }
            if (heldKeys.Length == 0)
            {
                return mainKey.ToString();
            }
            string[] names = new string[heldKeys.Length + 1];
            names[0] = mainKey.ToString();
            for (int index = 0; index < heldKeys.Length; index++)
            {
                names[index + 1] = heldKeys[index].ToString();
            }
            return string.Join(" + ", names);
        }

        public static bool operator ==(
            KeyboardShortcut left,
            KeyboardShortcut right
        )
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            KeyboardShortcut left,
            KeyboardShortcut right
        )
        {
            return !left.Equals(right);
        }

        internal static void InitializePolling(
            Func<KeyboardShortcut, bool> value
        )
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (poll != null)
            {
                throw new InvalidOperationException(
                    "Shortcut polling has already been prepared."
                );
            }
            poll = value;
        }

        internal string ToPersistedString()
        {
            return mainKey == KeyCode.None ? string.Empty : ToString();
        }

        internal static bool TryParse(
            string serializedValue,
            out KeyboardShortcut shortcut
        )
        {
            shortcut = Unset;
            if (serializedValue == null)
            {
                return false;
            }
            if (serializedValue.Length == 0)
            {
                return true;
            }
            if (serializedValue.IndexOf(',') >= 0 ||
                serializedValue.IndexOf(';') >= 0 ||
                serializedValue.IndexOf('|') >= 0)
            {
                return false;
            }

            string[] tokens = serializedValue.Split('+');
            KeyCode[] keys = new KeyCode[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
            {
                string token = tokens[index].Trim(' ');
                KeyCode key;
                if (token.Length == 0 ||
                    !TryParseDefinedName(token, out key))
                {
                    return false;
                }
                keys[index] = key;
            }

            if (keys[0] == KeyCode.None)
            {
                return false;
            }
            for (int index = 0; index < keys.Length; index++)
            {
                if (!IsKeyboardKey(keys[index]))
                {
                    return false;
                }
            }

            KeyCode[] held = new KeyCode[keys.Length - 1];
            Array.Copy(keys, 1, held, 0, held.Length);
            shortcut = new KeyboardShortcut(keys[0], held);
            return true;
        }

        private static void RequireKeyboardKey(
            KeyCode value,
            string parameterName
        )
        {
            if (!IsKeyboardKey(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Only defined keyboard KeyCode values are supported."
                );
            }
        }

        private static bool IsKeyboardKey(KeyCode value)
        {
            return value != KeyCode.None &&
                Enum.IsDefined(typeof(KeyCode), value) &&
                (int)value < (int)KeyCode.Mouse0;
        }

        private static bool TryParseDefinedName(
            string value,
            out KeyCode key
        )
        {
            foreach (string name in Enum.GetNames(typeof(KeyCode)))
            {
                if (string.Equals(name, value, StringComparison.Ordinal))
                {
                    key = (KeyCode)Enum.Parse(typeof(KeyCode), name, false);
                    return true;
                }
            }

            key = KeyCode.None;
            return false;
        }
    }
}
