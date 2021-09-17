using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MovementController: MonoBehaviour {
    
    private PlayerCore _playerCore;
    private PlayerStateMachine _playerStateMachine;
    private PlayerMovable _playerMovable;
    private Animator _animator;
    
    private HashSet<PlatformEffector2D> _touchedOneWays = new HashSet<PlatformEffector2D>();

    private int _currentMoveInput; // текущий ввод левого стика
    private int _verticalAlignment;

    private bool CanMoveForward => _playerMovable.CanMoveForward;
    
    private bool IsGrounded => _playerMovable.IsGrounded;  // Стоит ли объект на поверхности или находится в воздухе

    private int Direction {  // Направление юнита (1 - право, -1 - лево)
        get => _playerMovable.Direction;
        set => _playerMovable.Direction = value;
    }

    private bool ControlEnabled => _playerStateMachine.controlEnabled;

    private void InitStateTransitions(PlayerStateMachine playerStateMachine) {
        playerStateMachine.AddTransition(PlayerState.Standing, PlayerState.Running, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsRunning", true);
        });
        playerStateMachine.AddTransition(PlayerState.Standing, PlayerState.Jumping, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsJumping", true);
        });
        playerStateMachine.AddTransition(PlayerState.Standing, PlayerState.Falling, () => {
            _animator.SetBool("IsStanding", false);
            _animator.SetBool("IsFalling", true);
        });
        
        playerStateMachine.AddTransition(PlayerState.Running, PlayerState.Standing, () => {
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsStanding", true);
        });
        playerStateMachine.AddTransition(PlayerState.Running, PlayerState.Jumping, () => {
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsJumping", true);
        });
        playerStateMachine.AddTransition(PlayerState.Running, PlayerState.Falling, () => {
            _animator.SetBool("IsRunning", false);
            _animator.SetBool("IsFalling", true);
        });
        
        playerStateMachine.AddTransition(PlayerState.Jumping, PlayerState.Falling, () => {
            _animator.SetBool("IsJumping", false);
            _animator.SetBool("IsFalling", true);
        });
        
        playerStateMachine.AddTransition(PlayerState.Falling, PlayerState.Standing, () => {
            _animator.SetBool("IsFalling", false);
            _animator.SetBool("IsStanding", true);
        });
        playerStateMachine.AddTransition(PlayerState.Falling, PlayerState.Running, () => {
            _animator.SetBool("IsFalling", false);
            _animator.SetBool("IsRunning", true);
        });
    }

    private void ResetState() {
        if (!IsGrounded) {
            if (_playerMovable.Velocity.y > Config.FallingVelocityThreshold) {
                _playerStateMachine.ChangeState(PlayerState.Jumping);
            } else {
                _playerStateMachine.ChangeState(PlayerState.Falling);
            }
        } else if (_currentMoveInput != 0) {
            _playerStateMachine.ChangeState(PlayerState.Running);
        } else {
            _playerStateMachine.ChangeState(PlayerState.Standing);
        }
    }

    private void HandleStartJumpActivation(InputAction.CallbackContext context) {
        if (!ControlEnabled) {
            return;
        }

        if (!new[] {PlayerState.Standing, PlayerState.Running}.Contains(_playerStateMachine.State)) {
            return;
        }

        if (_verticalAlignment == -1) {
            StartCoroutine(FallThrow());
        } else {
            _playerMovable.StartJump(_currentMoveInput);
            _playerStateMachine.ChangeState(PlayerState.Jumping);
        }
    }

    private IEnumerator FallThrow() {
        var affectedOneWays = new HashSet<PlatformEffector2D>(_touchedOneWays);
        foreach (var elem in affectedOneWays) {
            elem.colliderMask &= ~(1 << LayerMask.NameToLayer(Layers.Player));
        }
        yield return new WaitForSeconds(Config.OneWayFallingThrowTimeout);
        foreach (var elem in affectedOneWays) {
            if (elem != null) { // нужно для обработки перехода между сценами (сохраненных платформ может уже не существовать)
                elem.colliderMask |= 1 << LayerMask.NameToLayer(Layers.Player);
            }
        }
    }

    private void HandleEndJumpActivation(InputAction.CallbackContext context) {
        if (!ControlEnabled) {
            return;
        }
        
        if (_playerStateMachine.State != PlayerState.Jumping) {
            return;
        }
        StartCoroutine(_playerMovable.EndJump());
    }
    
    private void HandleStateChanging() {
        if (_playerStateMachine.State == PlayerState.Standing) {
            if (!IsGrounded && CanMoveForward) { // TODO remove CanMoveForward
                _playerStateMachine.ChangeState(PlayerState.Falling);
            } else if (_currentMoveInput != 0 && CanMoveForward) {
                _playerStateMachine.ChangeState(PlayerState.Running);
            }
        } else if (_playerStateMachine.State == PlayerState.Running) {
            if (!IsGrounded) {
                _playerStateMachine.ChangeState(PlayerState.Falling);
            } else if (_currentMoveInput == 0 || !CanMoveForward) {
                _playerStateMachine.ChangeState(PlayerState.Standing);
            }
        } else if (_playerStateMachine.State == PlayerState.Jumping && _playerMovable.Velocity.y <= Config.FallingVelocityThreshold) {
            _playerStateMachine.ChangeState(PlayerState.Falling);
        } else if (_playerStateMachine.State == PlayerState.Falling && IsGrounded) {
            if (_currentMoveInput == 0) {
                _playerStateMachine.ChangeState(PlayerState.Standing);
            } else {
                _playerStateMachine.ChangeState(PlayerState.Running);
            }
        }
    }
    
    private void HandleCurrentState() {
        if (new [] {PlayerState.Standing, PlayerState.Running, PlayerState.Jumping, PlayerState.Falling}.Contains(_playerStateMachine.State)) {
            if (!ControlEnabled) {
                return;
            }
            
            Direction = _currentMoveInput;

            if (_playerStateMachine.State != PlayerState.Standing) {
                _playerMovable.HorizontalMove(_currentMoveInput);
            }
        }
    }

    private void FixedUpdate() {
        HandleStateChanging();
        HandleCurrentState();
    }
    
    public void Initialize() {
        _animator = GameManager.Instance.player.Animator;

        _playerCore = GameManager.Instance.player.Core;
        _playerMovable = GameManager.Instance.player.Movable;
        
        _playerStateMachine = GameManager.Instance.player.StateMachine;
        InitStateTransitions(_playerStateMachine);
        _playerStateMachine.ResetStateInvoked += ResetState;
        
        GameManager.Instance.playerInputActions["Move"].performed += context => {
            var value = context.ReadValue<Vector2>();
            var absAngle = Mathf.Abs(Vector2.SignedAngle(Vector2.up, value));
            if (absAngle > Config.MoveAxisAngleOffset && absAngle < 180 - Config.MoveAxisAngleOffset) {
                _currentMoveInput = Math.Sign(value.x);
                _verticalAlignment = 0;
            } else {
                _currentMoveInput = 0;
                _verticalAlignment = absAngle < 90 ? 1 : -1;
            }
        };
        GameManager.Instance.playerInputActions["Jump"].started += HandleStartJumpActivation;
        GameManager.Instance.playerInputActions["Jump"].canceled += HandleEndJumpActivation;
        
        _playerMovable.SurfaceCollisionEntered += value => {
            if (value.gameObject.CompareTag(Tags.OneWay)) {
                var effector = value.gameObject.GetComponent<PlatformEffector2D>();
                _touchedOneWays.Add(effector);
            }
        };
        _playerMovable.SurfaceCollisionExited += value => {
            if (value.gameObject.CompareTag(Tags.OneWay)) {
                var effector = value.gameObject.GetComponent<PlatformEffector2D>();
                _touchedOneWays.Remove(effector);
            }
        };

        SceneManager.sceneLoaded += (scene, mode) => {
            _touchedOneWays.Clear();
        };

        _playerCore.Died += () => {
            Debug.Log("Player's character died");
            // TODO change state to dying and run animation
        };
    }
}