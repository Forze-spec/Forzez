using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public struct PlayerComponents {
    public Animator Animator;
    public PlayerCore Core;
    public PlayerStateMachine StateMachine;
    public PlayerMovable Movable;
    public MovementController MovementController;
    public FightController FightController;
    public MagicArrowController MagicArrowController;
}

public class GameManager: MonoBehaviour {
    public GameObject playerPrefab;
    public GameObject inGameUiPrefab;

    [Scene]
    public string newGameStartScene;
    public Vector2 newGameStartSpawnPosition;

    [BoxGroup("Debug mode")]
    [InfoBox("Debug mode allows you to spawn game state data on a scene where GameManager component is exists. " +
             "It spawns player and in-game UI. Player will be spawned at GameManager object position.")]
    public bool debugModeEnabled;

    // END INSPECTOR DECLARATION

    public static GameManager Instance;

    public PlayerComponents player;
    public InputActionMap playerInputActions;
    public InputActionMap uiInputActions;

    public event ValueChangedEvent<string> LastCheckpointIdChanged;
    [CanBeNull] private string _lastCheckpointId;
    [CanBeNull] public string LastCheckpointId { 
        get => _lastCheckpointId;
        private set {
            var oldValue = _lastCheckpointId;
            _lastCheckpointId = value;
            LastCheckpointIdChanged?.Invoke(oldValue, _lastCheckpointId);
        }
    }
    
    private SceneLoader _sceneLoader;
    private SavesManager _saveManager;
    private SaveData? _currentSave;
    private GameObject _playerObject;
    private GameObject _inGameUiObject;
    private string _mainMenuSceneName;

    public List<SaveMetadata> SavesMeta => _saveManager.SavesMeta.Values.ToList();
    
    private void InitializeManager() {
        //Инициализация компонента управления
        var input = GetComponent<PlayerInput>();
        if (!input) {
            throw new NullReferenceException("No PlayerInput component on global object");
        }

        playerInputActions = input.actions.FindActionMap("Player", true);
        uiInputActions = input.actions.FindActionMap("UI", true);

        _sceneLoader = new SceneLoader();
        _saveManager = new SavesManager();
        
        _mainMenuSceneName = _sceneLoader.CurrentScene.name;
    }

    private void InitializePlayer() {
        if (!playerPrefab) {
            throw new NullReferenceException("The player's prefab is not set");
        }
        
        // Инициализация компонент игрока
        _playerObject = Instantiate(playerPrefab);

        DontDestroyOnLoad(_playerObject);
        
        var animator = _playerObject.GetComponent<Animator>();
        if (!animator) {
            throw new NullReferenceException("No Animator component on player object");
        }

        var core = _playerObject.GetComponent<PlayerCore>();
        if (!core) {
            throw new NullReferenceException("No UnitCore component on player object");
        }
        
        var stateMachine = _playerObject.GetComponent<PlayerStateMachine>();
        if (!stateMachine) {
            throw new NullReferenceException("No PlayerStatus component on player object");
        }
        
        var movable = _playerObject.GetComponent<PlayerMovable>();
        if (!movable) {
            throw new NullReferenceException("No PlayerMovable component on player object");
        }

        var movementController = _playerObject.GetComponent<MovementController>();
        if (!movementController) {
            throw new NullReferenceException("No MovementController component on player object");
        }

        var fightController = _playerObject.GetComponent<FightController>();
        if (!fightController) {
            throw new NullReferenceException("No FightController component on player object");
        }
        
        var magicArrowController = _playerObject.GetComponent<MagicArrowController>();
        if (!magicArrowController) {
            throw new NullReferenceException("No MagicArrowController component on player object");
        }

        player = new PlayerComponents {
            Animator = animator,
            Core = core,
            StateMachine = stateMachine,
            Movable = movable,
            MovementController = movementController,
            FightController = fightController,
            MagicArrowController = magicArrowController
        };

        player.Core.Initialize();
        player.StateMachine.Initialize();
        player.Movable.Initialize();
        player.MovementController.Initialize();
        player.FightController.Initialize();
        player.MagicArrowController.Initialize();
    }

    private void InitializeInGameUi() {
        if (!inGameUiPrefab) {
            throw new NullReferenceException("The InGameUi's prefab is not set");
        }
        
        _inGameUiObject = Instantiate(inGameUiPrefab);
        DontDestroyOnLoad(_inGameUiObject);
    }
    
    private void InitializeDebugMode() {
        _currentSave = null;
        InitializePlayer();
        _sceneLoader.Init(_playerObject);
        _playerObject.transform.position = (Vector2)transform.position;
        InitializeInGameUi();
    }

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        } else {
            Debug.LogWarning("Game manager already exists on the scene. New copy will be destroyed");
            DestroyImmediate(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        InitializeManager();

        if (debugModeEnabled) {
            InitializeDebugMode();
        }
    }

    public void ChangeScene(string sceneName, string transitionId) {
        _sceneLoader.LoadSceneWithTransition(sceneName, transitionId);
    }

    public void StartNewGame([CanBeNull] string saveSlotId) {
        _currentSave = new SaveData {
            metadata = new SaveMetadata {
                saveId = saveSlotId ?? Guid.NewGuid().ToString()
            },
            content = _saveManager.GetInitSaveContent()
        };
        
        InitializePlayer();
        _sceneLoader.Init(_playerObject);
        _sceneLoader.LoadSceneWithSpawnPosition(newGameStartScene, newGameStartSpawnPosition);
        InitializeInGameUi();
    }
    
    public void SaveGame(string sceneName, string checkpointId) {
        if (!_currentSave.HasValue) {
            Debug.Log($"Can't save game - save slot is empty");
            return;
        }

        LastCheckpointId = checkpointId;

        var saveObject = _currentSave.Value;
        saveObject.content.sceneId = sceneName;
        saveObject.content.checkpointId = checkpointId;
        saveObject.metadata.zoneName = "River valley"; // TODO
        saveObject.metadata.date = DateTime.Now;
        _currentSave = saveObject;
        
        _saveManager.Save(_currentSave.Value);
        Debug.Log($"Game saved. Save id: {_currentSave.Value.metadata.saveId}");
    }
    
    public void LoadGame(string saveId) {
        _currentSave = _saveManager.Load(saveId);
        LastCheckpointId = _currentSave.Value.content.checkpointId;
        var sceneId = _currentSave.Value.content.sceneId;
        var checkpointId = _currentSave.Value.content.checkpointId;
        InitializePlayer();
        _sceneLoader.Init(_playerObject);
        _sceneLoader.LoadSceneWithCheckpoint(sceneId, checkpointId);
        InitializeInGameUi();
    }

    public void ExitGame() {
        Application.Quit();
    }

    public void GoToMainMenu() {
        LastCheckpointId = null;
        _sceneLoader.Clear();
        Destroy(_playerObject);
        Destroy(_inGameUiObject);
        _sceneLoader.LoadScene(_mainMenuSceneName);
    }
}