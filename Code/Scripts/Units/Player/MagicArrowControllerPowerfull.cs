using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public enum PowerfullMagicArrowState {
    Nothing, Preparing, Aiming
}

public class MagicArrowControllerPowerfull : MonoBehaviour {
    
    [Tooltip("Energy cost of one cast")]
    public float castEnergyCost = 10f;
    
    public float shotDelay = 0.5f;
    public float minVelocity = 10f;
    public float maxVelocity = 10f;
    
    [Tooltip("Shell speed velocity incrementation per second")]
    public float velocityIncreaseSpeed = 10f;
    
    [Tooltip("Magic arrow shell prefab")]
    public GameObject shellPrefab;
    
    [Tooltip("Aim line renderer reference")]
    public LineRenderer aimLine;

    // END INSPECTOR DECLARATION

    private StateMachine<PowerfullMagicArrowState> _stateMachine;
    private float _delayEndTime;
    private float _preparationCostPerSecond;

    private PlayerMovable _playerMovable;
    private PlayerCore _playerCore;
    private Vector2 _inputVector;
    [CanBeNull] private Shell _preparedShell;
    
    private float _shellVelocity;
    private float ShellVelocity {
        get => _shellVelocity;
        set => _shellVelocity = Mathf.Clamp(value, minVelocity, maxVelocity);
    }

    private void InitStateTransitions() {
        _stateMachine = new StateMachine<PowerfullMagicArrowState>(PowerfullMagicArrowState.Nothing);
        _stateMachine.AddTransition(PowerfullMagicArrowState.Nothing, PowerfullMagicArrowState.Preparing, () => {
            _delayEndTime = Time.fixedTime + shotDelay;
        });
        _stateMachine.AddTransition(PowerfullMagicArrowState.Preparing, PowerfullMagicArrowState.Aiming, () => {
            PrepareShell();
            ShellVelocity = minVelocity;
            // TODO add arrow ready effect
        });
        _stateMachine.AddTransition(PowerfullMagicArrowState.Preparing, PowerfullMagicArrowState.Nothing);
        _stateMachine.AddTransition(PowerfullMagicArrowState.Aiming, PowerfullMagicArrowState.Nothing, () => {
            Shoot();
        });
    }
    
    private Vector2 CalculateTrajectory(Vector2 startPosition, Vector2 startVelocity, float gravity, float time) {
        var x = startPosition.x + startVelocity.x * time;
        var y = startPosition.y + startVelocity.y * time + gravity * Mathf.Pow(time, 2) / 2;
        return new Vector2(x, y);
    }

    private Vector2 GetAbsoluteAimDirection() {
        var direction = _inputVector;
        if (Math.Abs(direction.magnitude) < Config.FloatPrecision) {
            direction = Vector2.right * _playerMovable.Direction;
        }
        return direction.normalized;
    }

    private void PrepareShell() {
        var shellObject = Instantiate(shellPrefab, transform.position, Quaternion.identity);
        shellObject.transform.parent = transform;
        _preparedShell = shellObject.GetComponent<Shell>();
        _preparedShell.gameObject.SetActive(false);
    }

    private void Shoot() {
        aimLine.enabled = false;

        var direction = GetAbsoluteAimDirection();
        
        _preparedShell.gameObject.SetActive(true);
        _preparedShell.transform.parent = null;
        _preparedShell.Throw(direction * _shellVelocity);

        _preparedShell = null;
    }

    private void UpdateAimLine() {
        var direction = GetAbsoluteAimDirection();

        const float segmentSize = 5f;
        const float aimLineLength = 100f;
        
        var segmentsCount = (int)Math.Round(aimLineLength / segmentSize);
        var segmentTime = segmentSize / _shellVelocity;

        var startPoint = transform.position;
        var points = new List<Vector3>(new []{Vector3.zero});
        var prevPoint = startPoint;
        for (var i = 1; i < segmentsCount; ++i) {
            var point = CalculateTrajectory(startPoint,
                direction * _shellVelocity,
                _preparedShell.GravityFactor,
                i * segmentTime
            );
            
            // Debug.DrawLine(transform.TransformPoint(points.Last()), transform.TransformPoint(absolutePoint), Color.red, 0.02f);
            RaycastHit2D hit =  Physics2D.Linecast(prevPoint, point, Config.ObstacleLayerMask);
            if (hit) {
                points.Add(transform.InverseTransformPoint(hit.point));
                break;
            }

            prevPoint = point;
            points.Add(transform.InverseTransformPoint(point));
        }
        
        aimLine.positionCount = points.Count;
        aimLine.SetPositions(points.ToArray());
        aimLine.enabled = true;
    }
    
    private void FixedUpdate() {
        if (_stateMachine.CurrentState == PowerfullMagicArrowState.Preparing) {
            var neededEnergy = _preparationCostPerSecond * Time.fixedDeltaTime;
            if (_playerCore.CurrentEnergy < neededEnergy) {
                // TODO restore spent energy
                _stateMachine.ChangeState(PowerfullMagicArrowState.Nothing);
            } else {
                _playerCore.CurrentEnergy -= neededEnergy;

                if (Time.fixedTime > _delayEndTime) {
                    _stateMachine.ChangeState(PowerfullMagicArrowState.Aiming);
                }
            }
        } else if (_stateMachine.CurrentState == PowerfullMagicArrowState.Aiming) {
            if (ShellVelocity < maxVelocity) {
                ShellVelocity += velocityIncreaseSpeed * Time.fixedDeltaTime;
            }
            UpdateAimLine();
        }
    }

    public void Initialize() {
        _playerMovable = GameManager.Instance.player.Movable;
        _playerCore = GameManager.Instance.player.Core;
        InitStateTransitions();
        _preparationCostPerSecond = castEnergyCost / shotDelay;
        
        GameManager.Instance.playerInputActions["Aim"].performed += context => {
            if (!enabled) {
                return;
            }
            
            _inputVector = context.ReadValue<Vector2>();
        };
        
        _playerMovable.DirectionChanged += value => {
            if (!enabled) {
                return;
            }
            
            if (_stateMachine.CurrentState == PowerfullMagicArrowState.Aiming) {
                UpdateAimLine();
            }
        };

        GameManager.Instance.playerInputActions["Skill"].started += context => {
            if (!enabled) {
                return;
            }
            
            if (_stateMachine.CurrentState == PowerfullMagicArrowState.Nothing) {
                _stateMachine.ChangeState(PowerfullMagicArrowState.Preparing);
            }
        };
        GameManager.Instance.playerInputActions["Skill"].canceled += context => {
            if (!enabled) {
                return;
            }

            if (_stateMachine.CurrentState == PowerfullMagicArrowState.Preparing) {
                // TODO restore spent energy
                _stateMachine.ChangeState(PowerfullMagicArrowState.Nothing);
            } else if (_stateMachine.CurrentState == PowerfullMagicArrowState.Aiming) {
                _stateMachine.ChangeState(PowerfullMagicArrowState.Nothing);
            }
        };
    }
}