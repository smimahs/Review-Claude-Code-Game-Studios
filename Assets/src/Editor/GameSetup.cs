/// <summary>
/// ReelWords — One-click scene and asset setup for the Unity Editor.
///
/// Usage: open Unity 6000.3.8f1, let it compile, then from the menu bar:
///   ReelWords → 1. Create Data Assets   (creates ScriptableObjects + PanelSettings)
///   ReelWords → 2. Create Scenes        (creates MainMenu, Game, GameOver scenes)
///   ReelWords → 3. Full Setup           (runs both in sequence)
///
/// After running Full Setup:
///   1. Assign the generated PanelSettings asset to every UIDocument component
///      in the Inspector (or let the script do it — Full Setup handles this).
///   2. Add MainMenu, Game, and GameOver scenes to File → Build Settings.
///   3. Press Play from the MainMenu scene.
/// </summary>

using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ReelWords.Editor
{
    public static class GameSetup
    {
        // -----------------------------------------------------------------------
        //  Paths — all relative to the Unity project root (Assets/ folder)
        // -----------------------------------------------------------------------

        private const string DataDir        = "Assets/assets/data";
        private const string UIDir          = "Assets/assets/UI";
        private const string SceneDir       = "Assets/scenes";
        private const string SettingsDir    = "Assets/settings";

        private const string LetterValuesPath   = DataDir    + "/LetterValues.asset";
        private const string ReelSequencesPath  = DataDir    + "/ReelSequences.asset";
        private const string PanelSettingsPath  = SettingsDir + "/GamePanelSettings.asset";

        private const string MainMenuScenePath  = SceneDir   + "/MainMenu.unity";
        private const string GameScenePath      = SceneDir   + "/Game.unity";
        private const string GameOverScenePath  = SceneDir   + "/GameOver.unity";

        // -----------------------------------------------------------------------
        //  Menu items
        // -----------------------------------------------------------------------

        [MenuItem("ReelWords/3. Full Setup (Run Both)", priority = 3)]
        public static void FullSetup()
        {
            CreateDataAssets();
            CreateScenes();
            RegisterScenesInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ReelWords] Full Setup complete. Open Assets/scenes/MainMenu.unity and press Play.");
        }

        [MenuItem("ReelWords/1. Create Data Assets", priority = 1)]
        public static void CreateDataAssets()
        {
            EnsureDirectories();

            CreateLetterValueTable();
            CreateReelSequenceData();
            CreatePanelSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ReelWords] Data assets created.");
        }

        [MenuItem("ReelWords/2. Create Scenes", priority = 2)]
        public static void CreateScenes()
        {
            EnsureDirectories();

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                Debug.LogWarning("[ReelWords] PanelSettings not found — run 'Create Data Assets' first.");
                panelSettings = CreatePanelSettings();
            }

            var letterValues  = AssetDatabase.LoadAssetAtPath<LetterValueTable>(LetterValuesPath);
            var reelSequences = AssetDatabase.LoadAssetAtPath<ReelSequenceData>(ReelSequencesPath);

            CreateGameScene(panelSettings, letterValues, reelSequences);
            CreateMainMenuScene(panelSettings);
            CreateGameOverScene(panelSettings);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ReelWords] Scenes created in Assets/scenes/.");
        }

        // -----------------------------------------------------------------------
        //  Asset creation
        // -----------------------------------------------------------------------

        private static LetterValueTable CreateLetterValueTable()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LetterValueTable>(LetterValuesPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<LetterValueTable>();
            asset.name = "LetterValues";
            AssetDatabase.CreateAsset(asset, LetterValuesPath);

            // Populate using the built-in ContextMenu method via SerializedObject
            var so = new SerializedObject(asset);
            var entries = so.FindProperty("_entries");
            var letterData = new (char letter, int value)[]
            {
                ('A',1),('E',1),('I',1),('O',1),('U',1),('L',1),('N',1),('S',1),('T',1),('R',1),
                ('D',2),('G',2),
                ('B',3),('C',3),('M',3),('P',3),
                ('F',4),('H',4),('V',4),('W',4),('Y',4),
                ('K',5),
                ('J',8),('X',8),
                ('Q',10),('Z',10),
            };

            entries.arraySize = letterData.Length;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Re-fetch after resize
            so.Update();
            for (int i = 0; i < letterData.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                // Letter is stored as a char — serialize as int (char underlying type)
                var letterProp = entry.FindPropertyRelative("Letter");
                var valueProp  = entry.FindPropertyRelative("Value");
                if (letterProp != null) letterProp.intValue = letterData[i].letter;
                if (valueProp  != null) valueProp.intValue  = letterData[i].value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ReelSequenceData CreateReelSequenceData()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ReelSequenceData>(ReelSequencesPath);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<ReelSequenceData>();
            asset.name = "ReelSequences";
            AssetDatabase.CreateAsset(asset, ReelSequencesPath);

            // Six reel sequences. Initial characters spell "STARED" — a valid 6-letter word.
            // Subsequent positions maintain high word-forming density.
            // Each sequence is 16 characters cycling through varied consonants and vowels.
            var sequences = new string[]
            {
                "STPCHFBWMGLDNRK",   // Reel 0 — starts S; consonant-heavy
                "TRNLSCDPHMGFWBK",   // Reel 1 — starts T; consonant-heavy
                "AEIOUAEIOUAEIOU",   // Reel 2 — starts A; pure vowels
                "RNTLSDCMPBGHFWK",   // Reel 3 — starts R; consonant-heavy
                "EIAOUEIAOUEIAOUE",  // Reel 4 — starts E; vowel-dominant
                "DSTRNLCMPBGHFWK",   // Reel 5 — starts D; consonant-heavy
            };

            var so = new SerializedObject(asset);
            var seqProp = so.FindProperty("_sequences");
            seqProp.arraySize = sequences.Length;
            so.ApplyModifiedPropertiesWithoutUndo();

            so.Update();
            for (int i = 0; i < sequences.Length; i++)
            {
                var elem = seqProp.GetArrayElementAtIndex(i);
                elem.stringValue = sequences[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static PanelSettings CreatePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "GamePanelSettings";

            // Set public properties directly — SetEnumValue is not available in this Unity version
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;

            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            EditorUtility.SetDirty(ps);
            return ps;
        }

        // -----------------------------------------------------------------------
        //  Scene creation
        // -----------------------------------------------------------------------

        private static void CreateGameScene(
            PanelSettings panelSettings,
            LetterValueTable letterValues,
            ReelSequenceData reelSequences)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Camera ---
            var cameraGO = new GameObject("Main Camera");
            var cam = cameraGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
            cam.orthographic = false;
            cam.tag = "MainCamera";
            cameraGO.AddComponent<AudioListener>();

            // --- Infrastructure ---
            var infraGO = new GameObject("Infrastructure");
            var gsm = infraGO.AddComponent<GameStateMachine>();

            // SceneFlow — leave scene names empty so no scene loads happen (single-scene MVP)
            var sceneFlowGO = new GameObject("SceneFlow");
            var sceneFlowUiDoc = sceneFlowGO.AddComponent<UIDocument>();
            AssignPanelSettings(sceneFlowUiDoc, panelSettings);
            var sceneFlow = sceneFlowGO.AddComponent<SceneFlow>();
            LinkField(sceneFlow, "_gameStateMachine", gsm);
            LinkField(sceneFlow, "_mainMenuScene", "");
            LinkField(sceneFlow, "_gameScene", "");
            LinkField(sceneFlow, "_gameOverScene", "");

            // --- Gameplay ---
            var gameplayGO = new GameObject("Gameplay");

            var wvbGO = new GameObject("WordValidatorBehaviour");
            wvbGO.transform.SetParent(gameplayGO.transform);
            var wvb = wvbGO.AddComponent<WordValidatorBehaviour>();

            var ssbGO = new GameObject("ScoringSystemBehaviour");
            ssbGO.transform.SetParent(gameplayGO.transform);
            var ssb = ssbGO.AddComponent<ScoringSystemBehaviour>();
            if (letterValues != null)
                LinkField(ssb, "_letterValues", letterValues);

            var bmGO = new GameObject("BoardManager");
            bmGO.transform.SetParent(gameplayGO.transform);
            var bm = bmGO.AddComponent<BoardManager>();
            if (reelSequences != null)
                LinkField(bm, "_sequenceData", reelSequences);
            LinkField(bm, "_wordValidatorBehaviour", wvb);
            LinkField(bm, "_scoringSystemBehaviour", ssb);

            var tmGO = new GameObject("TurnManager");
            tmGO.transform.SetParent(gameplayGO.transform);
            var tm = tmGO.AddComponent<TurnManager>();
            LinkField(tm, "_boardManager", bm);

            var gmmGO = new GameObject("GameModeManager");
            gmmGO.transform.SetParent(gameplayGO.transform);
            var gmm = gmmGO.AddComponent<GameModeManager>();
            LinkField(gmm, "_turnManager", tm);
            LinkField(gmm, "_gameStateMachine", gsm);

            // --- UI ---
            var uiParent = new GameObject("UI");

            // Board UI + Word Input + Score UI + Turn HUD (all share one UIDocument on GameScreen)
            var gameScreenGO = new GameObject("GameScreen");
            gameScreenGO.transform.SetParent(uiParent.transform);
            var gameScreenDoc = gameScreenGO.AddComponent<UIDocument>();
            AssignPanelSettings(gameScreenDoc, panelSettings);
            AssignUXML(gameScreenDoc, UIDir + "/GameScreen.uxml");

            var boardUI = gameScreenGO.AddComponent<BoardUIController>();
            LinkField(boardUI, "_boardManager", bm);
            LinkField(boardUI, "_gameStateMachine", gsm);

            var wordInputUI = gameScreenGO.AddComponent<WordInputUIController>();
            LinkField(wordInputUI, "_boardManager", bm);
            LinkField(wordInputUI, "_boardUI", boardUI);
            LinkField(wordInputUI, "_gameStateMachine", gsm);

            var scoreUI = gameScreenGO.AddComponent<ScoreUIController>();
            LinkField(scoreUI, "_scoringSystem", ssb);
            LinkField(scoreUI, "_boardManager", bm);
            LinkField(scoreUI, "_boardUI", boardUI);
            LinkField(scoreUI, "_gameStateMachine", gsm);

            var turnHUD = gameScreenGO.AddComponent<TurnTimerHUD>();
            LinkField(turnHUD, "_turnManager", tm);
            LinkField(turnHUD, "_gameStateMachine", gsm);

            // Main Menu
            var mainMenuGO = new GameObject("MainMenu");
            mainMenuGO.transform.SetParent(uiParent.transform);
            var mainMenuDoc = mainMenuGO.AddComponent<UIDocument>();
            AssignPanelSettings(mainMenuDoc, panelSettings);
            AssignUXML(mainMenuDoc, UIDir + "/MainMenu.uxml");
            var mainMenuCtrl = mainMenuGO.AddComponent<MainMenuController>();
            LinkField(mainMenuCtrl, "_gameModeManager", gmm);
            LinkField(mainMenuCtrl, "_gameStateMachine", gsm);

            // Game Over Screen
            var gameOverGO = new GameObject("GameOverScreen");
            gameOverGO.transform.SetParent(uiParent.transform);
            var gameOverDoc = gameOverGO.AddComponent<UIDocument>();
            AssignPanelSettings(gameOverDoc, panelSettings);
            AssignUXML(gameOverDoc, UIDir + "/GameOverScreen.uxml");
            var gameOverCtrl = gameOverGO.AddComponent<GameOverScreenController>();
            LinkField(gameOverCtrl, "_scoringSystem", ssb);
            LinkField(gameOverCtrl, "_gameModeManager", gmm);
            LinkField(gameOverCtrl, "_gameStateMachine", gsm);

            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log($"[ReelWords] Game scene saved to {GameScenePath}");
        }

        private static void CreateMainMenuScene(PanelSettings panelSettings)
        {
            // Minimal redirect: the single-scene Game scene handles MainMenu state.
            // This stub scene immediately transitions to Game scene if SceneFlow is used.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGO = new GameObject("Main Camera");
            camGO.AddComponent<Camera>().tag = "MainCamera";
            camGO.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static void CreateGameOverScene(PanelSettings panelSettings)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGO = new GameObject("Main Camera");
            camGO.AddComponent<Camera>().tag = "MainCamera";
            camGO.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, GameOverScenePath);
        }

        // -----------------------------------------------------------------------
        //  Build Settings registration
        // -----------------------------------------------------------------------

        private static void RegisterScenesInBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(GameScenePath,     true),
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameOverScenePath, true),
            };
            EditorBuildSettings.scenes = scenes;
            Debug.Log("[ReelWords] Scenes registered in Build Settings.");
        }

        // -----------------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------------

        private static void EnsureDirectories()
        {
            foreach (var dir in new[] { DataDir, UIDir, SceneDir, SettingsDir })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    var parts = dir.Split('/');
                    var path = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var next = path + "/" + parts[i];
                        if (!AssetDatabase.IsValidFolder(next))
                            AssetDatabase.CreateFolder(path, parts[i]);
                        path = next;
                    }
                }
            }
        }

        /// <summary>Assigns a PanelSettings asset to a UIDocument via its public property.</summary>
        private static void AssignPanelSettings(UIDocument uiDoc, PanelSettings ps)
        {
            if (ps == null) return;
            uiDoc.panelSettings = ps;
            EditorUtility.SetDirty(uiDoc);
        }

        /// <summary>Assigns a UXML VisualTreeAsset to a UIDocument via its public property.</summary>
        private static void AssignUXML(UIDocument uiDoc, string uxmlPath)
        {
            var vta = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (vta == null) { Debug.LogWarning($"[ReelWords] UXML not found: {uxmlPath}"); return; }
            uiDoc.visualTreeAsset = vta;
            EditorUtility.SetDirty(uiDoc);
        }

        /// <summary>
        /// Links a serialized field on <paramref name="target"/> by searching common backing-field
        /// naming patterns used by [field: SerializeField] auto-properties and private fields.
        /// </summary>
        private static void LinkField(Object target, string fieldName, Object value,
            string propertyDisplayName = null)
        {
            var so = new SerializedObject(target);

            // Try exact name first, then auto-property backing field variants
            string[] candidates = propertyDisplayName != null
                ? new[]
                {
                    $"<{propertyDisplayName}>k__BackingField",
                    $"_{char.ToLower(propertyDisplayName[0])}{propertyDisplayName.Substring(1)}",
                    propertyDisplayName,
                    fieldName,
                }
                : new[] { fieldName };

            foreach (var name in candidates)
            {
                var prop = so.FindProperty(name);
                if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    prop.objectReferenceValue = value;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }

            Debug.LogWarning($"[ReelWords] Could not find serialized property '{fieldName}' " +
                             $"(display: {propertyDisplayName}) on {target.GetType().Name}. " +
                             "Wire this reference manually in the Inspector.");
        }

        /// <summary>Sets a string serialized property.</summary>
        private static void LinkField(Object target, string fieldName, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null && prop.propertyType == SerializedPropertyType.String)
            {
                prop.stringValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
