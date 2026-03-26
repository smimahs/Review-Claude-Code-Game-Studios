using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReelWords
{
    /// <summary>
    /// Controls the visual reel board: updates character labels, manages reel window
    /// selection/animation CSS classes, and forwards click interactions to BoardManager.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class BoardUIController : MonoBehaviour
    {
        private const int REEL_COUNT = 6;

        [SerializeField] private BoardManager _boardManager;
        public BoardManager BoardManager => _boardManager;
        [SerializeField] private GameStateMachine _gameStateMachine;
        public GameStateMachine GameStateMachine => _gameStateMachine;

        private UIDocument _document;
        private Label[] _reelCharLabels;
        private VisualElement[] _reelWindows;
        private int _animatingCount = 0;

        // Local selection tracking — mirrors BoardManager state for click logic
        private int _selectionStart = -1;
        private int _selectionEnd = -1;

        /// <summary>Returns true when any reel is mid-advance animation.</summary>
        public bool IsAnyReelAnimating => _animatingCount > 0;

        /// <summary>Returns the screen-space rect of the reel window at the given index.</summary>
        public Rect GetReelScreenRect(int index)
        {
            if (index < 0 || index >= REEL_COUNT)
                return Rect.zero;
            return _reelWindows[index].worldBound;
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _reelCharLabels = new Label[REEL_COUNT];
            _reelWindows = new VisualElement[REEL_COUNT];
        }

        private void OnEnable()
        {
            var root = _document.rootVisualElement;

            for (int i = 0; i < REEL_COUNT; i++)
            {
                _reelWindows[i]    = root.Q($"reel-{i}");
                _reelCharLabels[i] = root.Q<Label>($"reel-char-{i}");

                if (_reelWindows[i] == null)
                {
                    Debug.LogError($"[BoardUIController] reel-{i} not found in UXML. Check UIDocument source asset.", this);
                    return;
                }

                // Capture index for lambda
                int capturedIndex = i;
                _reelWindows[i].RegisterCallback<ClickEvent>(evt => OnReelClicked(capturedIndex));
            }

            if (BoardManager != null)
            {
                BoardManager.OnSelectionChanged += HandleSelectionChanged;
                BoardManager.OnWordAccepted += HandleWordAccepted;
            }

            if (GameStateMachine != null)
                GameStateMachine.OnStateChanged += HandleStateChanged;

            // Apply initial visibility
            HandleStateChanged(GameState.MainMenu, GameStateMachine != null ? GameStateMachine.CurrentState : GameState.Playing);
        }

        private void OnDisable()
        {
            for (int i = 0; i < REEL_COUNT; i++)
            {
                // Unregistering with null-safe check: just re-query and remove callbacks
                // by disabling the element's interaction (callbacks auto-deregister on disable)
            }

            if (BoardManager != null)
            {
                BoardManager.OnSelectionChanged -= HandleSelectionChanged;
                BoardManager.OnWordAccepted -= HandleWordAccepted;
            }

            if (GameStateMachine != null)
                GameStateMachine.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (_boardManager == null || !_boardManager.IsInitialized) return;

            // Keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                _boardManager.SubmitWord();
            else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                _boardManager.ClearSelection();

            // Sync reel character labels
            for (int i = 0; i < REEL_COUNT; i++)
            {
                if (_reelCharLabels[i] == null) continue;
                char c = _boardManager.GetCurrentChar(i);
                string charStr = c == '\0' ? "" : c.ToString();
                if (_reelCharLabels[i].text != charStr)
                    _reelCharLabels[i].text = charStr;
            }
        }

        // ---------------------------------------------------------------------------
        // Click selection logic
        // ---------------------------------------------------------------------------

        private void OnReelClicked(int index)
        {
            if (BoardManager == null) return;

            // If nothing selected, start a new selection at this reel
            if (_selectionStart < 0)
            {
                _selectionStart = index;
                _selectionEnd = index;
                BoardManager.SetSelection(_selectionStart, _selectionEnd);
                return;
            }

            // Clicking the same reel that is the sole selection → clear
            if (_selectionStart == _selectionEnd && index == _selectionStart)
            {
                _selectionStart = -1;
                _selectionEnd = -1;
                BoardManager.ClearSelection();
                return;
            }

            // Clicking adjacent to the current selection range → extend contiguously
            int newStart = _selectionStart;
            int newEnd = _selectionEnd;

            if (index == newStart - 1)
            {
                newStart = index;
            }
            else if (index == newEnd + 1)
            {
                newEnd = index;
            }
            else if (index >= newStart && index <= newEnd)
            {
                // Clicking inside the selection trims the end to this index
                newEnd = index;
            }
            else
            {
                // Non-contiguous click → start fresh selection
                newStart = index;
                newEnd = index;
            }

            _selectionStart = newStart;
            _selectionEnd = newEnd;
            BoardManager.SetSelection(_selectionStart, _selectionEnd);
        }

        // ---------------------------------------------------------------------------
        // BoardManager event handlers
        // ---------------------------------------------------------------------------

        private void HandleSelectionChanged(int[] indices)
        {
            _selectionStart = (indices != null && indices.Length > 0) ? indices[0] : -1;
            _selectionEnd   = (indices != null && indices.Length > 0) ? indices[indices.Length - 1] : -1;

            for (int i = 0; i < REEL_COUNT; i++)
            {
                bool isSelected = indices != null && System.Array.IndexOf(indices, i) >= 0;
                if (isSelected)
                    _reelWindows[i].AddToClassList("selected");
                else
                    _reelWindows[i].RemoveFromClassList("selected");
            }
        }

        private void HandleWordAccepted(int[] advancedIndices)
        {
            if (advancedIndices == null) return;

            foreach (int idx in advancedIndices)
            {
                if (idx < 0 || idx >= REEL_COUNT) continue;

                _animatingCount++;
                _reelWindows[idx].AddToClassList("animating");

                // Capture for closure
                int capturedIdx = idx;
                _reelWindows[idx].schedule.Execute(() =>
                {
                    _reelWindows[capturedIdx].RemoveFromClassList("animating");
                    _animatingCount = Mathf.Max(0, _animatingCount - 1);
                }).StartingIn(300); // 0.30s in milliseconds
            }
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
