using System;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

enum AirShootableEnemyState {
    Waiting, Patrolling, Attacking
}

[RequireComponent(typeof(Animator))]
public class AirShootableEnemy : AirMovable {
    
    [BoxGroup("Attack properties")]
    [Tooltip("Damage to player on contact")]
    public float meleeDamage = 10f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Time pause between range attacks")]
    public float rangeAttackPause = 1f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Point where shot spawns")]
    public Transform shotStartPoint;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Shell prefab")]
    public GameObject shellObject;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Predicate player movement when shoot")]
    public bool shootAhead;

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
    
    // END INSPECTOR DECLARATION
    
    private AutoStateMachine<AirShootableEnemyState> _stateMachine;
    private Animator _animator;
    private EnemyDetectArea _detectArea;
    private EnemyDamageArea _damageArea;
    private PlayerCore _playerCore;
    private PlayerMovable _playerMovable;
    private float _patrollingPauseEnd;
    private bool _moveEnabled = true;
    private float _attackPauseEnd;
    
    private int _currentPatrollingPointIndex = 0;
    private int CurrentPatrollingPointIndex {
        get => _currentPatrollingPointIndex;
        set => _currentPatrollingPointIndex = value % patrollingPoints.Length;
    }
    
    private AirShootableEnemyState State => _stateMachine.CurrentState;
    
    private bool IsPlayerVisible => _detectArea.isPlayerVisible;
    
    private Vector2 PlayerPosition => _detectArea.SupposedPlayerPosition;
    
    private bool CanAttack => _damageArea.canAttack;

    private void InitStateMachine() {
        _stateMachine = new AutoStateMachine<AirShootableEnemyState>(AirShootableEnemyState.Patrolling);

        // from PATROLLING
        _stateMachine.AddTransition(AirShootableEnemyState.Patrolling, AirShootableEnemyState.Waiting, () => {
            StopMovement();
            
            _animator.SetBool("IsPatrolling", false);
            _animator.SetBool("IsWaiting", true);

            _patrollingPauseEnd = Time.fixedTime + patrollingPause;
            CurrentPatrollingPointIndex++;
        });
        _stateMachine.AddTransition(AirShootableEnemyState.Patrolling, AirShootableEnemyState.Attacking, () => {
            StopMovement();
            _animator.SetBool("IsPatrolling", false);
            _animator.SetBool("IsAttacking", true);
            
            Direction = Math.Sign(_playerCore.transform.position.x - transform.position.x);
        });
        
        // from WAITING
        _stateMachine.AddTransition(AirShootableEnemyState.Waiting, AirShootableEnemyState.Patrolling, () => {
            _animator.SetBool("IsWaiting", false);
            _animator.SetBool("IsPatrolling", true);
        });
        _stateMachine.AddTransition(AirShootableEnemyState.Waiting, AirShootableEnemyState.Attacking, () => {
            _animator.SetBool("IsWaiting", false);
            _animator.SetBool("IsAttacking", true);
            
            Direction = Math.Sign(_playerCore.transform.position.x - transform.position.x);
        });
        
        //from ATTACKING
        _stateMachine.AddTransition(AirShootableEnemyState.Attacking, AirShootableEnemyState.Patrolling, () => {
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsPatrolling", true);
            _attackPauseEnd = Time.fixedTime + rangeAttackPause;
        });
        _stateMachine.AddTransition(AirShootableEnemyState.Attacking, AirShootableEnemyState.Waiting, () => {
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsWaiting", true);
            _attackPauseEnd = Time.fixedTime + rangeAttackPause;
        });
    }

    private void InitStateChanging() {
        _stateMachine.AddAutoTransition(AirShootableEnemyState.Patrolling, currentTime => 
            State == AirShootableEnemyState.Waiting && 
            currentTime > _patrollingPauseEnd
        );
        _stateMachine.AddAutoTransition(AirShootableEnemyState.Waiting, currentTime => {
            if (State != AirShootableEnemyState.Patrolling) {
                return false;
            }
                
            var distance = Vector2.Distance(patrollingPoints[CurrentPatrollingPointIndex],transform.position);
            return Math.Abs(distance) < Config.DistancePrecision;
        });
        _stateMachine.AddAutoTransition(AirShootableEnemyState.Attacking, currentTime => 
            new []{AirShootableEnemyState.Patrolling, AirShootableEnemyState.Waiting}.Contains(State) &&
            currentTime >= _attackPauseEnd &&
            IsPlayerVisible
        );
    }

    private void InitStateHandlers() {
        _stateMachine.AddStateHandler(AirShootableEnemyState.Patrolling, currentTime => {
            if (!_moveEnabled) {
                return;
            }
            
            var direction = patrollingPoints[CurrentPatrollingPointIndex] - (Vector2)transform.position;
            Move(direction);
        });
    }

    protected override void Awake() {
        base.Awake();

        _animator = GetComponent<Animator>();
        _playerCore = GameManager.Instance.player.Core;
        _playerMovable = GameManager.Instance.player.Movable;

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
        
        _animator.GetBehaviour<AirShootableEnemyAnimatorBehaviour>().AttackEnded += () => {
            if (State != AirShootableEnemyState.Attacking) {
                return;
            }
            
            _stateMachine.ChangeState(
                Time.fixedTime < _patrollingPauseEnd 
                    ? AirShootableEnemyState.Waiting 
                    : AirShootableEnemyState.Patrolling
            );
        };
    }

    private void FixedUpdate() {
        _stateMachine.DoIteration(Time.fixedTime);
        
        if (CanAttack) {
            Vector2 direction = _playerCore.transform.position - transform.position;
            _playerCore.TakeDamage(meleeDamage, direction);
        }
    }
    
    // ANIMATION EVENTS
    
    public void Shoot() {
        GameObject shell = Instantiate(shellObject);
        var shellStartPosition = shotStartPoint.position;
        
        shell.transform.position = shellStartPosition;
        var handler = shell.GetComponent<Shell>();
        
        var aheadSpeed = shootAhead && _playerMovable ? _playerMovable.Velocity : Vector2.zero;
        handler.Throw(shellStartPosition, PlayerPosition, aheadSpeed);
    }
}
