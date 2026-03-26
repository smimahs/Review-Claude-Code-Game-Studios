using System;
using UnityEngine;

namespace ReelWords
{
    /// <summary>
    /// ScriptableObject data asset mapping letters to their point values.
    /// </summary>
    [CreateAssetMenu(menuName = "ReelWords/Letter Value Table")]
    public class LetterValueTable : ScriptableObject
    {
        [Serializable]
        public class LetterEntry
        {
            public char Letter;
            public int Value;
        }

        [SerializeField] private LetterEntry[] _entries;

        /// <summary>Returns the point value for <paramref name="letter"/>, or 0 if unknown.</summary>
        public int GetValue(char letter)
        {
            var upper = char.ToUpperInvariant(letter);

            if (_entries != null)
            {
                foreach (var entry in _entries)
                {
                    if (char.ToUpperInvariant(entry.Letter) == upper)
                        return entry.Value;
                }
            }

#if UNITY_EDITOR
            Debug.LogWarning($"[LetterValueTable] No value defined for letter '{letter}'. Returning 0.");
#endif
            return 0;
        }

        /// <summary>
        /// Fills <c>_entries</c> with standard Scrabble letter values.
        /// Invoke from the Inspector via right-click → Initialize Defaults.
        /// </summary>
        [ContextMenu("Initialize Defaults")]
        public void InitializeDefaults()
        {
            _entries = new LetterEntry[]
            {
                new() { Letter = 'A', Value = 1  },
                new() { Letter = 'B', Value = 3  },
                new() { Letter = 'C', Value = 3  },
                new() { Letter = 'D', Value = 2  },
                new() { Letter = 'E', Value = 1  },
                new() { Letter = 'F', Value = 4  },
                new() { Letter = 'G', Value = 2  },
                new() { Letter = 'H', Value = 4  },
                new() { Letter = 'I', Value = 1  },
                new() { Letter = 'J', Value = 8  },
                new() { Letter = 'K', Value = 5  },
                new() { Letter = 'L', Value = 1  },
                new() { Letter = 'M', Value = 3  },
                new() { Letter = 'N', Value = 1  },
                new() { Letter = 'O', Value = 1  },
                new() { Letter = 'P', Value = 3  },
                new() { Letter = 'Q', Value = 10 },
                new() { Letter = 'R', Value = 1  },
                new() { Letter = 'S', Value = 1  },
                new() { Letter = 'T', Value = 1  },
                new() { Letter = 'U', Value = 1  },
                new() { Letter = 'V', Value = 4  },
                new() { Letter = 'W', Value = 4  },
                new() { Letter = 'X', Value = 8  },
                new() { Letter = 'Y', Value = 4  },
                new() { Letter = 'Z', Value = 10 },
            };

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
