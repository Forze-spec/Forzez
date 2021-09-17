using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PauseMenuHandler : MonoBehaviour {

    public GameObject pauseMenuPanel;
    public GameObject returnButton;
    
    // END INSPECTOR DECLARATION

    private bool IsOpened => pauseMenuPanel.activeSelf;
    
    private void OpenPauseMenu() {
        Time.timeScale = 0;
        // TODO UI SetActive(false)
        pauseMenuPanel.SetActive(true);
        EventSystem.current.SetSelectedGameObject(returnButton);
    }

    private void ClosePauseMenu() {
        EventSystem.current.SetSelectedGameObject(null);
        pauseMenuPanel.SetActive(false);
        // TODO UI SetActive(true)
        Time.timeScale = 1;
    }

    private void Awake() {
        GameManager.Instance.playerInputActions["Menu"].performed += StartPressHandler;
    }

    private void OnDestroy() {
        GameManager.Instance.playerInputActions["Menu"].performed -= StartPressHandler;
    }

    // button event handlers

    public void ReturnClickHandler() {
        ClosePauseMenu();
    }
    
    public void ExitClickHandler() {
        ClosePauseMenu();
        GameManager.Instance.GoToMainMenu();
    }

    public void StartPressHandler(InputAction.CallbackContext context) {
        if (IsOpened) {
            ClosePauseMenu();
        } else {
            OpenPauseMenu();
        }
    }
}
