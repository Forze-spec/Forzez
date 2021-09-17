using NaughtyAttributes;
using UnityEngine;

public class SceneTransition : MonoBehaviour {
    public string transitionId;
    public Transform spawnPoint;
    
    [Scene]
    public string sceneToTransit; // scene name
    
    // END INSPECTOR DECLARATION

    private void OnTriggerEnter2D(Collider2D other) {
        // физические слои настроены так, что триггер срабатывает только на игрока (слой EnvironmentTriggers)
        GameManager.Instance.ChangeScene(sceneToTransit, transitionId);
    }
}
