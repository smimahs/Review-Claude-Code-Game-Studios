using System.Collections.Generic;

namespace ReelWords
{
    internal class TrieNode
    {
        public readonly Dictionary<char, TrieNode> Children = new();
        public bool IsEndOfWord;
    }
}
