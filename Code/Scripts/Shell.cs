using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

enum ShellContactAction {
    Destroy,
    Bounce,
    PassThrough,
    Stick,
}

[RequireComponent(typeof(Rigidbody2D))]
public class Shell : MonoBehaviour {
    
    [Tooltip("Damage of shell hit")]
    public float damage;
    
    [Tooltip("Time to live of shell before interaction (value < 0 means endless)")]
    public float flyTTL;
    
    [Tooltip("Adjust or not shell rotation to velocity direction")]
    public bool adjustRotationToVelocity;
    
    [ShowIf("adjustRotationToVelocity")]
    [Tooltip("Angular speed of adjusting transform rotation to velocity rotation")]
    public float adjustRotationSpeed = 10f;
    
    [ShowIf("adjustRotationToVelocity")]
    [Tooltip("Adjusting rotation threshold angle to smooth small angles changing")]
    public float adjustRotationThreshold = 3f;
    
    [Tooltip("Object to spawn on shell destruction")]
    public GameObject destructiblePrefab;

    [BoxGroup("Obstacles interaction properties")]
    [Tooltip("Use or not custom gravity scale after bounce interaction")]
    public bool useBounceCustomGravity;
    
    [BoxGroup("Obstacles interaction properties")]
    [ShowIf("useBounceCustomGravity")]
    [Tooltip("Object to spawn on shell destruction")]
    public float gravityScaleAfterBounce;
    
    [BoxGroup("Obstacles interaction properties")]
    [Tooltip("Time to live after sticking to obstacle (value < 0 means endless)")]
    public float afterFlyTTL;
    
    [BoxGroup("Obstacles interaction properties")]
    [InfoBox("If shell are interacting with object which tag isn't represented " + 
             "in arrays then result interaction will be ShellContactAction.Destroy"
    )]
    [Tooltip("Tags to bounce off")]
    [Tag]
    public List<string> bounceTags = new List<string>();
    
    [BoxGroup("Obstacles interaction properties")]
    [Tooltip("Tags to stick to")]
    [Tag]
    public List<string> stickTags = new List<string>();
    
    [BoxGroup("Units interaction properties")]
    [InfoBox("If shell are interacting with object which tag isn't represented " + 
             "in arrays then result interaction will be ShellContactAction.PassThrough"
    )]
    [Tooltip("Tags to destroy")]
    [Tag]
    public List<string> destroyTags = new List<string>();
    
    [BoxGroup("Units interaction properties")]
    [Tooltip("Tags of targets to take damage")]
    [Tag]
    public List<string> targetTags = new List<string>();
    
    [BoxGroup("Linear movement properties")]
    [Tooltip("Speed on fly start")]
    public float startSpeed;

    [BoxGroup("Linear movement properties")]
    [Tooltip("Constant acceleration which applies to velocity direction")]
    public float movementAcceleration;

    [BoxGroup("Linear movement properties")]
    [Tooltip("Rotation angular speed of shell when use manual control")]
    public float controlRotationSpeed = 60f;

    [BoxGroup("Ballistic movement properties")]
    [Tooltip("Fly height factor for ballistic shell; " +
             "shot_height (height above highest point (start or end) = distance * shellFlyHeightFactor"
    )]
    public float shellFlyHeightFactor = 0.3f;

    // END INSPECTOR DECLARATION

    private Rigidbody2D _rb;
    private float _startTime;
    private bool _isFlying = true;

    private Vector2? _targetVelocityDirection;
    public Vector2? TargetVelocityDirection {
        get => _targetVelocityDirection;
        set {
            _targetTransform = null;
            _targetVelocityDirection = value;
        }
    }

    private Transform _targetTransform;
    public Transform TargetTransform {
        get => _targetTransform;
        set {
            _targetVelocityDirection = null;
            _targetTransform = value;
        }
    }

    public float GravityFactor => _rb.gravityScale * Physics2D.gravity.y;
    public Vector2 Velocity => _rb.velocity;

    private ShellContactAction GetUnitContactAction(string contactTag) {
        return destroyTags.Contains(contactTag) ? ShellContactAction.Destroy : ShellContactAction.PassThrough;
    }
    
    private ShellContactAction GetObstacleContactAction(string contactTag) {
        if (bounceTags.Contains(contactTag)) {
            return ShellContactAction.Bounce;
        }
        if (stickTags.Contains(contactTag)) {
            return ShellContactAction.Stick;
        }
        return ShellContactAction.Destroy;
    }
    
    private void Attach(Transform parent, Vector2 point) {
        transform.position = point;
        _rb.constraints = RigidbodyConstraints2D.FreezeAll;
        transform.parent = parent;
    }

    private void RotateToDirection(Vector2 direction) {
        var angle = Vector2.SignedAngle(transform.right, direction);
        _rb.angularVelocity = Mathf.Abs(angle) > adjustRotationThreshold 
            ? Mathf.Sign(angle) * adjustRotationSpeed 
            : angle / adjustRotationThreshold * adjustRotationSpeed;
    }
    
    private void HandleFly() {
        if (!_isFlying) {
            return;
        }
        
        if (flyTTL > 0 && Time.fixedTime > _startTime + flyTTL) {
            Destroy(gameObject);
        }

        var targetVelocityDirection = TargetTransform != null
            ? TargetTransform.position - transform.position
            : TargetVelocityDirection;

        if (targetVelocityDirection.HasValue) {
            var currentVelocityDirection = _rb.velocity.normalized;
            var angle = Vector2.SignedAngle(currentVelocityDirection, targetVelocityDirection.Value);
            _rb.velocity = Quaternion.Euler(
                0, 0, Mathf.Sign(angle) * Mathf.Min(controlRotationSpeed * Time.fixedDeltaTime, Mathf.Abs(angle))
            ) * _rb.velocity;
        }

        _rb.velocity += _rb.velocity.normalized * movementAcceleration * Time.fixedDeltaTime;
        
        if (adjustRotationToVelocity) {
            var directionToRotate = _rb.velocity.normalized;
            RotateToDirection(directionToRotate);
        }
    }

