using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReelWords
{
    /// <summary>
    /// Controls the word preview label and SUBMIT/CLEAR buttons.
    /// Reads the current word from BoardManager selection and dispatches submit/clear commands.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WordInputUIController : MonoBehaviour
    {
        [SerializeField] private BoardManager _boardManager;
        public BoardManager BoardManager => _boardManager;
        [SerializeField] private BoardUIController _boardUI;
        public BoardUIController BoardUI => _boardUI;
        [SerializeField] private GameStateMachine _gameStateMachine;
        public GameStateMachine GameStateMachine => _gameStateMachine;

        private UIDocument _document;
        private Label _wordPreview;
        private Button _submitButton;
        private Button _clearButton;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _document.rootVisualElement;

            _wordPreview = root.Q<Label>("word-preview");
            _submitButton = root.Q<Button>("submit-button");
            _clearButton = root.Q<Button>("clear-button");

            if (_submitButton == null || _clearButton == null || _wordPreview == null)
            {
                Debug.LogError("[WordInputUIController] word-preview, submit-button, or clear-button not found in UXML. Check UIDocument source asset.", this);
                return;
            }

            _submitButton.RegisterCallback<ClickEvent>(evt => HandleSubmit());
            _clearButton.RegisterCallback<ClickEvent>(evt => HandleClear());

            if (BoardManager != null)
                BoardManager.OnSelectionChanged += HandleSelectionChanged;

            if (GameStateMachine != null)
                GameStateMachine.OnStateChanged += HandleStateChanged;

            // Set initial state
            UpdateWordPreview(0);
            HandleStateChanged(GameState.MainMenu, GameStateMachine != null ? GameStateMachine.CurrentState : GameState.Playing);
        }

        private void OnDisable()
        {
            if (BoardManager != null)
                BoardManager.OnSelectionChanged -= HandleSelectionChanged;

            if (GameStateMachine != null)
                GameStateMachine.OnStateChanged -= HandleStateChanged;
        }

        // ---------------------------------------------------------------------------
        // Event handlers
        // ---------------------------------------------------------------------------

        private void HandleSelectionChanged(int[] indices)
        {
            int length = (indices != null) ? indices.Length : 0;
            UpdateWordPreview(length);
        }

        private void HandleSubmit()
        {
            if (BoardManager == null) return;
            BoardManager.SubmitWord();
        }

        private void HandleClear()
        {
            if (BoardManager == null) return;
            BoardManager.ClearSelection();
        }

        // ---------------------------------------------------------------------------
        // UI update helpers
        // ---------------------------------------------------------------------------

        private void UpdateWordPreview(int selectionLength)
        {
            string word = (BoardManager != null) ? BoardManager.CurrentWord : "";
            _wordPreview.text = word;

            bool canSubmit = selectionLength >= 2
                && (BoardUI == null || !BoardUI.IsAnyReelAnimating);
            _submitButton.SetEnabled(canSubmit);
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
