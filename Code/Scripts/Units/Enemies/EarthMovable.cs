using System;
using UnityEngine;

public abstract class EarthMovable : GroundMovable {
    
    [Tooltip("Horizontal move speed of unit")]
    public float moveSpeed;
    
    [Tooltip("Vertical jump speed of unit")]
    public float jumpVerticalSpeed;
    
    // END INSPECTOR DECLARATION

    protected void HorizontalMove(int direction, float partMultiplier = 1f) {
        var sign = Math.Sign(direction);
        // TODO not sure about it
        Direction = sign;
        
        if (!ForwardMoveOffsetAngle.HasValue) return;

        var newVelocity = Quaternion.Euler(0, 0, ForwardMoveOffsetAngle.Value) * 
                          new Vector2(sign * moveSpeed * partMultiplier, 0);
        var yVelocity = Mathf.Abs(newVelocity.y) > Mathf.Abs(Rb.velocity.y) ? newVelocity.y : Rb.velocity.y;
        Rb.velocity = new Vector2(newVelocity.x, yVelocity);
    }

    protected void SimpleHorizontalMove(int direction, float speed) {
        var sign = Math.Sign(direction);
        // TODO not sure about it
        Direction = sign;
        
        if (!ForwardMoveOffsetAngle.HasValue) return;
        
        var newVelocity = Quaternion.Euler(0, 0, ForwardMoveOffsetAngle.Value) * 
                          new Vector2(sign * speed, 0);
        var yVelocity = Mathf.Abs(newVelocity.y) > Mathf.Abs(Rb.velocity.y) ? newVelocity.y : Rb.velocity.y;
        Rb.velocity = new Vector2(newVelocity.x, yVelocity);
    }

    protected void StopHorizontalMove() {
        Rb.velocity = new Vector2(0, Rb.velocity.y);
    }

    protected virtual void Awake() {
        Initialize();
    }

    public void Jump() {
        if (!IsGrounded) return;
        Rb.velocity = new Vector2(Rb.velocity.x, jumpVerticalSpeed);
    }
}