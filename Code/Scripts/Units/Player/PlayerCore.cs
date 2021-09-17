using System;
using NaughtyAttributes;
using UnityEngine;

public class PlayerCore : UnitCore {
    
    // INSPECTOR DECLARATION
    
    [BoxGroup("Energy properties")]
    [Tooltip("Energy regeneration (points per second)")]
    public float energyRegeneration;

    [BoxGroup("Energy properties")]
    [Tooltip("Number of seconds before energy starts to regenerate after expense")]
    public float energyRegenerationCooldown = 1f;
    
    [BoxGroup("Energy properties")]
    [Tooltip("Current energy of player")]
    [SerializeField]
    private float _currentEnergy = 10;  // текущяя энергия
    
    [BoxGroup("Energy properties")]
    [Tooltip("Maximum energy of player")]
    [SerializeField]
    private float _maximumEnergy = 10;  // максимальная энергия

    // INNER FIELDS

    private PlayerStateMachine _playerStateMachine;
    private float _energyRegenerationCooldownEndTime = 0;
    private float _invulnerabilityEndTime; // время конца неуязвимости после получения урона
    
    // EVENTS
    
    public event ValueChangedEvent<float> CurrentEnergyChanged;
    public event ValueChangedEvent<float> MaximumEnergyChanged;
    
    // ACCESSORS
    
    public float CurrentEnergy {
        get => _currentEnergy;
        set {
            SetCurrentEnergy(value);
            _energyRegenerationCooldownEndTime = Time.fixedTime + energyRegenerationCooldown;
        }
    }
    
    public float MaximumEnergy {
        get => _maximumEnergy;
        set {  
            var oldValue = _maximumEnergy;
            _maximumEnergy = Mathf.Max(value, 0);
            if (Math.Abs(oldValue - _maximumEnergy) > Config.FloatPrecision) {
                MaximumEnergyChanged?.Invoke(oldValue, _maximumEnergy);
            }

            CurrentEnergy = Mathf.Clamp(value, 0, _maximumEnergy);
        }
    }
    
    // INNER FUNCTIONS
    
    private void SetCurrentEnergy(float value) {
        var oldValue = _currentEnergy;
        _currentEnergy = Mathf.Clamp(value, 0, MaximumEnergy);
        CurrentEnergyChanged?.Invoke(oldValue, _currentEnergy);
    }

    protected override void FixedUpdate() {
        base.FixedUpdate();
        if (Time.fixedTime > _energyRegenerationCooldownEndTime) {
            SetCurrentEnergy(_currentEnergy + energyRegeneration * Time.fixedDeltaTime);
        }
    }
    
    // API

    public void Initialize() {
        _playerStateMachine = GameManager.Instance.player.StateMachine;

        KnockbackedChanged += value => {
            _playerStateMachine.controlEnabled = !value;
            // TODO run player knockback animation
        };
    }
}