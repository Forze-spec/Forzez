using UnityEngine;
using Cinemachine;

public class CinemachineAutoAttach : MonoBehaviour {
    private CinemachineVirtualCamera _virtualCamera;
    
    protected void Awake() {
        _virtualCamera = GetComponent<CinemachineVirtualCamera>();
    }

    protected void Start() {
        _virtualCamera.Follow = GameManager.Instance.player.Core.transform;
    }
}
