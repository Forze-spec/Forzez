using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public delegate void ValueChangedEvent<in T>(T oldValue, T newValue);
public delegate void ValueUpdatedEvent<in T>(T newValue);
public delegate void NotifyEvent();


public sealed class CustomMathf {
    public static float SignSymmetricClamp(float value, float min, float max) {
        return value > 0 ? Mathf.Clamp(value, min, max) : Mathf.Clamp(value, -max, -min);
    }
    
    public static float DeadZone(float value, float deadValue, Func<float, bool> deadArea) {
        return deadArea(value) ? deadValue : value;
    }
}

public sealed class CustomMappers {
    public static Tuple<IEnumerable<T>, IEnumerable<T>> SplitOn<T>(IEnumerable<T> source, Func<T, bool> predicate) {
        var positiveList = new List<T>();
        var negativeList = new List<T>();
        foreach (var elem in source) {
            if (predicate(elem)) {
                positiveList.Add(elem);
            } else {
                negativeList.Add(elem);
            }
        }
        
        return new Tuple<IEnumerable<T>, IEnumerable<T>>(positiveList, negativeList);
    }
}