using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AutoStateMachine<T> : StateMachine<T> {
    private Dictionary<T, Action<float>> _handlers;
    private Dictionary<T, Func<float, bool>[]> _autoTransitions;
    
    private void HandleAutoTransitions(float currentTime) {
        foreach (var tuple in _autoTransitions) {
            var state = tuple.Key;
            var conditions = tuple.Value;
            
            if (!conditions.Any(elem => elem(currentTime))) {
                continue;
            }
            
            ChangeState(state);
            return;
        }
    }

    private void HandleCurrentState(float currentTime) {
        var handlerExists = _handlers.TryGetValue(CurrentState, out var handler);
        if (handlerExists) {
            handler(currentTime);
        }
    }
    
    public AutoStateMachine(T initState, bool showLogs = false) : base(initState, showLogs) {
        _handlers = new Dictionary<T, Action<float>>();
        _autoTransitions = new Dictionary<T, Func<float, bool>[]>();
    }

    public void AddAutoTransition(T toState, Func<float, bool> transitCondition) {
        _autoTransitions.Add(toState, new []{transitCondition});
    }
    
    public void AddAutoTransitions(T toState, Func<float, bool>[] transitConditions) {
        _autoTransitions.Add(toState, transitConditions);
    }

    public void AddStateHandler(T state, Action<float> stateHandler) {
        _handlers.Add(state, stateHandler);
    }

    public void DoIteration(float currentTime) {
        HandleAutoTransitions(currentTime);
        HandleCurrentState(currentTime);
    }
}