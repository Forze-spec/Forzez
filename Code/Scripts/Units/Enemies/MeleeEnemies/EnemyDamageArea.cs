using UnityEngine;

public class EnemyDamageArea : MonoBehaviour {
    public bool canAttack;

    private void OnTriggerEnter2D(Collider2D other) {
        Debug.Log(other.gameObject.tag);
        Debug.Log(LayerMask.LayerToName(gameObject.layer));
        Debug.Log(LayerMask.LayerToName(other.gameObject.layer));
        Debug.Log(other.gameObject.name);
        // физические слои настроены так, что триггер срабатывает только на игрока (слой UnitTriggers)
        canAttack = true;
    }
    
    private void OnTriggerExit2D(Collider2D other) {
        // физические слои настроены так, что триггер срабатывает только на игрока (слой UnitTriggers)
        canAttack = false;
    }
}
