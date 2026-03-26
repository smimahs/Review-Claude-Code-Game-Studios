using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ReelWords
{
    /// <summary>
    /// Handles scene transitions with a full-screen fade overlay built from UI Toolkit
    /// <see cref="VisualElement"/>s. Subscribes to <see cref="GameStateMachine.OnStateChanged"/>
    /// to automatically load the appropriate scene for each game state.
    ///
    /// The fade overlay is created at runtime as a full-screen, black
    /// <see cref="VisualElement"/> inserted into a dedicated <see cref="UIDocument"/>.
    ///
    /// Usage example:
    /// <code>
    ///   // Triggered automatically via GameStateMachine events.
    ///   // Or call directly:
    ///   await sceneFlow.LoadSceneAsync("GameScene");
    /// </code>
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class SceneFlow : MonoBehaviour
    {
        // -----------------------------------------------------------------
        //  Serialized configuration
        // -----------------------------------------------------------------

        [Header("Fade")]
        [SerializeField]
        [Tooltip("Duration of each fade half (fade-out and fade-in) in seconds.")]
        private float _fadeDuration = 0.3f;

        [Header("Scene Names")]
        [SerializeField]
        [Tooltip("Name of the scene to load when entering MainMenu state.")]
        private string _mainMenuScene = "MainMenu";

        [SerializeField]
        [Tooltip("Name of the scene to load when entering Playing state.")]
        private string _gameScene = "Game";

        [SerializeField]
        [Tooltip("Name of the scene to load when entering GameOver state.")]
        private string _gameOverScene = "GameOver";

        [Header("Dependencies")]
        [SerializeField]
        [Tooltip("State machine whose transitions trigger scene loads.")]
        private GameStateMachine _gameStateMachine;

        // -----------------------------------------------------------------
        //  Private state
        // -----------------------------------------------------------------

        private UIDocument _uiDocument;
        private VisualElement _fadeOverlay;

        // -----------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            CreateFadeOverlay();
        }

        private void Start()
        {
            if (_gameStateMachine == null)
            {
                Debug.LogError("[SceneFlow] GameStateMachine is not assigned.", this);
                return;
            }

            _gameStateMachine.OnStateChanged += HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (_gameStateMachine != null)
                _gameStateMachine.OnStateChanged -= HandleStateChanged;
        }

        // -----------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Fades the screen out, loads <paramref name="sceneName"/> additively then
        /// sets it as active (or loads it in Single mode), then fades back in.
        /// Uses Unity 6 <c>Awaitable</c> for async execution on the main thread.
        /// </summary>
        public async Awaitable LoadSceneAsync(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SceneFlow] LoadSceneAsync called with a null or empty scene name.", this);
                return;
            }

            await FadeAsync(0f, 1f, _fadeDuration);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[SceneFlow] SceneManager.LoadSceneAsync returned null for scene '{sceneName}'. " +
                               "Ensure the scene is added to Build Settings.", this);
                await FadeAsync(1f, 0f, _fadeDuration);
                return;
            }

            // Await the AsyncOperation via Unity 6's built-in awaiter support.
            await op;

            await FadeAsync(1f, 0f, _fadeDuration);
        }

        // -----------------------------------------------------------------
        //  Private helpers
        // -----------------------------------------------------------------

        private void CreateFadeOverlay()
        {
            if (_uiDocument == null)
            {
                Debug.LogError("[SceneFlow] UIDocument component is missing.", this);
                return;
            }

            // Ensure a root VisualElement exists.
            var root = _uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[SceneFlow] UIDocument has no root VisualElement.", this);
                return;
            }

            // The SceneFlow UIDocument is invisible during normal play — make the entire
            // panel pass through input so it never blocks the game board below it.
            root.pickingMode = PickingMode.Ignore;

            _fadeOverlay = new VisualElement
            {
                name = "fade-overlay",
                style =
                {
                    position        = Position.Absolute,
                    left            = 0,
                    top             = 0,
                    right           = 0,
                    bottom          = 0,
                    backgroundColor = new StyleColor(Color.black),
                    opacity         = 0f,
                },
                pickingMode = PickingMode.Ignore,
            };

            // Place at the front of the visual tree.
            _fadeOverlay.style.unitySliceScale = 0; // suppress atlas slice
            root.Add(_fadeOverlay);
            _fadeOverlay.BringToFront();
        }

        private async Awaitable FadeAsync(float fromAlpha, float toAlpha, float duration)
        {
            if (_fadeOverlay == null)
                return;

            var elapsed = 0f;

            // Clamp duration so we never divide by zero.
            var safeDuration = Mathf.Max(duration, float.Epsilon);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                _fadeOverlay.style.opacity = Mathf.Lerp(fromAlpha, toAlpha, t);
                await Awaitable.NextFrameAsync();
            }

            _fadeOverlay.style.opacity = toAlpha;
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
            var sceneName = to switch
            {
                GameState.MainMenu  => _mainMenuScene,
                GameState.Playing   => _gameScene,
                GameState.GameOver  => _gameOverScene,
                GameState.Paused    => null,
                _                   => null,
            };

            // Skip if name is empty or we are already in the target scene (prevents reload loops).
            if (string.IsNullOrEmpty(sceneName)) return;
            if (SceneManager.GetActiveScene().name == sceneName) return;

            LoadSceneFireAndForget(sceneName);
        }

        // async void is intentional — fire-and-forget from a synchronous event handler.
        private async void LoadSceneFireAndForget(string sceneName)
        {
            try { await LoadSceneAsync(sceneName); }
            catch (System.Exception e) { Debug.LogException(e, this); }
        }
    }
}
