using UnityEngine;

public class Interactive : MonoBehaviour {
    private PlayerStateMachine _playerStateMachine;

    private void Awake() {
        _playerStateMachine = GameManager.Instance.player.StateMachine;
    }

    private void OnTriggerEnter2D(Collider2D colliderEnter) {
        // физические слои настроены так, что триггер срабатывает только на игрока (слой EnvironmentTriggers)
        // TODO add interactable object indicator on UI
        _playerStateMachine.ActionEvent += HandleAction; // Добавляем обработчик для события ActionEvent
    }

    private void OnTriggerExit2D(Collider2D colliderExit) {
        // физические слои настроены так, что триггер срабатывает только на игрока (слой EnvironmentTriggers)
        _playerStateMachine.ActionEvent -= HandleAction; // Удаляем обработчик для события ActionEvent
    }

    private void HandleAction() {
        Debug.Log($"Object activated: {gameObject.GetInstanceID()}");
    }
}