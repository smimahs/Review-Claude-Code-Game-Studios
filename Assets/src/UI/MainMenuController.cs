using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ReelWords
{
    /// <summary>
    /// Controls the main menu screen: PLAY and QUIT button callbacks.
    /// Visible only in the MainMenu game state.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private GameModeManager _gameModeManager;
        public GameModeManager GameModeManager => _gameModeManager;
        [SerializeField] private GameStateMachine _gameStateMachine;
        public GameStateMachine GameStateMachine => _gameStateMachine;

        private UIDocument _document;
        private Button _playButton;
        private Button _quitButton;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _document.rootVisualElement;

            _playButton = root.Q<Button>("play-button");
            _quitButton = root.Q<Button>("quit-button");

            if (_playButton == null || _quitButton == null)
            {
                Debug.LogError("[MainMenuController] play-button or quit-button not found in UXML. Check UIDocument source asset.", this);
                return;
            }

            _playButton.RegisterCallback<ClickEvent>(evt => HandlePlay());
            _quitButton.RegisterCallback<ClickEvent>(evt => HandleQuit());

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

        private void HandlePlay()
        {
            if (GameModeManager != null)
                GameModeManager.StartGame();
        }

        private void HandleQuit()
        {
            Application.Quit();
        }

        // ---------------------------------------------------------------------------
        // State visibility
        // ---------------------------------------------------------------------------

        private void HandleStateChanged(GameState _, GameState newState)
        {
            var root = _document.rootVisualElement;
            root.style.display = (newState == GameState.MainMenu)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
