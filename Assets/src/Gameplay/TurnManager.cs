using System;
using UnityEngine;

namespace ReelWords
{
    /// <summary>
    /// Tracks remaining turns for a single game session.
    /// Subscribes to <see cref="BoardManager.OnWordAccepted"/> and
    /// <see cref="BoardManager.OnWordRejected"/> so that every board submission
    /// (valid or not) consumes exactly one turn.
    ///
    /// Usage example:
    /// <code>
    ///   turnManager.Initialize(20);   // start a 20-turn game
    ///   // ... board events fire ConsumeTurn automatically
    ///   // subscribe to OnTurnsExhausted to trigger game-over
    /// </code>
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        // -----------------------------------------------------------------
        //  Serialized configuration
        // -----------------------------------------------------------------

        [Header("Configuration")]
        [field: SerializeField]
        [Tooltip("Default number of turns when Initialize() is called without arguments.")]
        public int TurnsPerGame { get; private set; } = 20;

        [Header("Dependencies")]
        [SerializeField]
        [Tooltip("BoardManager whose submission events this TurnManager listens to.")]
        private BoardManager _boardManager;

        // -----------------------------------------------------------------
        //  Public state
        // -----------------------------------------------------------------

        /// <summary>Number of turns remaining in the current game session.</summary>
        public int TurnsRemaining { get; private set; }

        // -----------------------------------------------------------------
        //  Events
        // -----------------------------------------------------------------

        /// <summary>Fired after every change to <see cref="TurnsRemaining"/>. Parameter is the new value.</summary>
        public event Action<int> OnTurnsChanged;

        /// <summary>Fired once when <see cref="TurnsRemaining"/> reaches zero.</summary>
        public event Action OnTurnsExhausted;

        // -----------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------

        private void Start()
        {
            if (_boardManager == null)
            {
                Debug.LogError("[TurnManager] BoardManager reference is not assigned.", this);
                return;
            }

            _boardManager.OnWordAccepted += HandleWordAccepted;
            _boardManager.OnWordRejected += HandleWordRejected;
        }

        private void OnDestroy()
        {
            if (_boardManager != null)
            {
                _boardManager.OnWordAccepted -= HandleWordAccepted;
                _boardManager.OnWordRejected -= HandleWordRejected;
            }
        }

        // -----------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Begins a new game with <paramref name="turns"/> turns. Fires
        /// <see cref="OnTurnsChanged"/> immediately with the starting count.
        /// </summary>
        public void Initialize(int turns)
        {
            TurnsRemaining = turns;
            OnTurnsChanged?.Invoke(TurnsRemaining);
        }

        /// <summary>
        /// Decrements <see cref="TurnsRemaining"/> by one. Fires <see cref="OnTurnsChanged"/>
        /// and, if the count reaches zero, fires <see cref="OnTurnsExhausted"/>.
        /// Does nothing if TurnsRemaining is already zero.
        /// </summary>
        public void ConsumeTurn()
        {
            if (TurnsRemaining <= 0)
                return;

            TurnsRemaining--;
            OnTurnsChanged?.Invoke(TurnsRemaining);

            if (TurnsRemaining == 0)
                OnTurnsExhausted?.Invoke();
        }

        // -----------------------------------------------------------------
        //  Event handlers
        // -----------------------------------------------------------------

        private void HandleWordAccepted(int[] _) => ConsumeTurn();

        private void HandleWordRejected(string _, BoardValidationReason __) => ConsumeTurn();
    }
}
