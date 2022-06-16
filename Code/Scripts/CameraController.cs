using UnityEngine;

public class CameraController : MonoBehaviour {

    private Vector2 _velocity;    //сохраняемая скорость, нужда для функции SmoothDamp
    private GameObject _playerGameObject;    //ссылка на объект игрока

    public float smoothTimeX;    //время сглаживания по X; чем оно больше, тем медленнее и плавнее камера реагирует на движение
    public float smoothTimeY;    //время сглаживания по Y

    public bool bounded;    //прикреплена ли камера к игроку

    public Vector2 minCameraPos;    //минимальная позиция камеры, ниже она не будет двигаться
    public Vector2 maxCameraPos;    //максимальная позиция камеры, выше она не будет двигаться
    
    void Awake () {
        _playerGameObject = GameManager.Instance.player.Core.gameObject;    //получаем ссылку на игрока
    }

    void Update() {
        if (!_playerGameObject) return;

        var cameraPosition = transform.position;
        var playerPosition = _playerGameObject.transform.position;
        
        float posX = Mathf.SmoothDamp(cameraPosition.x, playerPosition.x, ref _velocity.x, smoothTimeX);    //получаем сглаженную позицию по X
        float posY = Mathf.SmoothDamp(cameraPosition.y, playerPosition.y, ref _velocity.y, smoothTimeY);    //получаем сглаженную позицию по Y

        if (bounded) {
            posX = Mathf.Clamp(posX, minCameraPos.x, maxCameraPos.x);    //проверяем границы значений и устанавливаем новую позицию позицию
            posY = Mathf.Clamp(posY, minCameraPos.y, maxCameraPos.y);
        }

        transform.position = new Vector3(posX, posY, cameraPosition.z);
    }
}