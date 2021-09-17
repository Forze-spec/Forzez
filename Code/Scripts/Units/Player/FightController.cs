using System;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.InputSystem;

public class FightController : MonoBehaviour {
    
    [Tooltip("Horizontal dodging speed of player")]
    public float dodgeSpeed = 20f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Melee attack damage of player")]
    public float meleeAttackDamage;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Pause between melee attacks")]
    public float pauseBetweenAttacks = 0.2f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Duration of combo time frame (starts after attack pause)")]
    public float comboFrameDuration = 0.2f;

    // END INSPECTOR DECLARATION

    private PlayerStateMachine _playerStateMachine;
    private PlayerCore _playerCore;
    private PlayerMovable _playerMovable;
    private Animator _animator;
    
    // TODO enable blinking while invulnerable
    private SpriteRenderer _spriteRenderer;
    private Color _defaultColor;
    
    private bool _isDodgeMoving;
    private int _dodgeDirection;
    private float _attackPauseEnd;
    private float _comboFrameEnd;
    private int _lastComboNumber;
    
    private int _currentMoveInput;
    private int _attackAlignment;
    private bool _isForwardAttacking;

    private int _attackNumber;
    private int AttackNumber {
        get => _attackNumber;
        set {
            _attackNumber = value > 2 ? 1 : Mathf.Clamp(value, 0, 2);
            _animator.SetInteger("AttackNumber", _attackNumber);
        }
    }
    
    private bool ControlEnabled => _playerStateMachine.controlEnabled;

    private void InitStateTransitions(PlayerStateMachine playerStateMachine) {
        playerStateMachine.AddTransition(PlayerState.Standing, PlayerState.Attacking, () => {
            _animator.SetBool("IsStanding", false);
        });
        playerStateMachine.AddTransition(PlayerState.Standing, PlayerState.Dodging, () => {
            _dodgeDirection = _playerMovable.Direction;
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsDodging", true);
        });
        
        playerStateMachine.AddTransition(PlayerState.Running, PlayerState.Attacking, () => {
            _animator.SetBool("IsRunning", false);
        });
        playerStateMachine.AddTransition(PlayerState.Running, PlayerState.Dodging, () => {
            _dodgeDirection = _playerMovable.Direction;
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsDodging", true);
        });
        
        playerStateMachine.AddTransition(PlayerState.Jumping, PlayerState.Attacking, () => {
            _animator.SetBool("IsJumping", false);
            _animator.SetBool("FlyingAttack", true);
        });
        playerStateMachine.AddTransition(PlayerState.Falling, PlayerState.Attacking, () => {
            _animator.SetBool("IsFalling", false);
            _animator.SetBool("FlyingAttack", true);
        });
        
        playerStateMachine.AddTransition(PlayerState.Attacking, PlayerState.Standing, () => {
            _animator.SetBool("FlyingAttack", false);
            _animator.SetBool("IsStanding", true);
        });
        playerStateMachine.AddTransition(PlayerState.Attacking, PlayerState.Running, () => {
            _animator.SetBool("FlyingAttack", false);
            _animator.SetBool("IsRunning", true);
        });
        playerStateMachine.AddTransition(PlayerState.Attacking, PlayerState.Jumping, () => {
            _animator.SetBool("FlyingAttack", false);
            _animator.SetBool("IsJumping", true);
        });
        playerStateMachine.AddTransition(PlayerState.Attacking, PlayerState.Falling, () => {
            _animator.SetBool("FlyingAttack", false);
            _animator.SetBool("IsFalling", true);
        });
        
        playerStateMachine.AddTransition(PlayerState.Dodging, PlayerState.Standing, () => {
            _isDodgeMoving = false;
            _dodgeDirection = 0;
            _animator.SetBool("IsDodging", false);
            _animator.SetBool("IsStanding", true);
        });
        playerStateMachine.AddTransition(PlayerState.Dodging, PlayerState.Running, () => {
            _isDodgeMoving = false;
            _dodgeDirection = 0;
            _animator.SetBool("IsDodging", false);
            _animator.SetBool("IsRunning", true);
        });
        playerStateMachine.AddTransition(PlayerState.Dodging, PlayerState.Falling, () => {
            _isDodgeMoving = false;
            _dodgeDirection = 0;
            _animator.SetBool("IsDodging", false);
            _animator.SetBool("IsFalling", true);
        });
    }

