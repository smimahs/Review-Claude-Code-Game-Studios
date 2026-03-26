using UnityEngine;

namespace ReelWords
{
    /// <summary>
    /// MonoBehaviour wrapper that owns and exposes the pure-C# <see cref="ScoringSystem"/>.
    /// Attach to the same GameObject as <see cref="BoardManager"/> or any persistent manager.
    ///
    /// Usage example:
    /// <code>
    ///   // In another MonoBehaviour (wired via Inspector):
    ///   [SerializeField] private ScoringSystemBehaviour _scoringSystemBehaviour;
    ///   _scoringSystemBehaviour.Scorer.ScoreWord("HELLO");
    ///   int total = _scoringSystemBehaviour.Scorer.SessionScore;
    /// </code>
    /// </summary>
    public class ScoringSystemBehaviour : MonoBehaviour
    {
        // -----------------------------------------------------------------
        //  Serialized configuration
        // -----------------------------------------------------------------

        [Header("Letter Values")]
        [SerializeField]
        [Tooltip("ScriptableObject mapping letters to their point values.")]
        private LetterValueTable _letterValues;
        public LetterValueTable LetterValues => _letterValues;

        // -----------------------------------------------------------------
        //  Private state
        // -----------------------------------------------------------------

        private ScoringSystem _scorer;

        // -----------------------------------------------------------------
        //  Public accessor
        // -----------------------------------------------------------------

        /// <summary>The pure-C# scoring system managed by this behaviour.</summary>
        public ScoringSystem Scorer => _scorer;

        // -----------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            if (_letterValues == null)
                Debug.LogError("[ScoringSystemBehaviour] LetterValues is not assigned — " +
                               "all letter scores will be 0.", this);

            _scorer = new ScoringSystem(_letterValues);
        }
    }
}
