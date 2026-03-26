using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ReelWords
{
    /// <summary>
    /// Central coordinator for the game board. Owns the six <see cref="ReelController"/>
    /// instances, manages word selection, validates submissions, and drives scoring.
    ///
    /// Usage example:
    /// <code>
    ///   boardManager.SetSelection(0, 3);   // select reels 0-3
    ///   boardManager.SubmitWord();          // validate and score
    /// </code>
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        // -----------------------------------------------------------------
        //  Serialized configuration
        // -----------------------------------------------------------------

        [Header("Data")]
        [SerializeField]
        [Tooltip("ScriptableObject that defines character sequences for each reel.")]
        private ReelSequenceData _sequenceData;
        public ReelSequenceData SequenceData => _sequenceData;

        [Header("Dependencies")]
        [SerializeField]
        [Tooltip("MonoBehaviour wrapper that exposes the pure-C# WordValidator.")]
        private WordValidatorBehaviour _wordValidatorBehaviour;

        [SerializeField]
        [Tooltip("MonoBehaviour wrapper that exposes the pure-C# ScoringSystem.")]
        private ScoringSystemBehaviour _scoringSystemBehaviour;

        // -----------------------------------------------------------------
        //  Constants
        // -----------------------------------------------------------------

        /// <summary>Fixed number of reels on the board.</summary>
        public const int REEL_COUNT = 6;

        // -----------------------------------------------------------------
        //  Private state
        // -----------------------------------------------------------------

        private ReelController[] _reels;
        private readonly HashSet<string> _foundWords = new();
        private readonly StringBuilder _wordBuilder = new(REEL_COUNT);

        // -----------------------------------------------------------------
        //  Public read-only state
        // -----------------------------------------------------------------

        /// <summary>
        /// Index of the first selected reel (inclusive), or -1 when no selection is active.
        /// </summary>
        public int SelectionStart { get; private set; } = -1;

        /// <summary>
        /// Index of the last selected reel (inclusive), or -1 when no selection is active.
        /// </summary>
        public int SelectionEnd { get; private set; } = -1;

        /// <summary>True when reels have been successfully initialised from SequenceData.</summary>
        public bool IsInitialized => _reels != null && _reels[0] != null;

        /// <summary>True when a contiguous reel range is selected.</summary>
        public bool HasSelection => SelectionStart >= 0;

        /// <summary>
        /// The word formed by the currently selected reels, or an empty string when there
        /// is no active selection.
        /// </summary>
        public string CurrentWord
        {
            get
            {
                if (!HasSelection)
                    return string.Empty;

                _wordBuilder.Clear();
                for (var i = SelectionStart; i <= SelectionEnd; i++)
                    _wordBuilder.Append(_reels[i].CurrentChar);

                return _wordBuilder.ToString();
            }
        }

        // -----------------------------------------------------------------
        //  Events
        // -----------------------------------------------------------------

        /// <summary>
        /// Fired whenever the selection changes. The int[] contains the selected reel
        /// indices in order, or is empty (length 0) when the selection is cleared.
        /// </summary>
        public event Action<int[]> OnSelectionChanged;

        /// <summary>
        /// Fired when a valid, non-duplicate word is accepted.
        /// The int[] contains the indices of the reels that were advanced.
        /// </summary>
        public event Action<int[]> OnWordAccepted;

        /// <summary>
        /// Fired when a submission is rejected for any reason (including invalid words).
        /// Parameters: (attemptedWord, reason).
        /// </summary>
        public event Action<string, BoardValidationReason> OnWordRejected;

        // -----------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            InitializeReels();
        }

        private void InitializeReels()
        {
            if (SequenceData == null)
            {
                Debug.LogError("[BoardManager] SequenceData is not assigned.", this);
                _reels = new ReelController[REEL_COUNT];
                return;
            }

            _reels = new ReelController[REEL_COUNT];
            for (var i = 0; i < REEL_COUNT; i++)
            {
                var sequence = SequenceData.GetSequence(i);
                if (string.IsNullOrEmpty(sequence))
                {
                    Debug.LogError($"[BoardManager] Reel {i} has an empty sequence — using fallback 'A'.", this);
                    sequence = "A";
                }
                _reels[i] = new ReelController(sequence);
            }
        }

        // -----------------------------------------------------------------
        //  Selection API
        // -----------------------------------------------------------------

        /// <summary>
        /// Sets the active selection to the contiguous range [<paramref name="start"/>,
        /// <paramref name="end"/>] (both inclusive). Both values must be in [0, REEL_COUNT-1]
        /// and start must be less than or equal to end.
        /// Fires <see cref="OnSelectionChanged"/> with the selected indices on success.
        /// Logs a warning and does nothing if the range is invalid.
        /// </summary>
        public void SetSelection(int start, int end)
        {
            if (start < 0 || end >= REEL_COUNT || start > end)
            {
                Debug.LogWarning($"[BoardManager] Invalid selection range [{start}, {end}]. " +
                                 $"Must be contiguous within [0, {REEL_COUNT - 1}].", this);
                return;
            }

            SelectionStart = start;
            SelectionEnd = end;
            FireSelectionChanged();
        }

        /// <summary>
        /// Clears the active selection. Fires <see cref="OnSelectionChanged"/> with an
        /// empty array.
        /// </summary>
        public void ClearSelection()
        {
            SelectionStart = -1;
            SelectionEnd = -1;
            OnSelectionChanged?.Invoke(Array.Empty<int>());
        }

        // -----------------------------------------------------------------
        //  Submission API
        // -----------------------------------------------------------------

        /// <summary>
        /// Submits the currently selected word for validation.
        /// Always consumes a turn — fires either <see cref="OnWordAccepted"/> or
        /// <see cref="OnWordRejected"/> regardless of validity.
        ///
        /// On acceptance: advances all selected reels and records the word in the
        /// found-words set.
        /// On rejection: does NOT advance reels.
        /// </summary>
        public void SubmitWord()
        {
            if (!HasSelection)
            {
                Debug.LogWarning("[BoardManager] SubmitWord called with no active selection.", this);
                return;
            }

            var word = CurrentWord;

            // --- Duplicate check (board-level, before validator) ---
            if (_foundWords.Contains(word))
            {
                OnWordRejected?.Invoke(word, BoardValidationReason.Duplicate);
                return;
            }

            // --- Delegate to WordValidator ---
            if (_wordValidatorBehaviour == null || _wordValidatorBehaviour.Validator == null)
            {
                Debug.LogError("[BoardManager] WordValidatorBehaviour is not assigned.", this);
                OnWordRejected?.Invoke(word, BoardValidationReason.DictionaryNotLoaded);
                return;
            }

            var result = _wordValidatorBehaviour.Validator.Validate(word);

            if (!result.IsValid)
            {
                var boardReason = MapValidationReason(result.Reason);
                OnWordRejected?.Invoke(word, boardReason);
                return;
            }

            // --- Accept the word ---
            _foundWords.Add(word);

            var advancedIndices = BuildSelectedIndices();

            for (var i = SelectionStart; i <= SelectionEnd; i++)
                _reels[i].Advance();

            if (_scoringSystemBehaviour != null)
                _scoringSystemBehaviour.Scorer.ScoreWord(word);
            else
                Debug.LogWarning("[BoardManager] ScoringSystemBehaviour is not assigned — score not updated.", this);

            OnWordAccepted?.Invoke(advancedIndices);
            ClearSelection();
        }

        // -----------------------------------------------------------------
        //  Accessor API
        // -----------------------------------------------------------------

        /// <summary>
        /// Returns the character currently showing on reel at <paramref name="reelIndex"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
        public char GetCurrentChar(int reelIndex)
        {
            if (reelIndex < 0 || reelIndex >= REEL_COUNT)
                throw new ArgumentOutOfRangeException(nameof(reelIndex),
                    $"Reel index must be in [0, {REEL_COUNT - 1}].");

            return _reels[reelIndex].CurrentChar;
        }

        // -----------------------------------------------------------------
        //  Reset API
        // -----------------------------------------------------------------

        /// <summary>
        /// Resets the board to its initial state: clears the found-words set, resets
        /// every reel to index 0 (reinitialises from SequenceData), and clears the selection.
        /// </summary>
        public void ResetBoard()
        {
            _foundWords.Clear();
            InitializeReels();
            ClearSelection();
        }

        // -----------------------------------------------------------------
        //  Private helpers
        // -----------------------------------------------------------------

        private void FireSelectionChanged()
        {
            OnSelectionChanged?.Invoke(BuildSelectedIndices());
        }

        private int[] BuildSelectedIndices()
        {
            var count = SelectionEnd - SelectionStart + 1;
            var indices = new int[count];
            for (var i = 0; i < count; i++)
                indices[i] = SelectionStart + i;
            return indices;
        }

        private static BoardValidationReason MapValidationReason(ValidationReason reason)
        {
            return reason switch
            {
                ValidationReason.TooShort          => BoardValidationReason.TooShort,
                ValidationReason.NotInDictionary   => BoardValidationReason.NotInDictionary,
                ValidationReason.DictionaryNotLoaded => BoardValidationReason.DictionaryNotLoaded,
                _                                  => BoardValidationReason.NotInDictionary,
            };
        }
    }

    // -----------------------------------------------------------------
    //  Board-level validation reason enum
    // -----------------------------------------------------------------

    /// <summary>
    /// Extends <see cref="ValidationReason"/> with board-level rejection causes.
    /// <see cref="WordValidator"/> only produces Valid/TooShort/NotInDictionary/DictionaryNotLoaded;
    /// <see cref="BoardManager"/> adds <see cref="Duplicate"/> before delegating.
    /// </summary>
    public enum BoardValidationReason
    {
        /// <summary>The word passed all checks.</summary>
        Valid,

        /// <summary>The word is shorter than the minimum required length.</summary>
        TooShort,

        /// <summary>The word was not found in the dictionary.</summary>
        NotInDictionary,

        /// <summary>The dictionary has not been loaded yet.</summary>
        DictionaryNotLoaded,

        /// <summary>The word has already been found this session.</summary>
        Duplicate,
    }
}
