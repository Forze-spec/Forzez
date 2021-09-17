using System.Linq;
using UnityEngine;

public enum MagicArrowState {
    Default, Preparation, Ready
}

public class MagicArrowController : MonoBehaviour {
    [Tooltip("Energy cost of one cast")]
    public float castEnergyCost = 10f;
    
    [Tooltip("Velocity of fired shell")]
    public float shellSpeed = 10f;
    
    [Tooltip("Magic arrow shell prefab")]
    public GameObject shellPrefab;

    [Tooltip("Parent transform of left and right aim lines")]
    public Transform aimLinesContainer;
    
    [Tooltip("Left aim line sprite renderer")]
    public SpriteRenderer leftAimLine;
    
    [Tooltip("Right aim line sprite renderer")]
    public SpriteRenderer rightAimLine;
    
    [Tooltip("Color of aim lines while preparing to shoot")]
    public Color linesDefaultColor = new Color(1f, 1f, 1f, 0.5f);
    
    [Tooltip("Color of aim lines when ready to shoot")]
    public Color linesHighlightColor = new Color(1f, 1f, 1f, 1f);

    [Tooltip("Angle between aim lines on preparation start")]
    public float startPreparingAngle = 30f;
    
    [Tooltip("Angular speed of aim rotation (per second)")]
    public float aimRotationSpeed = 30f;
    
    // END INSPECTOR DECLARATION
    
    private PlayerStateMachine _playerStateMachine;
    private PlayerCore _playerCore;
    private PlayerMovable _playerMovable;
    
    private Vector2 _inputVector;
    private Vector2 _currentAimDirection;
    private StateMachine<MagicArrowState> _stateMachine;
    private float _preparationDuration;
    private float _preparationEndTime;

    private bool IsControlEnabled => enabled && _playerStateMachine.controlEnabled;

    private void InitStateMachine() {
        _stateMachine = new StateMachine<MagicArrowState>(MagicArrowState.Default);
        _stateMachine.AddTransition(MagicArrowState.Default, MagicArrowState.Preparation, () => {
            _currentAimDirection = _inputVector.normalized;
            EnableAimLines();
            UpdateAimDirection();
            UpdateAngleBetweenAimLines();
            _preparationEndTime = Time.fixedTime + _preparationDuration;
            // TODO activate walking in _playerMovable
            // TODO run aim preparation animation on second layer
        });
        _stateMachine.AddTransition(MagicArrowState.Preparation, MagicArrowState.Ready, () => {
            HighlightAimLines();
            // TODO run aim ready animation on second layer
        });
        _stateMachine.AddTransition(MagicArrowState.Preparation, MagicArrowState.Default, () => {
            DisableAimLines();
            // TODO deactivate walking in _playerMovable
            // TODO stop aim preparation animation on second layer
        });
        _stateMachine.AddTransition(MagicArrowState.Ready, MagicArrowState.Default, () => {
            DisableAimLines();
            // TODO deactivate walking in _playerMovable
            // TODO stop aim ready animation on second layer
        });
    }
    
    private void Shoot() {
        _playerCore.CurrentEnergy -= castEnergyCost;

        var shellObject = Instantiate(shellPrefab, aimLinesContainer.position, Quaternion.identity);
        var shellHandler = shellObject.GetComponent<Shell>();
        shellHandler.Throw(_currentAimDirection * shellSpeed);
    }
    
    private void EnableAimLines() {
        aimLinesContainer.gameObject.SetActive(true);
        leftAimLine.color = linesDefaultColor;
        rightAimLine.color = linesDefaultColor;
    }

    private void HighlightAimLines() {
        leftAimLine.color = linesHighlightColor;
        rightAimLine.color = linesHighlightColor;
    }

    private void DisableAimLines() {
        aimLinesContainer.gameObject.SetActive(false);
    }

