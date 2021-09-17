using System;
using System.Collections;
using JetBrains.Annotations;
using NaughtyAttributes;
using UnityEngine;

public class UnitCore : MonoBehaviour {
    
    // INSPECTOR DECLARATION
    
    [BoxGroup("Health properties")]
    [Tooltip("Current health of unit")]
    [SerializeField]
    private float _currentHealth = 10;
    
    [BoxGroup("Health properties")]
    [Tooltip("Maximum health of unit")]
    [SerializeField]
    private float _maximumHealth = 10;
    
    [BoxGroup("Health properties")]
    [Tooltip("Health regeneration (points per second)")]
    public float healthRegeneration;
    
    [BoxGroup("Health properties")]
    [Tooltip("Number of seconds before health starts to regenerate after taking damage")]
    public float healthRegenerationCooldown = 1f;
    
    [BoxGroup("Invulnerability properties")]
    [Tooltip("Can unit take damage or not")]
    [SerializeField]
    private bool _canTakeDamage = true;

    [BoxGroup("Invulnerability properties")]
    [Tooltip("Invulnerability duration after taking damage (in seconds)")]
    public float invulnerabilityDuration = .5f;
    
    [BoxGroup("Knockback properties")]
    [Tooltip("Is unit knockbaked or not")]
    private bool _isKnockbacked;

    [BoxGroup("Knockback properties")]
    [Tooltip("Knockback duration after taking damage (in seconds)")]
    public float knockbackDuration = .2f;
    
    [Tooltip("Is dead unit or not")]
    [SerializeField]
    private bool _isDead;
    
    [Tooltip("Reference to unit's Rigidbody2d")]
    public Rigidbody2D unitRigidbodyRef;
    
    [Tooltip("Destructible prefab (if not null then unit object will be replaced with this prefab on unit death)")]
    public GameObject destructiblePrefab;
    
    // INNER FIELDS
    
    private Vector2? _lastDamageDirection;
    private float _healthRegenerationCooldownEndTime;

    [CanBeNull] public Func<bool> KnockbackCondition;
    
    // EVENTS
    
    public event ValueChangedEvent<float> CurrentHealthChanged;
    public event ValueChangedEvent<float> MaximumHealthChanged;
    public event NotifyEvent Died;
    public event ValueUpdatedEvent<bool> InvulnerabilityChanged;
    public event ValueUpdatedEvent<bool> KnockbackedChanged;
    
    // ACCESSORS

    public bool IsDead => _isDead;
    public bool CanTakeDamage {
        get => _canTakeDamage;
        private set {
            _canTakeDamage = value; 
            InvulnerabilityChanged?.Invoke(!_canTakeDamage);
        }
    }
    public bool IsKnockbacked {
        get => _isKnockbacked;
        private set {
            _isKnockbacked = value;
            KnockbackedChanged?.Invoke(_isKnockbacked);
        }
    }
    public float CurrentHealth {
        get => _currentHealth;
        set {
            SetCurrentHealth(value);
            _healthRegenerationCooldownEndTime = Time.fixedTime + healthRegenerationCooldown;
        }
    }
    public float MaximumHealth {
        get => _maximumHealth;
        set {
            var oldValue = _maximumHealth;
            _maximumHealth = Mathf.Max(value, 0);
            if (Math.Abs(oldValue - _maximumHealth) > Config.FloatPrecision) {
                MaximumHealthChanged?.Invoke(oldValue, _maximumHealth);
            }

            CurrentHealth = Mathf.Clamp(value, 0, _maximumHealth);
        }
    }
    
    // INNER FUNCTIONS
    
    private void Die() {    // убивает юнита и вызывает уничтожение
        _isDead = true;

        if (destructiblePrefab) {
            GameObject destructible = Instantiate(destructiblePrefab);  // создание объекта, хранящего частицы разрушенного объекта
            destructible.transform.position = transform.position;
            destructible.GetComponent<Destructible>().RunDestruction(_lastDamageDirection);
            Destroy(gameObject);
        }
        
        Died?.Invoke();
    }
    
    private IEnumerator HandleInvulnerability() {
        if (invulnerabilityDuration != 0) {
            var invulnerabilityEndTime = Time.fixedTime + invulnerabilityDuration;
            CanTakeDamage = false;
            yield return new WaitUntil(() => Time.fixedTime > invulnerabilityEndTime);
            CanTakeDamage = true;
        }
    }
    
    private IEnumerator DoKnockback(Vector2 direction, float knockbackForce) {
        var canDoKnockback = KnockbackCondition?.Invoke() ?? true;
        if (canDoKnockback && !IsKnockbacked) {
            var knockbackEndTime = Time.fixedTime + knockbackDuration;
            IsKnockbacked = true;
            
            unitRigidbodyRef.velocity = Vector2.zero;
            unitRigidbodyRef.AddForce(direction.normalized * knockbackForce, ForceMode2D.Impulse);
            
            yield return new WaitUntil(() => Time.fixedTime > knockbackEndTime);
            IsKnockbacked = false;
        }
    }

    private void SetCurrentHealth(float value) {
        var oldValue = _currentHealth;
        _currentHealth = Mathf.Clamp(value, 0, MaximumHealth);
        CurrentHealthChanged?.Invoke(oldValue, _currentHealth);
        
        if (!_isDead && _currentHealth <= 0) {
            Die();
        }
    }

    protected virtual void FixedUpdate() {
        if (Time.fixedTime > _healthRegenerationCooldownEndTime) {
            SetCurrentHealth(_currentHealth + healthRegeneration * Time.fixedDeltaTime);
        }
    }
    
    // API

    public void TakeDamage(float damage, Vector2? damageDirection = null, float knockbackForce = 0) {
        if (!CanTakeDamage) {
            return;
        }

        _lastDamageDirection = damageDirection;
        CurrentHealth -= damage;
        StartCoroutine(HandleInvulnerability());
        StartCoroutine(DoKnockback(damageDirection ?? Vector2.zero, knockbackForce));
    }
}
