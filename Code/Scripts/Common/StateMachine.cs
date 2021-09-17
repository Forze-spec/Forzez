using System;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine<T> {
    private T _currentState;
    private Dictionary<T, Dictionary<T, Action>> _transitions;
    private bool _showLogs;
    
    public event ValueChangedEvent<T> StateChanged;

    public StateMachine(T initState, bool showLogs = false) {
        _transitions = new Dictionary<T, Dictionary<T, Action>>();
        _currentState = initState;
        _showLogs = showLogs;
    }

    public void AddTransition(T from, T to, Action action = null) {
        Dictionary<T, Action> innerDict;
        bool res = _transitions.TryGetValue(from, out innerDict);
        if (res) {
            innerDict[to] = action;
        }
        else {
            innerDict = new Dictionary<T, Action>();
            innerDict.Add(to, action);
            _transitions.Add(from, innerDict);
        }
    }

    public void ChangeState(T newState) {
        Dictionary<T, Action> innerDict;
        bool res = _transitions.TryGetValue(_currentState, out innerDict);
        if (!res) {
            throw new Exception($"Unknown transition from {_currentState} to {newState}");
        }

        Action action;
        res = innerDict.TryGetValue(newState, out action);
        if (!res) {
            throw new Exception($"Unknown transition from {_currentState} to {newState}");
        }

        if (action != null) {
            action();
        }

        if (_showLogs) {
            Debug.Log($"Changing state from {_currentState} to {newState}");
        }

        var oldState = _currentState;
        _currentState = newState;
        StateChanged?.Invoke(oldState, _currentState);
    }

    public T CurrentState => _currentState;
}