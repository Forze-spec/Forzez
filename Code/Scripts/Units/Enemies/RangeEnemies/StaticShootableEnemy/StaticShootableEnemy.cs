using System;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

public enum StaticShootableEnemyMode {
    Fixed, Turnable, Aimable
}

enum StaticShootableEnemyState {
    Waiting, Aiming, Attacking
}

[RequireComponent(typeof(Rigidbody2D))]
public class StaticShootableEnemy : MonoBehaviour {
    
    [Tooltip("Rotatable part of enemy")]
    public Rigidbody2D barrelRb;
    
    [Tooltip("Type of enemy rotation")]
    public StaticShootableEnemyMode mode = StaticShootableEnemyMode.Fixed;
    
    private bool BarrelRotationEnabled => mode != StaticShootableEnemyMode.Fixed;
    [Tooltip("Speed of barrelRb rotation")]
    [ShowIf("BarrelRotationEnabled")]
    public float rotationSpeed = 10f;   // скорость вращения голо
    
    [BoxGroup("Attack properties")]
    [Tooltip("Damage to player on contact")]
    public float meleeDamage = 10f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Point where shot spawns")]
    public Transform shotStartPoint;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Shell prefab")]
    public GameObject shellObject;

    [BoxGroup("Attack properties")]
    [Tooltip("Pause between attacks")]
    public float attackPause;

    [BoxGroup("Attack properties")]
    [Tooltip("Predicate player movement when shoot (used only in turnable mode)")]
    public bool shootAhead;
    
    // END INSPECTOR DECLARATION

    private const float AttackAnglePrecision = 3f;
    
    private AutoStateMachine<StaticShootableEnemyState> _stateMachine;
    private Animator _animator;
    private EnemyDetectArea _detectArea;
    private EnemyDamageArea _damageArea;
    private PlayerCore _playerCore;
    private PlayerMovable _playerMovable;
    private float _attackPauseEndTime;
    private Vector2 _startBarrelDirection;
    
    private int Direction   //направление юнита (1 - право, -1 - лево)
    {
        get => Math.Sign(transform.localScale.x);
        set {
            var currentSignum = Math.Sign(transform.localScale.x);
            var targetSignum = Math.Sign(value);
            if (currentSignum * targetSignum >= 0) {    // Если произведение меньше нуля, то направление уже верное; 0 не меняет направления
                return;
            }
            
            transform.localScale = new Vector3(    //поворачиваем
                targetSignum * Mathf.Abs(transform.localScale.x), 
                transform.localScale.y, 
                transform.localScale.z
            );
        }
    }

    private Vector2 CurrentBarrelDirection => barrelRb.transform.right * Direction;

    private StaticShootableEnemyState State => _stateMachine.CurrentState;
    
    private bool IsPlayerVisible => _detectArea.isPlayerVisible;
    
    private Vector2 PlayerPosition => _detectArea.SupposedPlayerPosition;
    
    private bool CanAttack => _damageArea.canAttack;
    
    private bool IsBarrelPointed {
        get {
            if (!IsPlayerVisible) {
                return false;
            }

            if (mode == StaticShootableEnemyMode.Fixed) {
                return true;
            }
            
            Vector2 currentPosition = barrelRb.transform.position;
            
            Vector2 targetDirection;
            if (mode == StaticShootableEnemyMode.Turnable) {
                var signum = Math.Sign(PlayerPosition.x - currentPosition.x);
                targetDirection = new Vector2(Mathf.Abs(_startBarrelDirection.x) * signum, _startBarrelDirection.y);
            } else {
                targetDirection = (PlayerPosition - currentPosition).normalized;
            }

            var angle = Mathf.Abs(Vector2.SignedAngle(CurrentBarrelDirection, targetDirection));
            return angle <= AttackAnglePrecision;
        }
    }

    private void InitStateMachine() {
        _stateMachine = new AutoStateMachine<StaticShootableEnemyState>(StaticShootableEnemyState.Waiting);
        
        // from WAITING
        _stateMachine.AddTransition(StaticShootableEnemyState.Waiting, StaticShootableEnemyState.Aiming, () => {
            _animator.SetBool("IsWaiting", false);
            _animator.SetBool("IsAiming", true);
        });
        _stateMachine.AddTransition(StaticShootableEnemyState.Waiting, StaticShootableEnemyState.Attacking, () => {
            _animator.SetBool("IsWaiting", false);
            _animator.SetBool("IsAttacking", true);
        });
        
        // from AIMING
        _stateMachine.AddTransition(StaticShootableEnemyState.Aiming, StaticShootableEnemyState.Waiting, () => {
            _animator.SetBool("IsAiming", false);
            _animator.SetBool("IsWaiting", true);
        });
        _stateMachine.AddTransition(StaticShootableEnemyState.Aiming, StaticShootableEnemyState.Attacking, () => {
            _animator.SetBool("IsAiming", false);
            _animator.SetBool("IsAttacking", true);
        });
        
        // from ATTACKING
        _stateMachine.AddTransition(StaticShootableEnemyState.Attacking, StaticShootableEnemyState.Waiting, () => {
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsWaiting", true);
        });
        _stateMachine.AddTransition(StaticShootableEnemyState.Attacking, StaticShootableEnemyState.Aiming, () => {
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsAiming", true);
        });
    }

