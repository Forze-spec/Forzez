using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

struct DestructiblePart {
    public Rigidbody2D Rigidbody;
    public Material Material;
}

public class Destructible : MonoBehaviour {

    [Tooltip("Life time before destruction")]
    public float timeToLive = 1.5f;

    [Tooltip("Fading time of parts")]
    public float fadingTime = 3f;

    [Tooltip("Particles start velocity magnitude")]
    public float particlesVelocity = 5f;

    [Tooltip("Parts base velocity magnitude")]
    public float baseVelocity = 15f;

    [BoxGroup("Parts spreading randomization")]
    [Tooltip("Min max parts direction deviation angle")]
    [MinMaxSlider(-10f, 10f)]
    public Vector2 directionAngleRandomBounds; //min max границы отклонения

    [BoxGroup("Parts spreading randomization")]
    [Tooltip("Min max parts rotation velocity")]
    [MinMaxSlider(-10f, 10f)]
    public Vector2 angularVelocityRandomBounds; //min max крутящий момент

    [BoxGroup("Parts spreading randomization")]
    [Tooltip("Min max parts additional velocity magnitude")]
    [MinMaxSlider(-10f, 10f)]
    public Vector2 velocityRandomBounds; //min max толчка
    
    // END INSPECTOR DECLARATION

    private static readonly int FadeProp = Shader.PropertyToID("_Fade");
    private ParticleSystem _particleSystem;
    private List<DestructiblePart> _parts = new List<DestructiblePart>();
    private float _clearTime;
    private float _currentFading = 1f;

    private void HandleClearing(float time, float deltaTime) {
        if (time > _clearTime) return;
        
        _currentFading = Mathf.Clamp01(_currentFading - deltaTime / fadingTime);
        if (_currentFading <= 0f) {
            Destroy(gameObject);
        }

        foreach (var part in _parts) {
            part.Material.SetFloat(FadeProp, _currentFading);
        }
    }
    
    private void Awake() {  
        _particleSystem = GetComponent<ParticleSystem>();

        foreach (Transform elem in transform) {
            DestructiblePart child = new DestructiblePart {
                Rigidbody = elem.GetComponent<Rigidbody2D>(), 
                Material = elem.GetComponent<SpriteRenderer>().material
            };
            _parts.Add(child);
        }
    }

    private void Start() {
        _clearTime = Time.time + timeToLive;
    }

    private void Update() {
        HandleClearing(Time.time, Time.deltaTime);
    }

    public void RunDestruction(Vector2? damageDirection) {
        var hitDirection = damageDirection?.normalized ?? new Vector2(0, 0);
        foreach (var part in _parts) {
            var rb = part.Rigidbody;

            // вектор от центра объекта к центру частицы
            Vector2 toPartDirection = (rb.transform.position - transform.position).normalized;
            var direction = toPartDirection + hitDirection;
            
            var randomAngle = Random.Range(directionAngleRandomBounds.x, directionAngleRandomBounds.y);
            Vector2 randomizedDirection = (Quaternion.Euler(0f, 0f, randomAngle) * direction).normalized;
            
            var randomVelocity = Random.Range(velocityRandomBounds.x, velocityRandomBounds.y);
            rb.velocity = randomizedDirection * (baseVelocity + randomVelocity);
            
            var randomAngularVelocity = Random.Range(angularVelocityRandomBounds.x, angularVelocityRandomBounds.y);
            rb.angularVelocity = randomAngularVelocity;
        }
        
        if (_particleSystem != null) {
            var velocityModule = _particleSystem.velocityOverLifetime;
            var velocity = hitDirection.normalized * particlesVelocity;
            velocityModule.x = velocity.x;
            velocityModule.y = velocity.y;
        }
    }
}