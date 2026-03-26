using UnityEngine;

namespace ReelWords
{
    /// <summary>
    /// MonoBehaviour wrapper that owns and exposes the pure-C# <see cref="WordValidator"/>.
    /// Attach to the same GameObject as <see cref="BoardManager"/> or any persistent manager.
    ///
    /// Usage example:
    /// <code>
    ///   // In another MonoBehaviour (wired via Inspector):
    ///   [SerializeField] private WordValidatorBehaviour _wordValidatorBehaviour;
    ///   var result = _wordValidatorBehaviour.Validator.Validate("HELLO");
    /// </code>
    /// </summary>
    public class WordValidatorBehaviour : MonoBehaviour
    {
        // -----------------------------------------------------------------
        //  Serialized configuration
        // -----------------------------------------------------------------

        [Header("Word List")]
        [field: SerializeField]
        [Tooltip("Path to the word-list TextAsset relative to a Resources folder, without extension.")]
        public string WordListPath { get; private set; } = "Data/WordList";

        // -----------------------------------------------------------------
        //  Private state
        // -----------------------------------------------------------------

        private TrieDictionary _trie;
        private WordValidator _validator;

        // -----------------------------------------------------------------
        //  Public accessors
        // -----------------------------------------------------------------

        /// <summary>The pure-C# validator backed by this behaviour's trie dictionary.</summary>
        public WordValidator Validator => _validator;

        /// <summary>True once the backing <see cref="TrieDictionary"/> has finished loading.</summary>
        public bool IsReady => _trie?.IsLoaded ?? false;

        // -----------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            _trie = new TrieDictionary(WordListPath);
            _validator = new WordValidator(_trie);
        }

        private void Start()
        {
            _trie.Load();

            if (!_trie.IsLoaded)
                Debug.LogWarning("[WordValidatorBehaviour] TrieDictionary failed to load — " +
                                 $"check that a TextAsset exists at Resources/{WordListPath}.", this);
            else
                Debug.Log($"[WordValidatorBehaviour] Dictionary loaded: {_trie.WordCount} words.", this);
        }
    }
}
