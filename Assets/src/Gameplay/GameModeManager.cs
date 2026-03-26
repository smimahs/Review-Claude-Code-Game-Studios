using System;
using UnityEngine;

namespace ReelWords
{
    /// <summary>
    /// Selects and configures the active game mode, then drives the
    /// <see cref="GameStateMachine"/> in response to mode-specific end conditions.
    ///
    /// Usage example:
    /// <code>
    ///   gameModeManager.StartGame();   // initialise turns / timer and enter Playing state
    /// </code>
    /// </summary>
    public class GameModeManager : MonoBehaviour
    {
        // -----------------------------------------------------------------
        //  Game mode enum
        // -----------------------------------------------------------------

        /// <summary>Supported game modes.</summary>
        public enum GameMode
        {
            /// <summary>Game ends when all turns are consumed.</summary>
            TurnLimit,

            /// <summary>Game ends when the countdown timer reaches zero.</summary>
            Timer,
        }

        // -----------------------------------------------------------------
        //  Serialized configuration
        // -----------------------------------------------------------------

        [Header("Mode")]
        [field: SerializeField]
        [Tooltip("Which game mode to run when StartGame() is called.")]
        public GameMode ActiveMode { get; private set; } = GameMode.TurnLimit;

        [SerializeField]
        [Tooltip("If true, StartGame() is called automatically on Start — skips the main menu.")]
        private bool _autoStart = true;

        [Header("Dependencies")]
        [SerializeField]
        [Tooltip("TurnManager used in TurnLimit mode.")]
        private TurnManager _turnManager;

        [SerializeField]
        [Tooltip("Top-level state machine driven by this manager.")]
        private GameStateMachine _gameStateMachine;

        // -----------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------

        private void Start()
        {
            if (_turnManager == null)
                Debug.LogWarning("[GameModeManager] TurnManager is not assigned — " +
                                 "TurnLimit mode will not function correctly.", this);
            else
                _turnManager.OnTurnsExhausted += HandleTurnsExhausted;

            if (_gameStateMachine == null)
                Debug.LogError("[GameModeManager] GameStateMachine is not assigned.", this);

            if (_autoStart)
                StartGame();
        }

        private void OnDestroy()
        {
            if (_turnManager != null)
                _turnManager.OnTurnsExhausted -= HandleTurnsExhausted;
        }

        // -----------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Starts a new game session. Initialises the active mode's sub-systems
        /// and transitions the <see cref="GameStateMachine"/> to
        /// <see cref="GameState.Playing"/>.
        /// </summary>
        public void StartGame()
        {
            if (_gameStateMachine == null)
            {
                Debug.LogError("[GameModeManager] Cannot start game — GameStateMachine is not assigned.", this);
                return;
            }

            switch (ActiveMode)
            {
                case GameMode.TurnLimit:
                    if (_turnManager != null)
                        _turnManager.Initialize(_turnManager.TurnsPerGame);
                    else
                        Debug.LogWarning("[GameModeManager] TurnManager not assigned — skipping turn initialisation.", this);
                    break;

                case GameMode.Timer:
                    // Timer mode sub-system initialisation is handled by a future TimerManager.
                    Debug.LogWarning("[GameModeManager] Timer mode is not yet implemented.", this);
                    break;
            }

            _gameStateMachine.TransitionTo(GameState.Playing);
        }

        // -----------------------------------------------------------------
        //  Event handlers
        // -----------------------------------------------------------------

        private void HandleTurnsExhausted()
        {
            if (_gameStateMachine == null)
            {
                Debug.LogError("[GameModeManager] Cannot transition to GameOver — GameStateMachine is not assigned.", this);
                return;
            }

            _gameStateMachine.TransitionTo(GameState.GameOver);
        }
    }
}
