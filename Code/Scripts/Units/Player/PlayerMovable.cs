using System;
using System.Collections;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

public class PlayerMovable : GroundMovable {
    
    [Tooltip("Horizontal running speed of player")]
    public float moveSpeed = 10f;

    [BoxGroup("Jump properties")]
    [Tooltip("Vertical jump speed of player")]
    public float jumpVerticalSpeed = 10f;
    
    [BoxGroup("Jump properties")]
    [Tooltip("Horizontal jump speed of player (increases jump distance in movement direction)")]
    public float jumpHorizontalSpeed = 1f;
    
    [BoxGroup("Jump properties")]
    [Tooltip("Minimal duration of jump (used for jump interruption)")]
    public float minJumpDuration = .3f;
    
    [BoxGroup("Jump properties")]
    [Tooltip("Gravity multiplier of jump interruption (used for jump interruption)")]
    public float stopJumpGravityMultiplier = 3f;
    
    // END INSPECTOR DECLARATION
    
    private float _startJumpTime;  // время старта прыжка
    private int _jumpDirection;  // направление объекта во время начала прыжка (используется для горизонтального ускорения прыжка)
    private bool _jumpInterrupted;  // был ли прерван текущий прыжок или нет

    public Vector2 Velocity {
        get => Rb.velocity;
        set => Rb.velocity = value;
    }

    public void HorizontalMove(int direction, float partMultiplier = 1f) {
        if (!ForwardMoveOffsetAngle.HasValue) return;
        
        var sign = Math.Sign(direction);
        if (IsGrounded) {
            var newVelocity = Quaternion.Euler(0, 0, ForwardMoveOffsetAngle.Value) * 
                new Vector2(sign * moveSpeed * partMultiplier, 0);
            var yVelocity = Mathf.Abs(newVelocity.y) > Mathf.Abs(Rb.velocity.y) ? newVelocity.y : Rb.velocity.y;
            Rb.velocity = new Vector2(newVelocity.x, yVelocity);
        } else {
            if (sign != _jumpDirection) {
                _jumpDirection = 0;
            }
                
            var newVelocity = new Vector2(sign * moveSpeed * partMultiplier + _jumpDirection * jumpHorizontalSpeed, Rb.velocity.y);
            Rb.velocity = newVelocity;
        }
    }
    
    public void SimpleHorizontalMove(int direction, float speed) {
        var sign = Math.Sign(direction);
        Rb.velocity = new Vector2(sign * speed, Rb.velocity.y);
    }

    public void StartJump(int direction) {
        if (!IsGrounded) return;
        _jumpDirection = direction;
        Rb.velocity = new Vector2(Rb.velocity.x, jumpVerticalSpeed);
        _startJumpTime = Time.time;
    }

    public void Push(Vector2 force) {
        Rb.AddForce(force, ForceMode2D.Impulse);
    }

    public IEnumerator EndJump() {
        if (Rb.velocity.y <= 0 || _jumpInterrupted) yield break;

        _jumpInterrupted = true;
        yield return new WaitUntil(() => Time.fixedTime > _startJumpTime + minJumpDuration);
        var originalGravityScale = Rb.gravityScale;
        Rb.gravityScale = originalGravityScale * stopJumpGravityMultiplier;
        yield return new WaitUntil(() => Rb.velocity.y <= 0);
        Rb.gravityScale = originalGravityScale;
        _jumpInterrupted = false;
    }
}