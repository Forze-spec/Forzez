using System;
using System.Collections;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

enum RammingEnemyState {
    Standing, Patrolling, Chasing, Resting, Stunning, Attacking, Returning
}

[RequireComponent(typeof(Animator))]
public class RammingEnemy : EarthMovable {
    
    [Tooltip("Can or not be knockbacked while attacking")]
    public bool knockbackWhileAttacking;

    [Tooltip("Should or not be stunned after obstacles hit while attacking")]
    public bool stunOnObstaclesHit;
    
    [Tooltip("Rest duration after attacking")]
    public float restDuration = 3f;

    [ShowIf("stunOnObstaclesHit")]
    [Tooltip("Stun duration after hitting the obstacle")]
    public float stunDuration = 3f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Maximal distance to player to ram")]
    public float attackDistance = 10f;

    [BoxGroup("Attack properties")]
    [Tooltip("Speed of ramming movement")]
    public float rammingSpeed = 20f;

    [BoxGroup("Attack properties")]
    [Tooltip("Duration of ramming movement")]
    public float rammingDuration = 1f;

    [BoxGroup("Attack properties")]
    [Tooltip("Damage to player on contact")]
    public float meleeDamage = 10f;
    
    [BoxGroup("Patrol properties")]
    [Tooltip("Points defining the path of the patrol")]
    public Vector2[] patrollingPoints = {};

    [BoxGroup("Patrol properties")]
    [Tooltip("Time pause after achieving patrol point and before moving to the next")]
    public float patrollingPause = 3f;

    [BoxGroup("Knockback properties")]
    [Tooltip("Knockback (and move disabling) duration after taking damage by unit (in seconds)")]
    public float knockbackDuration = .2f;
    
    [BoxGroup("Knockback properties")]
    [Tooltip("Knockback force (applies once after taking damage)")]
    public float knockbackForce = 15f;

    // END INSPECTOR DECLARATION
    
    private AutoStateMachine<RammingEnemyState> _stateMachine;
    private Animator _animator;
    private EnemyDetectArea _detectArea;
    private EnemyDamageArea _damageArea;
    private PlayerCore _playerCore;
    private float _patrollingPauseEndTime;
    private float _pauseAfterAttackEndTime;
    private float _rammingEndTime;
    private int? _rammingDirection;
    private bool _moveEnabled = true;

    private int _currentPatrollingPointIndex = 0;
    private int CurrentPatrollingPointIndex {
        get => _currentPatrollingPointIndex;
        set => _currentPatrollingPointIndex = value % patrollingPoints.Length;
    }

    private RammingEnemyState State => _stateMachine.CurrentState;
    
    private bool IsPlayerVisible => _detectArea.isPlayerVisible;
    
    private Vector2 PlayerPosition => _detectArea.SupposedPlayerPosition;
    
    private bool CanAttack => _damageArea.canAttack;

