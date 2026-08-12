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

        private static void RequireKeyboardKey(
            KeyCode value,
            string parameterName
        )
        {
            int numeric = (int)value;
            if (value == KeyCode.None ||
                !Enum.IsDefined(typeof(KeyCode), value) ||
                numeric >= 323)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Only defined keyboard KeyCode values are supported."
                );
            }
        }
    }
}
