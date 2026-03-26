using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReelWords
{
    /// <summary>
    /// Controls the game over screen: displays final score and word count,
    /// offers PLAY AGAIN and MAIN MENU actions.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameOverScreenController : MonoBehaviour
    {
        private const float SCORE_COUNT_UP_DURATION = 1.5f;

        [SerializeField] private ScoringSystemBehaviour _scoringSystem;
        public ScoringSystemBehaviour ScoringSystem => _scoringSystem;
        [SerializeField] private GameModeManager _gameModeManager;
        public GameModeManager GameModeManager => _gameModeManager;
        [SerializeField] private GameStateMachine _gameStateMachine;
        public GameStateMachine GameStateMachine => _gameStateMachine;

        private UIDocument _document;
        private Label _finalScoreLabel;
        private Label _wordsFoundLabel;
        private Button _restartButton;
        private Button _menuButton;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _document.rootVisualElement;

            _finalScoreLabel = root.Q<Label>("final-score-label");
            _wordsFoundLabel = root.Q<Label>("words-found-label");
            _restartButton = root.Q<Button>("restart-button");
            _menuButton = root.Q<Button>("menu-button");

            _restartButton.RegisterCallback<ClickEvent>(evt => HandleRestart());
            _menuButton.RegisterCallback<ClickEvent>(evt => HandleMenu());

            if (GameStateMachine != null)
                GameStateMachine.OnStateChanged += HandleStateChanged;

            HandleStateChanged(GameState.MainMenu, GameStateMachine != null ? GameStateMachine.CurrentState : GameState.MainMenu);
        }

        private void OnDisable()
        {
            if (GameStateMachine != null)
                GameStateMachine.OnStateChanged -= HandleStateChanged;
        }

        // ---------------------------------------------------------------------------
        // Button handlers
        // ---------------------------------------------------------------------------

        private void HandleRestart()
        {
            if (GameModeManager != null)
                GameModeManager.StartGame();
        }

        private void HandleMenu()
        {
            if (GameStateMachine != null)
                GameStateMachine.TransitionTo(GameState.MainMenu);
        }

        // ---------------------------------------------------------------------------
        // Score display and count-up animation
        // ---------------------------------------------------------------------------

        private void ShowGameOverStats()
        {
            if (ScoringSystem == null || ScoringSystem.Scorer == null) return;

            int finalScore = ScoringSystem.Scorer.SessionScore;
            int wordsFound = ScoringSystem.Scorer.WordsFoundCount;

            _wordsFoundLabel.text = $"Words: {wordsFound}";

            // Animate score counting up
            AnimateScoreCountUp(finalScore);
        }

        private void AnimateScoreCountUp(int targetScore)
        {
            float elapsed = 0f;
            _finalScoreLabel.text = "Score: 0";

            IVisualElementScheduledItem anim = null;
            anim = _finalScoreLabel.schedule.Execute(() =>
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / SCORE_COUNT_UP_DURATION);
                // Ease-out curve
                float eased = 1f - (1f - t) * (1f - t);
                int displayScore = Mathf.RoundToInt(eased * targetScore);
                _finalScoreLabel.text = $"Score: {displayScore}";

                if (elapsed >= SCORE_COUNT_UP_DURATION)
                {
                    _finalScoreLabel.text = $"Score: {targetScore}";
                    anim?.Pause();
                }
            }).Every(16); // ~60 fps
        }

        // ---------------------------------------------------------------------------
        // State visibility
        // ---------------------------------------------------------------------------

        private void HandleStateChanged(GameState _, GameState newState)
        {
            var root = _document.rootVisualElement;

            if (newState == GameState.GameOver)
            {
                root.style.display = DisplayStyle.Flex;
                ShowGameOverStats();
            }
            else
            {
                root.style.display = DisplayStyle.None;
            }
        }
    }
}
