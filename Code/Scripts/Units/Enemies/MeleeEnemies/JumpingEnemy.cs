using System;
using System.Collections;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

enum JumpingEnemyState {
    Standing, Patrolling, Chasing, WaitingForAttack, Attacking, Returning
}


[RequireComponent(typeof(Animator))]
public class JumpingEnemy : EarthMovable {

    [BoxGroup("Attack properties")]
    [Tooltip("Damage to player on contact")]
    public float damage = 10f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Maximal distance to player to jump")]
    public float attackDistance = 2f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Pause between jump attacks (duration of WaitingForAttack state)")]
    public float attackPause = 3f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Height of jump attack (from take-off point)")]
    public float jumpHeight = 3f;
    
    [BoxGroup("Attack properties")]
    [Tooltip("Should or not enemy try to predict player movement before jump")]
    public bool jumpAhead;
    
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
    public float knockbackForce = 150f;

    // END INSPECTOR DECLARATION

    private AutoStateMachine<JumpingEnemyState> _stateMachine;
    private PlayerMovable _playerMovable;
    private PlayerCore _playerCore;
    private EnemyDetectArea _detectArea;
    private EnemyDamageArea _damageArea;
    private Animator _animator;
    private float _patrollingPauseEndTime;
    private bool _isAttacking;
    private float _attackPauseEndTime;
    private bool _moveEnabled = true;

    private int _currentPatrollingPointIndex = 0;
    private int CurrentPatrollingPointIndex {
        get => _currentPatrollingPointIndex;
        set => _currentPatrollingPointIndex = value % patrollingPoints.Length;
    }

    private JumpingEnemyState State => _stateMachine.CurrentState;

    private bool IsPlayerVisible => _detectArea.isPlayerVisible;
    
    private Vector2 PlayerPosition => _detectArea.SupposedPlayerPosition;
    
    private bool CanAttack => _damageArea.canAttack;

    private void InitStateMachine() {
        _stateMachine = new AutoStateMachine<JumpingEnemyState>(
            patrollingPoints.Length > 0 ? JumpingEnemyState.Patrolling : JumpingEnemyState.Standing
        );
        
        // from STANDING
        _stateMachine.AddTransition(JumpingEnemyState.Standing, JumpingEnemyState.Patrolling, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsWalking", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Standing, JumpingEnemyState.Chasing, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Standing, JumpingEnemyState.WaitingForAttack, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsWaitingForAttack", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Standing, JumpingEnemyState.Attacking, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsAttacking", true);
        });
        
