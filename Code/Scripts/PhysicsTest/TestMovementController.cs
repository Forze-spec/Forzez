using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestMovementController : GroundMovable {
    public PlayerInput playerInputRef;
    
    public float moveSpeed;
    public float jumpPower;
    
    private Vector2 _input;
    private float _movePower = 150;

    private void DoGroundedMovement(Vector2 moveVector) {
        Vector2 currentVelocity = Rb.velocity;
        if (currentVelocity.magnitude < moveSpeed) {
            Rb.AddForce(moveVector, ForceMode2D.Force);
        } else {
            Vector2 moveVectorProjection = Vector3.Project(moveVector, Rb.velocity);
            moveVector = moveVector - moveVectorProjection;
            // TODO спроецировать moveVector на currentVelocity
            // TODO вычесть из moveVector проекцию
            // TODO Rb.AddForce(разность moveVector и проекции, ForceMode2D.Force);
        }
        
        Rb.AddForce(moveVector, ForceMode2D.Force);

        // Vector2 velocityProjection = Vector3.Project(Rb.velocity, moveVector);
        // // TODO учесть направление вектора проекции (запрещаять только если направление совпадает)
        // // TODO в идеале занулять параллельную скорости часть, направленную только в сторону самой нормали
        // // TODO (в противоположную занулять не нужно)
        // // TODO то есть добавляться будет только перпендикулярная скорости составляющая вектора moveVector
        // Debug.Log(velocityProjection);
        // if (velocityProjection.magnitude < moveSpeed) {
        //     Rb.AddForce(moveVector, ForceMode2D.Force);
        // }
    }

    private void DoAirMovement(Vector2 moveVector) {
        
    }

    private void HandleMovement() {
        var xSign = Math.Sign(_input.x);

        Direction = xSign;
        
        if (xSign == 0 || !ForwardMoveOffsetAngle.HasValue) {
            return;
        }

        Vector2 moveVector = Quaternion.Euler(0, 0, ForwardMoveOffsetAngle.Value) * 
                          new Vector2(xSign * _movePower, 0);
        
        Vector2 currentVelocity = Rb.velocity;
        if (currentVelocity.magnitude < moveSpeed) {
            Rb.AddForce(moveVector, ForceMode2D.Force);
        } else {
            Vector2 moveVectorProjection = Vector3.Project(moveVector, Rb.velocity);
            moveVector = moveVector - moveVectorProjection;
            // TODO спроецировать moveVector на currentVelocity
            // TODO вычесть из moveVector проекцию
            // TODO Rb.AddForce(разность moveVector и проекции, ForceMode2D.Force);
        }
        
        Rb.AddForce(moveVector, ForceMode2D.Force);

        // if (IsGrounded) {
        //     DoGroundedMovement(moveVector);
        // } else {
        //     DoAirMovement(moveVector);
        // }
    }

    private void HandleJump() {
        if (!IsGrounded) {
            return;
        }

        Rb.AddForce(new Vector2(0, jumpPower), ForceMode2D.Impulse);
    }

    private void Awake() {
        Initialize();

        var actionMap = playerInputRef.actions.FindActionMap("Player", true);

        actionMap["Move"].performed += context => {
            _input = context.ReadValue<Vector2>();
        };

        actionMap["Jump"].started += context => HandleJump();
    }

    private void FixedUpdate() {
        HandleMovement();
    }
}