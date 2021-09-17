using System;
using System.Collections;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;
using Random = UnityEngine.Random;

enum AirProvokedEnemyState {
    Waiting, Patrolling, Attacking, Returning
}

[RequireComponent(typeof(UnitCore))]
public class AirProvokedEnemy : AirMovable {
    
    [Tooltip("Time for the unit to calm down and stop chasing if he can't see (or access) player")]
    public float calmDownTime = 3f;

    [Tooltip("Damage to player on contact")]
    public float damage = 10f;
    
    [BoxGroup("Knockback properties")]
    [Tooltip("Knockback (and move disabling) duration after taking damage by unit (in seconds)")]
    public float knockbackDuration = .2f;
    
    [BoxGroup("Knockback properties")]
    [Tooltip("Knockback force (applies once after taking damage)")]
    public float knockbackForce = 15f;
    
    [BoxGroup("Patrol properties")]
    [Tooltip("Points defining the path of the patrol")]
    public Vector2[] patrollingPoints = {};
    
    [BoxGroup("Patrol properties")]
    [Tooltip("Time pause after achieving patrol point and before moving to the next")]
    public float patrollingPause = 3f;
    
    [BoxGroup("Patrol properties")]
    [Tooltip("Time pause between moves while in waiting state")]
    public float waitingMovePause = 0.5f;

    [BoxGroup("Patrol properties")]
    [Tooltip("Radius of movement while in waiting state")]
    public float waitingMoveRadius = 0.5f;

    // END INSPECTOR DECLARATION
    
    private Animator _animator;
    private AutoStateMachine<AirProvokedEnemyState> _stateMachine;
    private PlayerCore _playerCore;
    private EnemyDetectArea _detectArea;
    private EnemyDamageArea _damageArea;
    private float _stopChasingTime;
    private float _patrollingPauseEndTime;
    private bool _moveEnabled = true;
    private Vector2 _waitingTargetPosition;
    private float _waitingMovePauseEnd;

    private int _currentPatrollingPointIndex = 0;
    private int CurrentPatrollingPointIndex {
        get => _currentPatrollingPointIndex;
        set => _currentPatrollingPointIndex = value % patrollingPoints.Length;
    }
    
    private AirProvokedEnemyState State => _stateMachine.CurrentState;
    
    private bool IsPlayerVisible => _detectArea.isPlayerVisible;
    
    private Vector2 PlayerPosition => _detectArea.SupposedPlayerPosition;
    
    private bool CanAttack => _damageArea.canAttack;

    private void InitStateMachine() {
        _stateMachine = new AutoStateMachine<AirProvokedEnemyState>(
            patrollingPoints.Length > 0 ? AirProvokedEnemyState.Patrolling : AirProvokedEnemyState.Waiting
        );
        
        // from WAITING
        _stateMachine.AddTransition(AirProvokedEnemyState.Waiting, AirProvokedEnemyState.Patrolling, () => {
            CurrentPatrollingPointIndex++;
            
            _animator.SetBool("IsWaiting", false);
            _animator.SetBool("IsFlying", true);
        });
        _stateMachine.AddTransition(AirProvokedEnemyState.Waiting, AirProvokedEnemyState.Attacking, () => {
            _animator.SetBool("IsWaiting", false);
            _animator.SetBool("IsAttacking", true);
        });
        
        // from PATROLLING
        _stateMachine.AddTransition(AirProvokedEnemyState.Patrolling, AirProvokedEnemyState.Waiting, () => {
            _patrollingPauseEndTime = Time.fixedTime + patrollingPause;
            StopMovement();
            _waitingMovePauseEnd = Time.fixedTime + waitingMovePause;
            _waitingTargetPosition = (Vector2)transform.position + new Vector2(
                Random.Range(-waitingMoveRadius, waitingMoveRadius),
                Random.Range(-waitingMoveRadius, waitingMoveRadius)
            );
            
            _animator.SetBool("IsFlying", false);
            _animator.SetBool("IsWaiting", true);
        });
        _stateMachine.AddTransition(AirProvokedEnemyState.Patrolling, AirProvokedEnemyState.Attacking, () => {
            _animator.SetBool("IsFlying", false);
            _animator.SetBool("IsAttacking", true);
        });
        
        // from ATTACKING
        _stateMachine.AddTransition(AirProvokedEnemyState.Attacking, AirProvokedEnemyState.Returning, () => {
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsFlying", true);
        });
        
        
        // from RETURNING
        _stateMachine.AddTransition(AirProvokedEnemyState.Returning, AirProvokedEnemyState.Waiting, () => {
            _patrollingPauseEndTime = Time.fixedTime + patrollingPause;
            CurrentPatrollingPointIndex++;
            StopMovement();
            _waitingMovePauseEnd = Time.fixedTime + waitingMovePause;
            _waitingTargetPosition = (Vector2)transform.position + new Vector2(
                Random.Range(-waitingMoveRadius, waitingMoveRadius),
                Random.Range(-waitingMoveRadius, waitingMoveRadius)
            );
            
            _animator.SetBool("IsFlying", false);
            _animator.SetBool("IsWaiting", true);
        });
        _stateMachine.AddTransition(AirProvokedEnemyState.Returning, AirProvokedEnemyState.Attacking, () => {
            _animator.SetBool("IsFlying", false);
            _animator.SetBool("IsAttacking", true);
        });
    }

