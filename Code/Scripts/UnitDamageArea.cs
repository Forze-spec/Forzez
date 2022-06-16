using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NaughtyAttributes;
using UnityEngine;

class UnitContactInfo {
    public UnitCore Core;
    public float NextAttackTime;
}

public class UnitDamageArea : MonoBehaviour {
    private const float HorizontalPushAngle = 5f;
    
    // INSPECTOR DECLARATION

    [BoxGroup("Attack properties")]
    [Tooltip("Melee damage")]
    public float damage = 10f;

    [BoxGroup("Attack properties")]
    [Tooltip("Time interval between taking damage to unit")]
    public float timeBetweenAttacks = .5f;

    [BoxGroup("Knockback properties")]
    [Tooltip("Force of knockback while attack")]
    public float knockbackForce = 30f;

    [BoxGroup("Knockback properties")]
    [Tooltip("Start point of attack direction (optional)")]
    [CanBeNull] public Transform directionPivot;
    
    [Tooltip("Ignored unit tags")]
    [Tag]
    public string[] ignoreTags;

    // INNER FIELDS
    
    private Dictionary<int, UnitContactInfo> _touchedUnitCores = new Dictionary<int, UnitContactInfo>();
    
    // INNER FUNCTIONS

    private void OnTriggerEnter2D(Collider2D other) {
        if (ignoreTags != null && ignoreTags.Contains(other.tag)) {
            return;
        }
        
        var id = other.gameObject.GetInstanceID();
        var unitCore = other.gameObject.GetComponent<UnitCore>();
        if (unitCore == null) {
            Debug.LogWarning("Damage area contacted object without UnitCore component");
            return;
        }

        _touchedUnitCores.Add(id, new UnitContactInfo {
            Core = unitCore,
            NextAttackTime = Time.fixedTime
        });
    }

    private void OnTriggerStay2D(Collider2D other) {
        var id = other.gameObject.GetInstanceID();
        var success = _touchedUnitCores.TryGetValue(id, out var contactInfo);
        if (!success) {
            throw new NullReferenceException("Can't access contact info while taking damage");
        }

        var currentTime = Time.fixedTime;
        if (currentTime < contactInfo.NextAttackTime) {
            return;
        }

        var startPoint = directionPivot != null ? directionPivot.position : transform.position;
        
        // ближний бой юнитов отталкивает только по горизонтали, используется небольшой угол для накидывания
        var xValue = Mathf.Sign(other.transform.position.x - startPoint.x);
        var yValue = Mathf.Tan(HorizontalPushAngle * Mathf.Deg2Rad);
        var damageDirection = new Vector2(xValue, yValue);
        
        contactInfo.Core.TakeDamage(damage, damageDirection, knockbackForce);
        
        _touchedUnitCores[id].NextAttackTime = currentTime + timeBetweenAttacks;
    }

    private void OnTriggerExit2D(Collider2D other) {
        var id = other.gameObject.GetInstanceID();
        _touchedUnitCores.Remove(id);
    }
}