    private void Awake() {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start() {
        _startTime = Time.fixedTime;
    }

    private void FixedUpdate() {
        HandleFly();
    }
    
    private void OnTriggerEnter2D(Collider2D other) {
        if (targetTags.Contains(other.tag)) {
            var targetCore = other.GetComponent<UnitCore>();
            if (targetCore != null) {
                targetCore.TakeDamage(damage, _rb.velocity);
            }
        }

        var action = GetUnitContactAction(other.tag);
        if (action == ShellContactAction.Destroy) {
            if (destructiblePrefab) {
                GameObject destructible = Instantiate(destructiblePrefab); // создание объекта, хранящего эффект разрушения снаряда
                destructible.transform.position = transform.position;
                // TODO combine with destructible script
            }
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D other) {
        _isFlying = false;
        var action = GetObstacleContactAction(other.collider.tag);
        if (action == ShellContactAction.Stick) {
            ContactPoint2D contact = other.contacts[0];
            Attach(other.transform, contact.point);
            if (afterFlyTTL >= 0) {
                Destroy(gameObject, afterFlyTTL);
            }
        } else if (action == ShellContactAction.Destroy) {
            if (destructiblePrefab) {
                GameObject destructible = Instantiate(destructiblePrefab); // создание объекта, хранящего эффект разрушения снаряда
                destructible.transform.position = transform.position;
                // TODO combine with destructible script
            }
            Destroy(gameObject);
        } else if (action == ShellContactAction.Bounce) {
            if (useBounceCustomGravity) {
                _rb.gravityScale = gravityScaleAfterBounce;
                if (afterFlyTTL >= 0) {
                    Destroy(gameObject, afterFlyTTL);
                }
            }
        }
    }

    public void Throw(Vector2 velocity) {
        _rb.velocity = velocity;
        transform.rotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.right, velocity));
    }
    
    private float CalculateBallisticFlyDuration(float startHeight, float flyHeight, float gravity) {
        var maxHeight = startHeight < 0 ? flyHeight : startHeight + flyHeight; // maxHeight не может быть меньше jumpHeight

        var firstSqrt = Mathf.Sqrt(2 * gravity * flyHeight);
        var secondSqrt = Mathf.Sqrt(2 * gravity * maxHeight);
        
        var flyDuration1 = (firstSqrt - secondSqrt) / gravity;
        var flyDuration2 = (firstSqrt + secondSqrt) / gravity;
        return Mathf.Max(flyDuration1, flyDuration2);
    }

    private Vector2 CalculateBallisticFlyVelocity(float flyDuration, float flyHeight, float distance, float gravity) {
        var velocityX = distance / flyDuration;
        var velocityY = Mathf.Sqrt(2 * gravity * flyHeight);
        return new Vector2(velocityX, velocityY);
    }

    private float CalculateLinearFlyDuration(float startVelocity, float acceleration, float distance) {
        if (acceleration == 0) {
            if (startVelocity == 0) {
                throw new Exception("Start velocity is 0");
            }

            return distance / startVelocity;
        }

        var sqrt = Mathf.Sqrt(Mathf.Pow(startVelocity, 2) + 2 * acceleration * distance);
        var duration1 = (-startVelocity + sqrt) / acceleration;
        var duration2 = (-startVelocity - sqrt) / acceleration;
        return Mathf.Max(duration1, duration2);
    }

    public void Throw(Vector2 from, Vector2 to, Vector2 aheadSpeed) {
        if (_rb.gravityScale == 0) {
            var direction = to - from;
            if (aheadSpeed.magnitude != 0) {
                var distance = direction.magnitude;
                var flyDuration = CalculateLinearFlyDuration(startSpeed, movementAcceleration, distance);
                var newDirection = to + aheadSpeed * flyDuration - from;
                if (Math.Sign(newDirection.x) == Math.Sign(direction.x)) {
                    direction = newDirection;
                }
            }

            direction = direction.normalized;
            _rb.velocity = direction * startSpeed;
            _rb.SetRotation(Vector2.SignedAngle(Vector2.right, direction));
        } else {
            float horizontalDistance = to.x - from.x;
            float startHeight = from.y - to.y;
            var shellFlyHeight = Mathf.Abs(horizontalDistance) * shellFlyHeightFactor;
            var shellFlyDuration = CalculateBallisticFlyDuration(
                startHeight, shellFlyHeight, _rb.gravityScale * -Physics2D.gravity.y
            );

            if (aheadSpeed.magnitude != 0) {
                var newHorizontalDistance = horizontalDistance + shellFlyDuration * aheadSpeed.x;
                
                if (Math.Sign(newHorizontalDistance) == Math.Sign(horizontalDistance)) {
                    horizontalDistance = newHorizontalDistance;
                }
            }

            var shellVelocity = CalculateBallisticFlyVelocity(
                shellFlyDuration, shellFlyHeight, horizontalDistance, _rb.gravityScale * -Physics2D.gravity.y
            );
            
            _rb.velocity = shellVelocity;
            _rb.SetRotation(Vector2.SignedAngle(Vector2.right, shellVelocity));
        }
    }
}
