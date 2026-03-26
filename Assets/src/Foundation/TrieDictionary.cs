using System;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("ReelWords.Tests")]

namespace ReelWords
{
    /// <summary>
    /// Pure C# trie-backed dictionary loaded from a Unity Resources TextAsset.
    /// </summary>
    public class TrieDictionary
    {
        private readonly string _resourcePath;
        private readonly TrieNode _root;

        /// <summary>Number of words inserted into the trie. Set after <see cref="Load"/> completes.</summary>
        public int WordCount { get; private set; }

        /// <summary>True after a successful <see cref="Load"/> call, even if the word list is empty.</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>Creates a dictionary that will load from <c>Resources/{resourcePath}</c>.</summary>
        /// <param name="resourcePath">Path relative to any Resources folder, without extension.</param>
        public TrieDictionary(string resourcePath = "Data/WordList")
        {
            _resourcePath = resourcePath;
            _root = new TrieNode();
        }

        /// <summary>
        /// Internal constructor for tests — accepts a pre-populated root node so no
        /// Resources load is required.
        /// </summary>
        internal TrieDictionary(TrieNode preloadedRoot, int wordCount)
        {
            _resourcePath = string.Empty;
            _root = preloadedRoot;
            WordCount = wordCount;
            IsLoaded = true;
        }

        /// <summary>
        /// Loads the word list from <c>Resources/{resourcePath}</c>.
        /// Each line is trimmed, uppercased, and inserted into the trie.
        /// </summary>
        public void Load()
        {
            var asset = Resources.Load<TextAsset>(_resourcePath);

            if (asset == null)
            {
                Debug.LogWarning($"[TrieDictionary] Word list not found at Resources/{_resourcePath}. Dictionary is empty.");
                return;
            }

            var text = asset.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning($"[TrieDictionary] Word list at Resources/{_resourcePath} is empty.");
                IsLoaded = true;
                WordCount = 0;
                return;
            }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var word = line.Trim().ToUpperInvariant();
                if (word.Length > 0)
                    Insert(word);
            }

            IsLoaded = true;
        }

        /// <summary>
        /// Returns true if the word exists in the dictionary.
        /// Case-insensitive. Returns false for null, empty, or non-alpha input without throwing.
        /// </summary>
        public bool Contains(string word)
        {
            try
            {
                if (string.IsNullOrEmpty(word))
                    return false;

                var upper = word.ToUpperInvariant();

                foreach (var ch in upper)
                {
                    if (!char.IsLetter(ch))
                        return false;
                }

                var node = _root;
                foreach (var ch in upper)
                {
                    if (!node.Children.TryGetValue(ch, out var next))
                        return false;
                    node = next;
                }

                return node.IsEndOfWord;
            }
            catch
            {
                return false;
            }
        }

        private void Insert(string word)
        {
            var node = _root;
            foreach (var ch in word)
            {
                if (!node.Children.TryGetValue(ch, out var next))
                {
                    next = new TrieNode();
                    node.Children[ch] = next;
                }
                node = next;
            }

            if (!node.IsEndOfWord)
            {
                node.IsEndOfWord = true;
                WordCount++;
            }
        }
    }
}
