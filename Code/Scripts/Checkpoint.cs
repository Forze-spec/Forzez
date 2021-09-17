using UnityEngine;
using UnityEngine.SceneManagement;

public class Checkpoint : MonoBehaviour {
    public string checkpointId;
    
    // END INSPECTOR DECLARATION

    // TODO replace color changing with animation
    private SpriteRenderer _spriteRenderer;
    private Color _defaultColor;
    private Color _activeColor = Color.cyan;

    private bool IsActive => GameManager.Instance.LastCheckpointId == checkpointId;

    private void HandleActivation(string oldValue, string newValue) {
        if (oldValue == checkpointId && newValue != checkpointId) {
            _spriteRenderer.color = _defaultColor;
        } else if (oldValue != checkpointId && newValue == checkpointId) {
            _spriteRenderer.color = _activeColor;
        }
    }

    private void Awake() {
        // TODO replace color changing with animation
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _defaultColor = _spriteRenderer.color;

        if (IsActive) {
            _spriteRenderer.color = _activeColor;
        }

        GameManager.Instance.LastCheckpointIdChanged += HandleActivation;
    }

    private void OnDestroy() {
        GameManager.Instance.LastCheckpointIdChanged -= HandleActivation;
    }

    private void OnTriggerEnter2D(Collider2D other) {
        // физические слои настроены так, что триггер срабатывает только на игрока (слой EnvironmentTriggers)
        if (!IsActive) {
            GameManager.Instance.SaveGame(SceneManager.GetActiveScene().name, checkpointId);
        }
    }
}