        // from PATROLLING
        _stateMachine.AddTransition(JumpingEnemyState.Patrolling, JumpingEnemyState.Standing, () => {
            StopHorizontalMove();
            _patrollingPauseEndTime = Time.fixedTime + patrollingPause;
            CurrentPatrollingPointIndex++;
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsStanding", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Patrolling, JumpingEnemyState.Chasing, () => {
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Patrolling, JumpingEnemyState.WaitingForAttack, () => {
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsWaitingForAttack", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Patrolling, JumpingEnemyState.Attacking, () => {
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsAttacking", true);
        });
        
        // from CHASING
        _stateMachine.AddTransition(JumpingEnemyState.Chasing, JumpingEnemyState.WaitingForAttack, () => {
            StopHorizontalMove();
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsWaitingForAttack", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Chasing, JumpingEnemyState.Attacking, () => {
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsAttacking", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Chasing, JumpingEnemyState.Returning, () => {
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsWalking", true);
        });
        
        // from WAITING_FOR_ATTACK
        _stateMachine.AddTransition(JumpingEnemyState.WaitingForAttack, JumpingEnemyState.Chasing, () => {
            _animator.SetBool("IsWaitingForAttack", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.WaitingForAttack, JumpingEnemyState.Attacking, () => {
            _animator.SetBool("IsWaitingForAttack", false);
            _animator.SetBool("IsAttacking", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.WaitingForAttack, JumpingEnemyState.Returning, () => {
            _animator.SetBool("IsWaitingForAttack", false);
            _animator.SetBool("IsWalking", true);
        });
        
        // from ATTACKING
        _stateMachine.AddTransition(JumpingEnemyState.Attacking, JumpingEnemyState.Chasing, () => {
            _attackPauseEndTime = Time.fixedTime + attackPause;
            _isAttacking = false;
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Attacking, JumpingEnemyState.WaitingForAttack, () => {
            _attackPauseEndTime = Time.fixedTime + attackPause;
            _isAttacking = false;
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsWaitingForAttack", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Attacking, JumpingEnemyState.Returning, () => {
            _attackPauseEndTime = Time.fixedTime + attackPause;
            _isAttacking = false;
            _animator.SetBool("IsAttacking", false);
            _animator.SetBool("IsWalking", true);
        });
        
        // from RETURNING
        _stateMachine.AddTransition(JumpingEnemyState.Returning, JumpingEnemyState.Standing, () => {
            _patrollingPauseEndTime = Time.fixedTime + patrollingPause;
            CurrentPatrollingPointIndex++;
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsStanding", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Returning, JumpingEnemyState.Chasing, () => {
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsRunning", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Returning, JumpingEnemyState.WaitingForAttack, () => {
            StopHorizontalMove();
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsWaitingForAttack", true);
        });
        _stateMachine.AddTransition(JumpingEnemyState.Returning, JumpingEnemyState.Attacking, () => {
            _animator.SetBool("IsWalking", false);
            _animator.SetBool("IsAttacking", true);
        });
    }

    private void InitStateChanging() {
        _stateMachine.AddAutoTransition(JumpingEnemyState.Standing, currentTime => {
            if (!new[] {JumpingEnemyState.Patrolling, JumpingEnemyState.Returning}.Contains(State)) {
                return false;
            }
            
            var targetX = patrollingPoints[CurrentPatrollingPointIndex].x;
            var currentX = transform.position.x;
            var distance = targetX - currentX;
            return Math.Abs(distance) < Config.DistancePrecision;
        });

        _stateMachine.AddAutoTransition(JumpingEnemyState.Patrolling, currentTime => 
            State == JumpingEnemyState.Standing &&
            patrollingPoints.Length > 1 &&
            currentTime > _patrollingPauseEndTime
        );
        
        _stateMachine.AddAutoTransitions(JumpingEnemyState.Chasing, new Func<float, bool>[] {
            currentTime => new[] {
                JumpingEnemyState.Standing, JumpingEnemyState.Patrolling, JumpingEnemyState.Returning
            }.Contains(State) && IsPlayerVisible,
            currentTime => {
                if (!new[] {JumpingEnemyState.WaitingForAttack, JumpingEnemyState.Attacking}.Contains(State)) {
                    return false;
                }

                var distance = Mathf.Abs(PlayerPosition.x - transform.position.x);
                return State == JumpingEnemyState.Attacking 
                    ? _isAttacking && Rb.velocity.y < Config.FallingVelocityThreshold && IsGrounded && distance > attackDistance 
                    : distance > attackDistance;
            }
        });

        _stateMachine.AddAutoTransition(JumpingEnemyState.WaitingForAttack, currentTime => {
            if (!new[] {
                JumpingEnemyState.Standing, JumpingEnemyState.Patrolling, 
                JumpingEnemyState.Chasing, JumpingEnemyState.Attacking, 
                JumpingEnemyState.Returning
            }.Contains(State)) {
                return false;
            }
            
            if (!IsPlayerVisible) {
                return false;
            }
            
            var distance = Mathf.Abs(PlayerPosition.x - transform.position.x);
            return State == JumpingEnemyState.Attacking
                ? _isAttacking && Rb.velocity.y < Config.FallingVelocityThreshold && IsGrounded && distance <= attackDistance 
                : distance <= attackDistance && currentTime < _attackPauseEndTime;

        });

        _stateMachine.AddAutoTransition(JumpingEnemyState.Attacking, currentTime => {
            if (!new[] {
                JumpingEnemyState.Standing, JumpingEnemyState.Patrolling, 
                JumpingEnemyState.Chasing, JumpingEnemyState.WaitingForAttack, 
                JumpingEnemyState.Returning
            }.Contains(State)) {
                return false;
            }
            
            if (!IsPlayerVisible || currentTime < _attackPauseEndTime) {
                return false;
            }

            var distance = Mathf.Abs(PlayerPosition.x - transform.position.x);
            return distance <= attackDistance;

        });

        _stateMachine.AddAutoTransitions(JumpingEnemyState.Returning, new Func<float, bool>[] {
            currentTime => 
                State == JumpingEnemyState.Attacking && 
                _isAttacking && 
                Rb.velocity.y < Config.FallingVelocityThreshold &&
                IsGrounded &&
                Mathf.Abs(PlayerPosition.x - transform.position.x) < attackDistance &&
                !IsPlayerVisible,
            currentTime => 
                State == JumpingEnemyState.WaitingForAttack && 
                !IsPlayerVisible,
            currentTime => 
                State == JumpingEnemyState.Chasing && 
                Mathf.Abs(PlayerPosition.x - transform.position.x) < attackDistance &&
                !IsPlayerVisible
        });
    }

    private void InitStateHandlers() {
        _stateMachine.AddStateHandler(JumpingEnemyState.Patrolling, currentTime => {
            if (!_moveEnabled) {
                return;
            }
            
            var targetX = patrollingPoints[CurrentPatrollingPointIndex].x;
            var currentX = transform.position.x;
            var direction = Math.Sign(targetX - currentX);
            HorizontalMove(direction, 0.5f);
        });

        _stateMachine.AddStateHandler(JumpingEnemyState.Chasing, currentTime => {
            if (!_moveEnabled) {
                return;
            }

            var targetX = PlayerPosition.x;
            var currentX = transform.position.x;
            var direction = Math.Sign(targetX - currentX);
            HorizontalMove(direction);
        });

        _stateMachine.AddStateHandler(JumpingEnemyState.WaitingForAttack, currentTime => {
            if (!_moveEnabled) {
                return;
            }
            
            var targetX = PlayerPosition.x;
            var currentX = transform.position.x;
            Direction = Math.Sign(targetX - currentX);
        });

        _stateMachine.AddStateHandler(JumpingEnemyState.Returning, currentTime => {
            if (!_moveEnabled) {
                return;
            }
            
            var targetX = patrollingPoints[CurrentPatrollingPointIndex].x;
            var currentX = transform.position.x;
            var direction = Math.Sign(targetX - currentX);
            HorizontalMove(direction, 0.5f);
        });
    }

    private float CalculateJumpDuration(float startHeight, float jumpHeight, float gravity) {
        var maxHeight = startHeight < 0 ? jumpHeight : startHeight + jumpHeight; // maxHeight не может быть меньше jumpHeight

        var firstSqrt = Mathf.Sqrt(2 * gravity * jumpHeight);
        var secondSqrt = Mathf.Sqrt(2 * gravity * maxHeight);
        
        var jumpDuration1 = (firstSqrt - secondSqrt) / gravity;
        var jumpDuration2 = (firstSqrt + secondSqrt) / gravity;
        return Mathf.Max(jumpDuration1, jumpDuration2);
    }

    private Vector2 CalculateJumpVelocity(float jumpDuration, float jumpHeight, float distance, float gravity) {
        var velocityX = distance / jumpDuration;
        var velocityY = Mathf.Sqrt(2 * gravity * jumpHeight);
        return new Vector2(velocityX, velocityY);
    }

    protected override void Awake() {
        base.Awake();

        _playerCore = GameManager.Instance.player.Core;
        _playerMovable = GameManager.Instance.player.Movable;
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
            patrollingPoints = new Vector2[] {transform.position};
        }
        
        InitStateMachine();
        InitStateChanging();
        InitStateHandlers();
        
        var core = GetComponent<UnitCore>();
        core.KnockbackCondition = () => State != JumpingEnemyState.Attacking;
        core.KnockbackedChanged += value => _moveEnabled = !value;
    }

    private void FixedUpdate() {
        _stateMachine.DoIteration(Time.fixedTime);
        
        if (CanAttack) {
            Vector2 direction = _playerCore.transform.position - transform.position;
            _playerCore.TakeDamage(damage, direction);
        }
    }
    
    // ANIMATION EVENTS
    
    public void JumpWindowStart() {
        var targetPosition = PlayerPosition;
        var currentPosition = transform.position;
        float startHeight = currentPosition.y - targetPosition.y;
        var jumpDuration = CalculateJumpDuration(startHeight, jumpHeight, Rb.gravityScale * -Physics2D.gravity.y);
        float distance = targetPosition.x - currentPosition.x;
        if (jumpAhead && _playerMovable) {
            distance += jumpDuration * _playerMovable.Velocity.x;
        }

        Rb.velocity = CalculateJumpVelocity(jumpDuration, jumpHeight, distance, Rb.gravityScale * -Physics2D.gravity.y);
        _isAttacking = true;
    }
}