    private void InitStateChanging() {
        _stateMachine.AddAutoTransition(StaticShootableEnemyState.Waiting, currentTime => 
            State == StaticShootableEnemyState.Aiming && !IsPlayerVisible
        );
        _stateMachine.AddAutoTransition(StaticShootableEnemyState.Aiming, currentTime => 
            State == StaticShootableEnemyState.Waiting && IsPlayerVisible && (!IsBarrelPointed || currentTime < _attackPauseEndTime)
        );
        _stateMachine.AddAutoTransition(StaticShootableEnemyState.Attacking, currentTime => 
            new[] {StaticShootableEnemyState.Aiming, StaticShootableEnemyState.Waiting}.Contains(State) && IsBarrelPointed && currentTime >= _attackPauseEndTime
        );
    }

    private void InitStateHandlers() {
        _stateMachine.AddStateHandler(StaticShootableEnemyState.Waiting, currentTime => {
            if (mode != StaticShootableEnemyMode.Fixed) {
                RotateBarrel(_startBarrelDirection, Time.fixedDeltaTime);
            }
        });
        _stateMachine.AddStateHandler(StaticShootableEnemyState.Aiming, currentTime => {
            if (mode == StaticShootableEnemyMode.Fixed) {
                return;
            }

            Vector2 currentPosition = barrelRb.transform.position;
            
            Vector2 targetDirection;
            if (mode == StaticShootableEnemyMode.Turnable) {
                var signum = Math.Sign(PlayerPosition.x - currentPosition.x);
                targetDirection = new Vector2(Mathf.Abs(_startBarrelDirection.x) * signum, _startBarrelDirection.y);
            } else {
                targetDirection = (PlayerPosition - currentPosition).normalized;
            }

            RotateBarrel(targetDirection, Time.fixedDeltaTime);
        });
    }

    private void RotateBarrel(Vector2 targetDirection, float deltaTime) {
        var angle = Vector2.SignedAngle(CurrentBarrelDirection, targetDirection);
        var speedAngle = Mathf.Sign(angle) * rotationSpeed * deltaTime;
        var trueRotationAngle = Mathf.Abs(angle) < Mathf.Abs(speedAngle) ? angle : speedAngle;
        barrelRb.transform.Rotate(Vector3.forward, trueRotationAngle);

        if (Mathf.Abs(barrelRb.transform.rotation.eulerAngles.z) > 90) {
            Direction = Math.Sign(CurrentBarrelDirection.x);
        }
    }

    private void Awake() {
        if (!barrelRb) {
            throw new NullReferenceException("Head rigidbody is empty");
        }

        _animator = GetComponent<Animator>();
        _playerCore = GameManager.Instance.player.Core;
        _playerMovable = GameManager.Instance.player.Movable;

        _startBarrelDirection = CurrentBarrelDirection;

        _detectArea = GetComponentInChildren<EnemyDetectArea>();
        if (!_detectArea) {
            throw new Exception($"No detect area on enemy");
        }

        _damageArea = GetComponentInChildren<EnemyDamageArea>();
        if (!_damageArea) {
            throw new Exception($"No damage area on enemy");
        }
        
        InitStateMachine();
        InitStateHandlers();
        InitStateChanging();
        
        _animator.GetBehaviour<StaticShootableEnemyAnimatorBehaviour>().AttackEnded += () => {
            if (State != StaticShootableEnemyState.Attacking) {
                return;
            }
            
            _attackPauseEndTime = Time.fixedTime + attackPause;
            _stateMachine.ChangeState(IsPlayerVisible ? StaticShootableEnemyState.Aiming : StaticShootableEnemyState.Waiting);
        };
    }
    
    protected virtual void FixedUpdate() {
        _stateMachine.DoIteration(Time.fixedTime);
        
        if (CanAttack) {
            Vector2 direction = _playerCore.transform.position - transform.position;
            _playerCore.TakeDamage(meleeDamage, direction);
        }
    }
    
    // ANIMATION EVENTS
    
    public void Shoot() {
        GameObject shell = Instantiate(shellObject);
        Vector2 shellStartPosition = shotStartPoint.position;
        
        shell.transform.position = shellStartPosition;
        var handler = shell.GetComponent<Shell>();

        Vector2 shellTargetPosition;
        Vector2 aheadSpeed;
        if (mode == StaticShootableEnemyMode.Turnable) {
            shellTargetPosition = PlayerPosition;
            aheadSpeed = shootAhead && _playerMovable ? _playerMovable.Velocity : Vector2.zero;
        } else {
            shellTargetPosition = shellStartPosition + CurrentBarrelDirection;
            aheadSpeed = Vector2.zero;
        }
        
        handler.Throw(shellStartPosition, shellTargetPosition, aheadSpeed);
    }
}