    private void HandleDodgeActivation(InputAction.CallbackContext context) {
        if (!ControlEnabled) {
            return;
        }

        if (!new[] {PlayerState.Standing, PlayerState.Running}.Contains(_playerStateMachine.State)) {
            return;
        }
        _playerStateMachine.ChangeState(PlayerState.Dodging);
    }
    private void HandleMeleeAttackActivation(InputAction.CallbackContext context) {
        if (!ControlEnabled || Time.fixedTime < _attackPauseEnd) {
            return;
        }
        
        if (!new[] {
            PlayerState.Standing, PlayerState.Running, PlayerState.Jumping, PlayerState.Falling
        }.Contains(_playerStateMachine.State)) {
            return;
        }

        _playerStateMachine.ChangeState(PlayerState.Attacking);
    }

    private void FixedUpdate() {
        if (_playerStateMachine.State == PlayerState.Dodging && _isDodgeMoving) {
            _playerMovable.SimpleHorizontalMove(_dodgeDirection, dodgeSpeed);
        } else if (_playerStateMachine.State == PlayerState.Attacking) {
            _playerMovable.HorizontalMove(_currentMoveInput);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other) {
        var targetObject = other.gameObject;
        var targetCore = targetObject.GetComponent<UnitCore>();
        if (targetCore != null) {
            targetCore.TakeDamage(meleeAttackDamage, targetObject.transform.position - transform.position);
        }
    }
    
    public void Initialize() {
        _animator = GameManager.Instance.player.Animator;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>(); // TODO enable blinking while invulnerable

        _playerCore = GameManager.Instance.player.Core;
        
        _playerMovable = GameManager.Instance.player.Movable;
        
        _playerStateMachine = GameManager.Instance.player.StateMachine;
        InitStateTransitions(_playerStateMachine);
        
        _defaultColor = _spriteRenderer.color;

        GameManager.Instance.playerInputActions["Move"].performed += context => {
            var value = context.ReadValue<Vector2>();
            if (Math.Abs(value.magnitude) < Config.FloatPrecision) {
                _currentMoveInput = 0;
                _attackAlignment = 0;
                return;
            }

            var absAngle = Mathf.Abs(Vector2.SignedAngle(Vector2.up, value));
            if (absAngle > Config.MoveAxisAngleOffset && absAngle < 180 - Config.MoveAxisAngleOffset) {
                _currentMoveInput = Math.Sign(value.x);
                _attackAlignment = 0;
            } else {
                _currentMoveInput = 0;
                _attackAlignment = absAngle < 90 ? 1 : -1;
            }
        };
        GameManager.Instance.playerInputActions["Dodge"].started += HandleDodgeActivation;
        GameManager.Instance.playerInputActions["Attack"].started += HandleMeleeAttackActivation;

        _playerStateMachine.StateChanged += (oldState, newState) => {
            if (oldState == PlayerState.Attacking) {
                _isForwardAttacking = false;
            }

            if (newState == PlayerState.Attacking) {
                _playerMovable.Direction = _currentMoveInput;
                _isForwardAttacking = _attackAlignment == 0;
                _animator.SetInteger("AttackAlignment", _attackAlignment);
                if (_isForwardAttacking) {
                    AttackNumber = Time.fixedTime < _comboFrameEnd ? _lastComboNumber + 1 : 1;
                    _lastComboNumber = AttackNumber;
                } else {
                    AttackNumber = 1;
                }
            }
        };

        _playerCore.InvulnerabilityChanged += value => {
            if (value) {
                _spriteRenderer.color = Color.red;  // TODO enable blinking while invulnerable
            } else {
                _spriteRenderer.color = _defaultColor;  // TODO disable blinking while invulnerable
            }
        };
    }

    // ANIMATION EVENTS

    public void AttackEnds() {
        _attackPauseEnd = Time.fixedTime + pauseBetweenAttacks;
        if (_isForwardAttacking) {
            _comboFrameEnd = _attackPauseEnd + comboFrameDuration;
        }

        AttackNumber = 0;
        _playerStateMachine.ResetState();
    }

    public void DodgeWindowStarts() {
        _isDodgeMoving = true;
    }

    public void DodgeWindowEnds() {
        _playerStateMachine.ResetState();
    }
}