    private void InitStateMachine() {
        _stateMachine = new AutoStateMachine<RammingEnemyState>(
            patrollingPoints.Length > 0 ? RammingEnemyState.Patrolling : RammingEnemyState.Standing
        );
        
        // from STANDING
        _stateMachine.AddTransition(RammingEnemyState.Standing, RammingEnemyState.Patrolling, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsWalking", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Standing, RammingEnemyState.Chasing, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Standing, RammingEnemyState.Attacking, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsAttacking", true);
        });

        // from PATROLLING
        _stateMachine.AddTransition(RammingEnemyState.Patrolling, RammingEnemyState.Standing, () => {
            StopHorizontalMove();
            _patrollingPauseEndTime = Time.fixedTime + patrollingPause;
            CurrentPatrollingPointIndex++;
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsStanding", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Patrolling, RammingEnemyState.Chasing, () => {
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Patrolling, RammingEnemyState.Attacking, () => {
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsAttacking", true);
        });

        // from CHASING
        _stateMachine.AddTransition(RammingEnemyState.Chasing, RammingEnemyState.Attacking, () => {
            StopHorizontalMove();
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsAttacking", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Chasing, RammingEnemyState.Returning, () => {
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsWalking", true);
        });
        
        // from ATTACKING
        _stateMachine.AddTransition(RammingEnemyState.Attacking, RammingEnemyState.Resting, () => {
            _pauseAfterAttackEndTime = Time.fixedTime + restDuration;
            _rammingDirection = null;
            StopHorizontalMove();
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsResting", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Attacking, RammingEnemyState.Stunning, () => {
            _pauseAfterAttackEndTime = Time.fixedTime + stunDuration;
            _rammingDirection = null;
            StopHorizontalMove();
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsStunning", true);
        });
        
        // from RESTING
        _stateMachine.AddTransition(RammingEnemyState.Resting, RammingEnemyState.Attacking, () => {
            _animator.SetBool("IsResting", false);
            _animator.SetBool("IsAttacking", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Resting, RammingEnemyState.Chasing, () => {
            _animator.SetBool("IsResting", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Resting, RammingEnemyState.Returning, () => {
            _animator.SetBool("IsResting", false);
            _animator.SetBool("IsWalking", true);
        });
        
        // from STUNNING
        _stateMachine.AddTransition(RammingEnemyState.Stunning, RammingEnemyState.Attacking, () => {
            _animator.SetBool("IsStunning", false);
            _animator.SetBool("IsAttacking", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Stunning, RammingEnemyState.Chasing, () => {
            _animator.SetBool("IsStunning", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Stunning, RammingEnemyState.Returning, () => {
            _animator.SetBool("IsStunning", false);
            _animator.SetBool("IsWalking", true);
        });
        
        // from RETURNING
        _stateMachine.AddTransition(RammingEnemyState.Returning, RammingEnemyState.Chasing, () => {
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Returning, RammingEnemyState.Standing, () => {
            _patrollingPauseEndTime = Time.fixedTime + patrollingPause;
            CurrentPatrollingPointIndex++;
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsStanding", true);
        });
        _stateMachine.AddTransition(RammingEnemyState.Returning, RammingEnemyState.Attacking , () => {
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsAttacking", true);
        });
    }

    private void InitStateChanging() {
        _stateMachine.AddAutoTransition(RammingEnemyState.Standing, currentTime => {
            if (!new[] {RammingEnemyState.Patrolling, RammingEnemyState.Returning}.Contains(State)) {
                return false;
            }

            var targetX = patrollingPoints[CurrentPatrollingPointIndex].x;
            var currentX = transform.position.x;
            var distance = targetX - currentX;
            return Math.Abs(distance) < Config.DistancePrecision;
        });

        _stateMachine.AddAutoTransition(RammingEnemyState.Patrolling, currentTime => 
            State == RammingEnemyState.Standing &&
            patrollingPoints.Length > 1 &&
            currentTime > _patrollingPauseEndTime
        );

        _stateMachine.AddAutoTransitions(RammingEnemyState.Chasing, new Func<float, bool>[] {
            currentTime => 
                new[] {
                    RammingEnemyState.Standing, RammingEnemyState.Patrolling, RammingEnemyState.Returning
                }.Contains(State) && IsPlayerVisible,
            currentTime => 
                new[] {RammingEnemyState.Resting, RammingEnemyState.Stunning}.Contains(State) && 
                currentTime >= _pauseAfterAttackEndTime && 
                Mathf.Abs(PlayerPosition.x - transform.position.x) > attackDistance
        });

        _stateMachine.AddAutoTransitions(RammingEnemyState.Attacking, new Func<float, bool>[] {
            currentTime => 
                new[] {
                    RammingEnemyState.Standing, RammingEnemyState.Patrolling,
                    RammingEnemyState.Returning, RammingEnemyState.Chasing
                }.Contains(State) && IsPlayerVisible && 
                Mathf.Abs(PlayerPosition.x - transform.position.x) <= attackDistance,
            currentTime => 
                new[] {RammingEnemyState.Resting, RammingEnemyState.Stunning}.Contains(State) &&
                currentTime >= _pauseAfterAttackEndTime && IsPlayerVisible &&
                Mathf.Abs(PlayerPosition.x - transform.position.x) <= attackDistance
        });

        _stateMachine.AddAutoTransition(RammingEnemyState.Resting, currentTime => 
            State == RammingEnemyState.Attacking && _rammingDirection != null && 
            (currentTime >= _rammingEndTime || !stunOnObstaclesHit && !CanMoveForward)
        );

        _stateMachine.AddAutoTransition(RammingEnemyState.Stunning, currentTime => 
            State == RammingEnemyState.Attacking && stunOnObstaclesHit && !CanMoveForward
        );

        _stateMachine.AddAutoTransitions(RammingEnemyState.Returning, new Func<float, bool>[] {
            currentTime =>
                State == RammingEnemyState.Chasing && 
                Mathf.Abs(PlayerPosition.x - transform.position.x) < attackDistance &&
                !IsPlayerVisible,
            currentTime => 
                new[] {RammingEnemyState.Resting, RammingEnemyState.Stunning}.Contains(State) &&
                currentTime >= _pauseAfterAttackEndTime &&
                Mathf.Abs(PlayerPosition.x - transform.position.x) < attackDistance &&
                !IsPlayerVisible,
        });
    }

    private void InitStateHandlers() {
        _stateMachine.AddStateHandler(RammingEnemyState.Patrolling, currentTime => {
            if (!_moveEnabled) {
                return;
            }
            
            var targetX = patrollingPoints[CurrentPatrollingPointIndex].x;
            var currentX = transform.position.x;
            var direction = Math.Sign(targetX - currentX);
            HorizontalMove(direction, 0.5f);
        });

        _stateMachine.AddStateHandler(RammingEnemyState.Chasing, currentTime => {
            if (!_moveEnabled) {
                return;
            }

            var targetX = PlayerPosition.x;
            var currentX = transform.position.x;
            var direction = Math.Sign(targetX - currentX);
            HorizontalMove(direction);
        });

        _stateMachine.AddStateHandler(RammingEnemyState.Attacking, currentTime => {
            if (!_moveEnabled) {
                return;
            }
            if (_rammingDirection.HasValue) {
                SimpleHorizontalMove(_rammingDirection.Value, rammingSpeed);
            } else {
                var targetX = PlayerPosition.x;
                var currentX = transform.position.x;
                Direction = Math.Sign(targetX - currentX);
            }  
        });

        _stateMachine.AddStateHandler(RammingEnemyState.Returning, currentTime => {
            if (!_moveEnabled) {
                return;
            }

            var targetX = patrollingPoints[CurrentPatrollingPointIndex].x;
            var currentX = transform.position.x;
            var direction = Math.Sign(targetX - currentX);
            HorizontalMove(direction, 0.5f);
        });
    }
    
    protected override void Awake() {
        base.Awake();
        
        _playerCore = GameManager.Instance.player.Core;
        _animator = GetComponent<Animator>();
        
        _detectArea = GetComponentInChildren<EnemyDetectArea>();
        if (!_detectArea) {
            throw new Exception($"No detect area on enemy");
        }

        _damageArea = GetComponentInChildren<EnemyDamageArea>();
        if (!_damageArea) {
            throw new Exception($"No damage area on enemy");
        }

        if (patrollingPoints.Length == 0) {
            patrollingPoints = new Vector2[] { transform.position };
        }

        InitStateMachine();
        InitStateChanging();
        InitStateHandlers();

        var core = GetComponent<UnitCore>();
        core.KnockbackCondition = () => State != RammingEnemyState.Attacking || knockbackWhileAttacking;
        core.KnockbackedChanged += value => _moveEnabled = !value;
    }
    
    private void FixedUpdate() {
        _stateMachine.DoIteration(Time.fixedTime);
        
        if (CanAttack) {
            Vector2 direction = _playerCore.transform.position - transform.position;
            _playerCore.TakeDamage(meleeDamage, direction);
        }
    }
    
    // ANIMATION EVENTS
    
    public void AttackWindowStart() {
        _rammingDirection = Direction;
        _rammingEndTime = Time.fixedTime + rammingDuration;
    }
}