using System;
using UnityEngine;

public enum PlayerState {
    Standing, Running, Jumping, Falling, Dodging, Attacking
}

public class PlayerStateMachine : MonoBehaviour {
    
    private AutoStateMachine<PlayerState> _stateMachine;
    
    public event ValueChangedEvent<PlayerState> StateChanged;

    public event NotifyEvent ResetStateInvoked;

    public NotifyEvent ActionEvent;
    
    [Tooltip("Is input control enabled or not")]
    public bool controlEnabled = true;

    public void AddTransition(PlayerState from, PlayerState to, Action action = null) =>
        _stateMachine.AddTransition(from, to, action);

    public void AddAutoTransition(PlayerState toState, Func<float, bool> transitCondition) {
        _stateMachine.AddAutoTransition(toState, transitCondition);
    }
    
    public void AddAutoTransitions(PlayerState toState, Func<float, bool>[] transitCondition) {
        _stateMachine.AddAutoTransitions(toState, transitCondition);
    }

    public void AddStateHandler(PlayerState state, Action<float> stateHandler) {
        _stateMachine.AddStateHandler(state, stateHandler);
    }

    public PlayerState State => _stateMachine.CurrentState;

    public void ChangeState(PlayerState state) => _stateMachine.ChangeState(state);

    public void ResetState() {
        ResetStateInvoked?.Invoke();
    }

    public void Initialize() {
        _stateMachine = new AutoStateMachine<PlayerState>(PlayerState.Standing);
        GameManager.Instance.playerInputActions["Action"].performed += context => ActionEvent?.Invoke();

        _stateMachine.StateChanged += (oldValue, newValue) => {
            StateChanged?.Invoke(oldValue, newValue);
        };
    }
}
