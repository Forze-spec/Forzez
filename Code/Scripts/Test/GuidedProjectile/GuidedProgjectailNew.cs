using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum State { Fly, Stucked, ControlFly, Return, None }

public class ReturnedProjectile : MonoBehaviour
{
    private StateMachine<State> _stateMachine;
    private PlayerMovable _playerMovable;
    private Rigidbody2D _rb;
    private Vector2 _inputAxis;

    public float FlySpeed = 3f;
    public float ReturnSpeed = 3f;
    public float ReturnPrecision = 1f;

    private State CurState
    {
        get { return _stateMachine.CurrentState; }
    }

    private void Awake()
    {
        _stateMachine = new StateMachine<State>(State.None);
        _stateMachine.AddTransition(State.None, State.Fly, NoneToFlyEvent);
        _stateMachine.AddTransition(State.None, State.ControlFly, NoneToControlFlyEvent);
        _stateMachine.AddTransition(State.Fly, State.Stucked, FlyToStuckedEvent);
        _stateMachine.AddTransition(State.ControlFly, State.Stucked, ControlFlyToStuckedEvent);
        _stateMachine.AddTransition(State.Stucked, State.Return, StuckedToReturnEvent);
        _stateMachine.AddTransition(State.Return, State.None, ReturnToNoneEvent);

        _rb = GetComponent<Rigidbody2D>();
        _playerMovable = GameManager.Instance.player.Movable;

        GameManager.Instance.playerInputActions["Aim"].performed += HandleAim;
    }

    private void FlyToStuckedEvent()
    {
        GetComponent<Collider2D>().enabled = false;
        _rb.constraints = RigidbodyConstraints2D.FreezeAll; // frezee pozition
    }

    private void StuckedToReturnEvent()
    {
        var _vectorPlayer = (GameManager.Instance.player.Core.transform.position - transform.position).normalized;
        _rb.velocity = _vectorPlayer * ReturnSpeed;
        _rb.constraints = RigidbodyConstraints2D.None; // unfreeze pozition
    }

    private void ControlFlyToStuckedEvent()
    {
        // GameManager.Instance.player.Actions.isMotionEnabled = true;
        GetComponent<Collider2D>().enabled = false;
        _rb.constraints = RigidbodyConstraints2D.FreezeAll; // frezee pozition
    }

    private void NoneToFlyEvent()
    {
        _rb.velocity = new Vector2(_playerMovable.Direction, 0) * FlySpeed;
    }

    private void NoneToControlFlyEvent()
    {
        _rb.velocity = new Vector2(_playerMovable.Direction, 0) * FlySpeed;
    }

    private void ReturnToNoneEvent()
    {
        _rb.velocity = new Vector2(0, 0);
        Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (CurState == State.ControlFly)
        {
            _rb.velocity = _inputAxis.normalized * FlySpeed;
            return;
        }

        if (CurState == State.Return)
        {
            var dist = (_playerMovable.transform.position - transform.position).magnitude;
            if (dist < ReturnPrecision)
            {
                _stateMachine.ChangeState(State.None);
            }
            else
            {
                var direction = (GameManager.Instance.player.FightController.transform.position - transform.position).normalized;
                _rb.velocity = direction * ReturnSpeed;
            }
        }
    }

    private void HandleAim(InputAction.CallbackContext context)
    {
        if (CurState != State.ControlFly) return;

        var input = context.ReadValue<Vector2>();
        // TODO откидывать вектора длины меньше необходимой
        _inputAxis = input;
    }
}
