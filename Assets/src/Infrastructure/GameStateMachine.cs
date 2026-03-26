using System;
using UnityEngine;

namespace ReelWords
{
    /// <summary>
    /// Top-level finite state machine for the game.
    /// Enforces legal transitions and fires granular events so listeners can react
    /// without needing to interpret raw state pairs.
    ///
    /// Legal transitions:
    /// <list type="bullet">
    ///   <item>MainMenu → Playing</item>
    ///   <item>Playing → Paused</item>
    ///   <item>Playing → GameOver</item>
    ///   <item>Paused → Playing</item>
    ///   <item>GameOver → MainMenu</item>
    /// </list>
    ///
    /// Usage example:
    /// <code>
    ///   gameStateMachine.TransitionTo(GameState.Playing);
    ///   gameStateMachine.OnGameStarted += HandleGameStarted;
    /// </code>
    /// </summary>
    public class GameStateMachine : MonoBehaviour
    {
        // -----------------------------------------------------------------
        //  Public state
        // -----------------------------------------------------------------

        /// <summary>The current game state. Starts at <see cref="GameState.MainMenu"/>.</summary>
        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        // -----------------------------------------------------------------
        //  Events
        // -----------------------------------------------------------------

        /// <summary>
        /// Fired on every successful transition.
        /// Parameters: (fromState, toState).
        /// </summary>
        public event Action<GameState, GameState> OnStateChanged;

        /// <summary>Fired when transitioning from MainMenu to Playing.</summary>
        public event Action OnGameStarted;

        /// <summary>Fired when transitioning to GameOver.</summary>
        public event Action OnGameOver;

        /// <summary>Fired when transitioning from Playing to Paused.</summary>
        public event Action OnGamePaused;

        /// <summary>Fired when transitioning from Paused back to Playing.</summary>
        public event Action OnGameResumed;

        // -----------------------------------------------------------------
        //  Transition API
        // -----------------------------------------------------------------

        /// <summary>
        /// Attempts to transition to <paramref name="newState"/>.
        /// Logs a warning and does nothing if the transition is not legal from the
        /// current state. Fires <see cref="OnStateChanged"/> and the appropriate
        /// granular event on success.
        /// </summary>
        public void TransitionTo(GameState newState)
        {
            if (!IsLegalTransition(CurrentState, newState))
            {
                Debug.LogWarning($"[GameStateMachine] Illegal transition: {CurrentState} → {newState}. " +
                                 "Request ignored.", this);
                return;
            }

            var previous = CurrentState;
            CurrentState = newState;

            OnStateChanged?.Invoke(previous, newState);
            FireGranularEvent(previous, newState);
        }

        // -----------------------------------------------------------------
        //  Private helpers
        // -----------------------------------------------------------------

        private static bool IsLegalTransition(GameState from, GameState to)
        {
            return (from, to) switch
            {
                (GameState.MainMenu,  GameState.Playing)  => true,
                (GameState.Playing,   GameState.Paused)   => true,
                (GameState.Playing,   GameState.GameOver) => true,
                (GameState.Paused,    GameState.Playing)  => true,
                (GameState.GameOver,  GameState.MainMenu) => true,
                _                                         => false,
            };
        }

        private void FireGranularEvent(GameState from, GameState to)
        {
            switch (to)
            {
                case GameState.Playing when from == GameState.MainMenu:
                    OnGameStarted?.Invoke();
                    break;

                case GameState.Playing when from == GameState.Paused:
                    OnGameResumed?.Invoke();
                    break;

                case GameState.Paused:
                    OnGamePaused?.Invoke();
                    break;

                case GameState.GameOver:
                    OnGameOver?.Invoke();
                    break;
            }
        }
    }

    // -----------------------------------------------------------------
    //  GameState enum
    // -----------------------------------------------------------------

    /// <summary>Top-level states of the game.</summary>
    public enum GameState
    {
        /// <summary>The main menu / title screen is active.</summary>
        MainMenu,

        /// <summary>A game session is in progress.</summary>
        Playing,

        /// <summary>The session is suspended (pause menu visible).</summary>
        Paused,

        /// <summary>The session has ended and the results screen is shown.</summary>
        GameOver,
    }
}
