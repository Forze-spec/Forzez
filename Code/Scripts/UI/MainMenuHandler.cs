using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

enum MainMenuState {
    Root,
    CreatingNewGame,
    LoadingSave,
    Settings,
}

public class MainMenuHandler : MonoBehaviour {
    
    [BoxGroup("Root menu button refs")]
    public Button newGameButton;
    
    [BoxGroup("Root menu button refs")]
    public Button loadSaveButton;
    
    [BoxGroup("Root menu button refs")]
    public Button settingsButton;
    
    [BoxGroup("Submenu refs")]
    public GameObject rootPanel;
    
    [BoxGroup("Submenu refs")]
    public GameObject saveSlotsPanel;
    
    [BoxGroup("Submenu refs")]
    public GameObject settingsPanel;

    public int saveSlotsNumber = 5;
    public Transform saveSlotsContainer;
    public GameObject saveSlotPrefab;

    public GameObject gameStateObjectPrefab;
    
    // END INSPECTOR DECLARATION
    
    private StateMachine<MainMenuState> _stateMachine;
    private List<SaveSlot> _emptySaveSlots;

    private void InitStateMachine() {
        _stateMachine = new StateMachine<MainMenuState>(MainMenuState.Root);
        
        _stateMachine.AddTransition(MainMenuState.Root, MainMenuState.CreatingNewGame, () => {
            rootPanel.SetActive(false);
            saveSlotsPanel.SetActive(true);
            foreach (var emptySlot in _emptySaveSlots) {
                emptySlot.button.interactable = true;
            }
            EventSystem.current.SetSelectedGameObject(saveSlotsContainer.transform.GetChild(0).gameObject);
        });
        _stateMachine.AddTransition(MainMenuState.Root, MainMenuState.LoadingSave, () => {
            rootPanel.SetActive(false);
            saveSlotsPanel.SetActive(true);
            foreach (var emptySlot in _emptySaveSlots) {
                emptySlot.button.interactable = false;
            }

            if (_emptySaveSlots.Count != saveSlotsNumber) {
                EventSystem.current.SetSelectedGameObject(saveSlotsContainer.transform.GetChild(0).gameObject);
            }
        });
        _stateMachine.AddTransition(MainMenuState.Root, MainMenuState.Settings, () => {
            rootPanel.SetActive(false);
            settingsPanel.SetActive(true);
        });
        _stateMachine.AddTransition(MainMenuState.CreatingNewGame, MainMenuState.Root, () => {
            saveSlotsPanel.SetActive(false);
            rootPanel.SetActive(true);
            EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);
        });
        _stateMachine.AddTransition(MainMenuState.LoadingSave, MainMenuState.Root, () => {
            saveSlotsPanel.SetActive(false);
            rootPanel.SetActive(true);
            EventSystem.current.SetSelectedGameObject(loadSaveButton.gameObject);
        });
        _stateMachine.AddTransition(MainMenuState.Settings, MainMenuState.Root, () => {
            settingsPanel.SetActive(false);
            rootPanel.SetActive(true);
            EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
        });
    }

    private void InitSaveSlots() {
        var savesMeta = GameManager.Instance.SavesMeta
            .OrderByDescending(elem => elem.date).ToList();
        foreach (var elem in savesMeta) {
            var slotObject = Instantiate(saveSlotPrefab, saveSlotsContainer);
            var slotHandler = slotObject.GetComponent<SaveSlot>();
            slotHandler.id.text = elem.saveId;
            slotHandler.zone.text = elem.zoneName;
            slotHandler.date.text = elem.date.ToString("MM.dd.yyyy HH:mm");
            slotHandler.button.onClick.AddListener(() => SaveSlotClickHandler(elem.saveId));
        }

        var emptySlotsNumber = saveSlotsNumber - savesMeta.Count;
        _emptySaveSlots = new List<SaveSlot>();
        for (var i = 0; i < emptySlotsNumber; ++i) {
            var slotObject = Instantiate(saveSlotPrefab, saveSlotsContainer);
            var slotHandler = slotObject.GetComponent<SaveSlot>();
            slotHandler.button.onClick.AddListener(() => SaveSlotClickHandler(null));
            _emptySaveSlots.Add(slotHandler);
        }
    }

    private void Awake() {
        if (GameManager.Instance == null) {
            Instantiate(gameStateObjectPrefab);
        }
        EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);
        
        InitStateMachine();
        InitSaveSlots();

        GameManager.Instance.uiInputActions["Cancel"].performed += CancelPressHandler;
    }

    private void OnDestroy() {
        GameManager.Instance.uiInputActions["Cancel"].performed -= CancelPressHandler;
    }

    // button event handlers

    public void NewGameClickHandler() {
        _stateMachine.ChangeState(MainMenuState.CreatingNewGame);
    }

    public void LoadSaveClickHandler() {
        _stateMachine.ChangeState(MainMenuState.LoadingSave);
    }

    public void SettingsClickHandler() {
        _stateMachine.ChangeState(MainMenuState.Settings);
    }

    public void ExitClickHandler() {
        GameManager.Instance.ExitGame();
    }

    public void SaveSlotClickHandler([CanBeNull] string saveSlotId) {
        if (_stateMachine.CurrentState == MainMenuState.CreatingNewGame) {
            GameManager.Instance.StartNewGame(saveSlotId);
        } else if (_stateMachine.CurrentState == MainMenuState.LoadingSave) {
            if (saveSlotId is null) {
                throw new NullReferenceException("Can't load save with null id");
            }
            
            GameManager.Instance.LoadGame(saveSlotId);
        } else {
            throw new InvalidOperationException("Menu state error. Can't load save.");
        }
    }

    public void CancelPressHandler(InputAction.CallbackContext context) {
        if (new[] {
            MainMenuState.CreatingNewGame, MainMenuState.LoadingSave, MainMenuState.Settings
        }.Contains(_stateMachine.CurrentState)) {
            _stateMachine.ChangeState(MainMenuState.Root);
        }
    }
}
