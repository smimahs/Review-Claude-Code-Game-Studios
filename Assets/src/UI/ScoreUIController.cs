using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReelWords
{
    /// <summary>
    /// Displays the running session score and triggers score flyout animations
    /// when a word is accepted.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ScoreUIController : MonoBehaviour
    {
        private const int FLYOUT_POOL_SIZE = 4;

        [SerializeField] private ScoringSystemBehaviour _scoringSystem;
        public ScoringSystemBehaviour ScoringSystem => _scoringSystem;
        [SerializeField] private BoardManager _boardManager;
        public BoardManager BoardManager => _boardManager;
        [SerializeField] private BoardUIController _boardUI;
        public BoardUIController BoardUI => _boardUI;
        [SerializeField] private GameStateMachine _gameStateMachine;
        public GameStateMachine GameStateMachine => _gameStateMachine;

        private UIDocument _document;
        private Label _scoreLabel;

        private readonly List<Label> _flyoutPool = new(FLYOUT_POOL_SIZE);
        private int[] _lastAdvancedIndices;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _document.rootVisualElement;
            _scoreLabel = root.Q<Label>("score-label");

            if (ScoringSystem != null && ScoringSystem.Scorer != null)
                ScoringSystem.Scorer.OnScoreChanged += HandleScoreChanged;

            if (GameStateMachine != null)
                GameStateMachine.OnGameStarted += HandleGameStarted;

            if (BoardManager != null)
                BoardManager.OnWordAccepted += HandleWordAccepted;

            if (GameStateMachine != null)
                GameStateMachine.OnStateChanged += HandleStateChanged;

            _scoreLabel.text = "0";
            HandleStateChanged(GameState.MainMenu, GameStateMachine != null ? GameStateMachine.CurrentState : GameState.Playing);
        }

        private void OnDisable()
        {
            if (ScoringSystem != null && ScoringSystem.Scorer != null)
                ScoringSystem.Scorer.OnScoreChanged -= HandleScoreChanged;

            if (BoardManager != null)
                BoardManager.OnWordAccepted -= HandleWordAccepted;

            if (GameStateMachine != null)
            {
                GameStateMachine.OnGameStarted -= HandleGameStarted;
                GameStateMachine.OnStateChanged -= HandleStateChanged;
            }
        }

        // ---------------------------------------------------------------------------
        // Event handlers
        // ---------------------------------------------------------------------------

        private void HandleWordAccepted(int[] advancedIndices)
        {
            // Cache indices so HandleScoreChanged can look them up for flyout positioning
            _lastAdvancedIndices = advancedIndices;
        }

        private void HandleScoreChanged(int sessionScore, int wordScore)
        {
            _scoreLabel.text = sessionScore.ToString();

            if (wordScore > 0)
                TriggerFlyout(wordScore);
        }

        private void HandleGameStarted()
        {
            _scoreLabel.text = "0";
        }

        // ---------------------------------------------------------------------------
        // Flyout animation
        // ---------------------------------------------------------------------------

        private void TriggerFlyout(int wordScore)
        {
            if (BoardUI == null) return;

            // Pick the middle reel of the advanced indices for flyout origin, fallback to reel 0
            int anchorIndex = 0;
            if (_lastAdvancedIndices != null && _lastAdvancedIndices.Length > 0)
                anchorIndex = _lastAdvancedIndices[_lastAdvancedIndices.Length / 2];

            Rect reelRect = BoardUI.GetReelScreenRect(anchorIndex);
            float startX = reelRect.x + reelRect.width / 2f - 40f; // rough centering offset
            float startY = reelRect.y - 10f;

            var root = _document.rootVisualElement;
            Label flyout = GetOrCreateFlyout();
            flyout.text = $"+{wordScore} pts";
            flyout.AddToClassList("flyout-label");

            flyout.style.position = Position.Absolute;
            flyout.style.left = startX;
            flyout.style.top = startY;
            flyout.style.opacity = 1f;

            root.Add(flyout);

            float elapsed = 0f;
            const float totalDuration = 1.0f;
            const float riseDistance = 80f;
            const float fadeStartRatio = 0.6f;

            IVisualElementScheduledItem anim = null;
            anim = flyout.schedule.Execute(() =>
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / totalDuration);
                flyout.style.top = startY - riseDistance * t;

                float fadeT = Mathf.Clamp01(
                    (elapsed - totalDuration * fadeStartRatio)
                    / (totalDuration * (1f - fadeStartRatio)));
                flyout.style.opacity = 1f - fadeT;

                if (elapsed >= totalDuration)
                {
                    root.Remove(flyout);
                    ReturnFlyoutToPool(flyout);
                    anim?.Pause();
                }
            }).Every(16); // ~60 fps
        }

        private Label GetOrCreateFlyout()
        {
            if (_flyoutPool.Count > 0)
            {
                int last = _flyoutPool.Count - 1;
                Label pooled = _flyoutPool[last];
                _flyoutPool.RemoveAt(last);
                return pooled;
            }
            return new Label();
        }

        private void ReturnFlyoutToPool(Label flyout)
        {
            flyout.RemoveFromClassList("flyout-label");
            if (_flyoutPool.Count < FLYOUT_POOL_SIZE)
                _flyoutPool.Add(flyout);
            // If pool is full the label is simply discarded (GC'd)
        }

        // ---------------------------------------------------------------------------
        // State visibility
        // ---------------------------------------------------------------------------

        private void HandleStateChanged(GameState _, GameState newState)
        {
            var root = _document.rootVisualElement;
            root.style.display = (newState == GameState.Playing)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
