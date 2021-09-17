using System;
using UnityEngine;

public class RouteEnemy : MonoBehaviour {
    
    [Tooltip("Move speed of route enemy")]
    public float moveSpeed = 5f;
    
    [Tooltip("Obstacles and turning points detection distance")]
    public float detectDistance = 3f;
    
    [Tooltip("Required angle between surface normal and unit up vector to turn around")]
    public float obstaclesAngle = 80f;
    
    [Tooltip("Turning points of route enemy")]
    public Vector2[] turningPoints;
    
    // END INSPECTOR DECLARATION
    
    private Rigidbody2D _rb;
    private Transform _renderer;

    private int _direction = 1;    //направление юнита (1 - право, -1 - лево)
    private int Direction
    {
        get => _direction;
        set {
            var signum = Math.Sign(value);
            if (signum * _direction >= 0) {    // Если произведение меньше нуля, то направление уже верное; 0 не меняет направления
                return;
            }
            
            transform.localScale = new Vector3(    //поворачиваем
                signum * Mathf.Abs(transform.localScale.x), 
                transform.localScale.y, 
                transform.localScale.z
            );
            _renderer.localScale = transform.localScale;
            
            _direction = signum;    //ставим направление
        }
    }

    private void Move(int direction) {
        var moveDir = Mathf.Sign(direction) * transform.right; 
        
        Vector2 gravityVelocity = Vector3.Project(_rb.velocity, transform.up);
        Vector2 moveVelocity = moveDir * moveSpeed;
        _rb.velocity = gravityVelocity + moveVelocity;
    }

    private void HandleObstacles() {
        var raycastStart = transform.position;
        var raycastDirection = transform.right * Direction;
        
        RaycastHit2D hit = Physics2D.Raycast(
            raycastStart, raycastDirection, detectDistance, Config.ObstacleLayerMask
        );
        
        Debug.DrawRay(
            raycastStart, raycastDirection * detectDistance, Color.blue, .02f
        );

        if (hit) {
            var obstacleAngle = Vector2.SignedAngle(transform.up, hit.normal);
            if (Mathf.Abs(obstacleAngle) > obstaclesAngle) {
                Direction = -Direction;
            }
        }
    }

    private void HandleTurningPoints() {
        foreach (var point in turningPoints) {
            var distance = point - (Vector2)transform.position;
            if (distance.magnitude < detectDistance) {
                var absAngle = Mathf.Abs(Vector2.SignedAngle(_rb.velocity, distance));
                if (absAngle < 90) {
                    Direction = -Direction;
                }
            }
        }
    }

    private void SynchronizeRenderer() {
        _renderer.position = transform.position;

        var smoothRate = Mathf.Clamp(Mathf.Abs(_rb.angularVelocity / 1000), .1f, 1f);
        _renderer.rotation = Quaternion.Lerp(_renderer.rotation, transform.rotation, smoothRate);
    }

    private void Awake() {
        _rb = GetComponentInParent<Rigidbody2D>();
        if (!_rb) {
            throw new NullReferenceException("No RigidBody2D component on RouteEnemy");
        }
        
        var spriteRenderer = transform.parent.GetComponentInChildren<SpriteRenderer>();
        
        if (!spriteRenderer) {
            throw new NullReferenceException("No SpriteRenderer component on RouteEnemy");
        }

        _renderer = spriteRenderer.transform;
    }

    private void FixedUpdate() {
        HandleObstacles();
        HandleTurningPoints();
        Move(Direction);
        SynchronizeRenderer();
    }
}