    private void InitStateChanging() {
        _stateMachine.AddAutoTransition(AirProvokedEnemyState.Waiting, currentTime => {
            if (!new[] {AirProvokedEnemyState.Patrolling, AirProvokedEnemyState.Returning}.Contains(State)) {
                return false;
            }

            var targetX = patrollingPoints[_currentPatrollingPointIndex].x;
            var currentX = transform.position.x;
            var distance = targetX - currentX;
            return Math.Abs(distance) < Config.DistancePrecision;
        });
        _stateMachine.AddAutoTransition(AirProvokedEnemyState.Patrolling, currentTime => 
            State == AirProvokedEnemyState.Waiting &&
            patrollingPoints.Length > 1 &&
            currentTime > _patrollingPauseEndTime
        );
        _stateMachine.AddAutoTransition(AirProvokedEnemyState.Attacking, currentTime => 
            new[] {
                AirProvokedEnemyState.Waiting, AirProvokedEnemyState.Patrolling, AirProvokedEnemyState.Returning
            }.Contains(State) && IsPlayerVisible
        );
        _stateMachine.AddAutoTransition(AirProvokedEnemyState.Returning, currentTime => 
            State == AirProvokedEnemyState.Attacking && 
            (PlayerPosition - (Vector2)transform.position).magnitude < Config.DistancePrecision && 
            !IsPlayerVisible
        );
    }

    private Vector2 GetWaitingPoint() {
        while (true) {
            var point = patrollingPoints[_currentPatrollingPointIndex] + new Vector2(
                Random.Range(-waitingMoveRadius, waitingMoveRadius),
                Random.Range(-waitingMoveRadius, waitingMoveRadius)
            );
            
            var distance = Vector2.Distance(point, transform.position);
            if (distance < waitingMoveRadius) {
                continue;
            }
            
            RaycastHit2D hit =  Physics2D.Linecast(
                transform.position, point, Config.ObstacleLayerMask
            );
            if (!hit) {
                return point;
            }
        }
    }

    private void InitStateHandlers() {
        _stateMachine.AddStateHandler(AirProvokedEnemyState.Waiting, currentTime => {
            if (!_moveEnabled || currentTime < _waitingMovePauseEnd) {
                return;
            }
            
            var direction = _waitingTargetPosition - (Vector2)transform.position;
            if (direction.magnitude < Config.DistancePrecision) {
                StopMovement();
                _waitingMovePauseEnd = currentTime + waitingMovePause;
                _waitingTargetPosition = GetWaitingPoint();
            } else {
                Move(direction, 0.5f);
            }
        });
        _stateMachine.AddStateHandler(AirProvokedEnemyState.Patrolling, currentTime => {
            if (!_moveEnabled) {
                return;
            }

            var direction = patrollingPoints[_currentPatrollingPointIndex] - (Vector2)transform.position;
            Move(direction, 0.5f);
        });
        _stateMachine.AddStateHandler(AirProvokedEnemyState.Attacking, currentTime => {
            if (_moveEnabled) {
                Vector2 direction = PlayerPosition - (Vector2)transform.position;
                if (direction.magnitude > Config.DistancePrecision) {
                    Move(direction);
                } else {
                    // TODO do it once (can break knockback)
                    StopMovement();
                }
            }
        });
        _stateMachine.AddStateHandler(AirProvokedEnemyState.Returning, currentTime => {
            if (!_moveEnabled) {
                return;
            }
            var direction = patrollingPoints[_currentPatrollingPointIndex] - (Vector2)transform.position;
            Move(direction, 0.5f);
        });
    }

    protected override void Awake() {
        base.Awake();
        
        _animator = GetComponent<Animator>();
        _playerCore = GameManager.Instance.player.Core;
        
        _detectArea = GetComponentInChildren<EnemyDetectArea>();
        if (!_detectArea) {
            throw new Exception($"No detect area on enemy");
        }

        _damageArea = GetComponentInChildren<EnemyDamageArea>();
        if (!_damageArea) {
            throw new Exception($"No damage area on enemy");
        }
        
        if (patrollingPoints.Length == 0) {
            patrollingPoints = new Vector2[] {transform.position};
        }
        
        InitStateMachine();
        InitStateHandlers();
        InitStateChanging();

        var core = GetComponent<UnitCore>();
        core.KnockbackedChanged += value => _moveEnabled = !value;
    }

    private void FixedUpdate() {
        _stateMachine.DoIteration(Time.fixedTime);
        
        if (CanAttack) {
            Vector2 direction = _playerCore.transform.position - transform.position;
            _playerCore.TakeDamage(damage, direction);
        }
    }
}
