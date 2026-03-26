using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReelWords
{
    /// <summary>
    /// Displays the remaining turn count and applies a warning style when turns run low.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TurnTimerHUD : MonoBehaviour
    {
        private const int WARNING_THRESHOLD = 3;

        [SerializeField] private TurnManager _turnManager;
        public TurnManager TurnManager => _turnManager;
        [SerializeField] private GameStateMachine _gameStateMachine;
        public GameStateMachine GameStateMachine => _gameStateMachine;

        private UIDocument _document;
        private Label _turnsLabel;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _document.rootVisualElement;
            _turnsLabel = root.Q<Label>("turns-label");

            if (TurnManager != null)
                TurnManager.OnTurnsChanged += HandleTurnsChanged;

            if (GameStateMachine != null)
                GameStateMachine.OnStateChanged += HandleStateChanged;

            HandleStateChanged(GameState.MainMenu, GameStateMachine != null ? GameStateMachine.CurrentState : GameState.Playing);
        }

        private void OnDisable()
        {
            if (TurnManager != null)
                TurnManager.OnTurnsChanged -= HandleTurnsChanged;

            if (GameStateMachine != null)
                GameStateMachine.OnStateChanged -= HandleStateChanged;
        }

        // ---------------------------------------------------------------------------
        // Event handlers
        // ---------------------------------------------------------------------------

        private void HandleTurnsChanged(int turnsRemaining)
        {
            _turnsLabel.text = $"Turns: {turnsRemaining}";

            if (turnsRemaining <= WARNING_THRESHOLD)
                _turnsLabel.AddToClassList("warning");
            else
                _turnsLabel.RemoveFromClassList("warning");
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
