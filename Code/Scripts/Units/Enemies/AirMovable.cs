using System;
using UnityEngine;

public class AirMovable : MonoBehaviour {
    protected Rigidbody2D Rb;    //объект rigidbody2d, чтоб не дергать его каждый раз отдельно
    public float moveSpeed;    //скорость передвижения
    
    private int _direction = 1;    //направление юнита (1 - право, -1 - лево)
    public int Direction
    {
        get => _direction;
        protected set {
            var signum = Math.Sign(value);
            if (signum * _direction >= 0) {    // Если произведение меньше нуля, то направление уже верное; 0 не меняет направления
                return;
            }
            
            transform.localScale = new Vector3(    //поворачиваем
                signum * Mathf.Abs(transform.localScale.x), 
                transform.localScale.y, 
                transform.localScale.z
            );
            _direction = signum;    //ставим направление
        }
    }

    protected virtual void Awake() {
        Rb = GetComponent<Rigidbody2D>();
    }

    public void Move(Vector2 direction, float partMultiplier = 1) {
        int signum = Math.Sign(direction.x);
        Direction = signum;
        Rb.velocity = direction.normalized * moveSpeed * partMultiplier;
    }

    public void StopMovement() {
        Rb.velocity = Vector3.zero;
    }
}
