using UnityEngine;

public class EnemyDetectArea : MonoBehaviour {
    
    [Tooltip("Use or not raycast to check obstacles when detecting player")]
    public bool needRaycastToDetect;
    
    // END INSPECTOR DECLARATION
    
    private bool _isPlayerInFov;
    private Vector2 _lastVisiblePlayerPosition;
    
    private PlayerCore _playerCore;
    private bool IsPlayerAvailable => _playerCore && !_playerCore.IsDead;
    private Vector2 ExactPlayerPosition => _playerCore.transform.position;

    // В общем случае показывает, если игрок в зоне видимости
    // Если включена needRaycastToDetect, то в добавок проверяет препятствия через raycast
    public bool isPlayerVisible;
    
    // Предположительная позиция игрока. Если игрок видим, то возвращается его точная позиция
    // В противном случае - позиция, где мы последний раз видели игрока
    public Vector2 SupposedPlayerPosition =>
        isPlayerVisible ? (Vector2)_playerCore.transform.position : _lastVisiblePlayerPosition;

    private void Awake() {
        _playerCore = GameManager.Instance.player.Core;
        
        InvokeRepeating(nameof(UpdatePlayerVisibility), 0, Config.DetectAreaRaycastRepeatRate);
    }

    private void OnTriggerEnter2D(Collider2D other) {
        // физические слои настроены так, что триггер срабатывает только на игрока (слой UnitTriggers)
        _isPlayerInFov = true;
    }
    
    private void OnTriggerExit2D(Collider2D other) {
        // физические слои настроены так, что триггер срабатывает только на игрока (слой UnitTriggers)
        _isPlayerInFov = false;
    }
    
    private void UpdatePlayerVisibility() {
        if (!IsPlayerAvailable || !_isPlayerInFov) {
            isPlayerVisible = false;
            return;
        }

        if (needRaycastToDetect) {
            RaycastHit2D hit =  Physics2D.Linecast(
                transform.parent.position, ExactPlayerPosition, Config.ObstacleLayerMask
            );

            isPlayerVisible = !hit;
            if (isPlayerVisible) {
                _lastVisiblePlayerPosition = ExactPlayerPosition;
            }
        } else {
            isPlayerVisible = true;
            _lastVisiblePlayerPosition = ExactPlayerPosition;
        }
    }
}