    private void UpdateAngleBetweenAimLines() {
        var restPart = Mathf.Max((_preparationEndTime - Time.fixedTime) / _preparationDuration, 0);
        var neededAngle = restPart * startPreparingAngle;

        leftAimLine.transform.parent.localEulerAngles = new Vector3(0, 0, neededAngle / 2);
        rightAimLine.transform.parent.localEulerAngles = new Vector3(0, 0, -neededAngle / 2);
    }
    
    private void UpdateAimDirection() {
        if (!(_inputVector.magnitude > 0)) {
            return;
        }
        
        var target = _inputVector.normalized;
        var current = _currentAimDirection;
        var currentAngle = Vector2.SignedAngle(Vector2.up, current);
        var targetAngle = Vector2.SignedAngle(Vector2.up, target);

        var resultAngle = Mathf.Min(aimRotationSpeed * Time.fixedDeltaTime, Mathf.Abs(targetAngle - currentAngle));
        resultAngle = resultAngle * Mathf.Sign(targetAngle - currentAngle);
        _currentAimDirection = Quaternion.Euler(0, 0, resultAngle) * _currentAimDirection;

        // получаем из _currentAimDirection локальный вектор в transform
        var localVector = transform.InverseTransformVector(_currentAimDirection);

        // устанавливаем локальное вращение для aimLinesContainer в пространстве transform
        aimLinesContainer.localEulerAngles = new Vector3(
            0, 0, Vector2.SignedAngle(transform.right, localVector)
        );
    }

    private void FixedUpdate() {
        if (_stateMachine.CurrentState == MagicArrowState.Default) {
            if (!IsControlEnabled) {
                return;
            }
            
            if (
                _inputVector.magnitude > 0 &&
                _playerCore.CurrentEnergy >= castEnergyCost &&
                new[] {
                    PlayerState.Standing, PlayerState.Running, PlayerState.Jumping, PlayerState.Falling
                }.Contains(_playerStateMachine.State)
            ) {
                _stateMachine.ChangeState(MagicArrowState.Preparation);
            }
            return;
        }
        
        if (!IsControlEnabled) {
            _stateMachine.ChangeState(MagicArrowState.Default);
        }

        UpdateAimDirection();

        if (_stateMachine.CurrentState == MagicArrowState.Preparation) {
            if (!(_inputVector.magnitude > 0) || _playerCore.CurrentEnergy < castEnergyCost) {
                _stateMachine.ChangeState(MagicArrowState.Default);
            }
            
            UpdateAngleBetweenAimLines();
            
            if (Time.fixedTime > _preparationEndTime) {
                _stateMachine.ChangeState(MagicArrowState.Ready);
            }
        } else if (_stateMachine.CurrentState == MagicArrowState.Ready) {
            if (_playerCore.CurrentEnergy < castEnergyCost) {
                _stateMachine.ChangeState(MagicArrowState.Default);
            }

            if (!(_inputVector.magnitude > 0)) {
                Shoot();
                _stateMachine.ChangeState(MagicArrowState.Default);
            }
        }
    }

    public void Initialize() {
        _playerStateMachine = GameManager.Instance.player.StateMachine;
        _playerCore = GameManager.Instance.player.Core;
        _playerMovable = GameManager.Instance.player.Movable;

        InitStateMachine();

        _preparationDuration = 1f; // TODO get from preparation animation

        // необходимо для обновления позиции прицела сразу после смены направления
        _playerMovable.DirectionChanged += (value) => {
            if (_stateMachine.CurrentState == MagicArrowState.Default) {
                return;
            }
            
            UpdateAimDirection();
        }; 

        _playerStateMachine.StateChanged += (oldState, newState) => {
            if (_stateMachine.CurrentState != MagicArrowState.Default && !new[] {
                PlayerState.Standing, PlayerState.Running, PlayerState.Jumping, PlayerState.Falling
            }.Contains(newState)) {
                _stateMachine.ChangeState(MagicArrowState.Default);
            }
        };

        GameManager.Instance.playerInputActions["Aim"].performed += context => {
            _inputVector = context.ReadValue<Vector2>();
        };
    }
}