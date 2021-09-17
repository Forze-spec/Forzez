using NaughtyAttributes;
using UnityEngine;

public enum StickyEnemyBounceMode {
    Normal,
    Mirrored
}

[RequireComponent(typeof(Rigidbody2D))]
public class StickyEnemy : MonoBehaviour {

    [Tooltip("Fly speed of sticky enemy (movement speed between fixed positions)")]
    public float jumpSpeed = 3f;

    [Tooltip("Pause duration between jumps")]
    public float pauseBetweenJumps = 3f;
    
    [Tooltip("Bounce mode of sticky enemy (normal - normal to the surface, mirrored - mirrored about the surface normal)")]
    public StickyEnemyBounceMode jumpMode = StickyEnemyBounceMode.Mirrored;

    private bool NormalBounceAngleOffsetEnabled => jumpMode == StickyEnemyBounceMode.Normal;
    [Tooltip("Additional offset angle used for normal bounce mode (it adds to normal angle)")]
    [ShowIf("NormalBounceAngleOffsetEnabled")]
    [MinValue(-90), MaxValue(90)]
    public float normalJumpAngleOffset = 0f;
    
    // END INSPECTOR DECLARATION
    
    private const float _minJumpDuration = .1f;
    
    private Rigidbody2D _rb;
    private Vector2 _jumpDirection;
    private bool _isJumping;
    private float _endJumpMoment;
    private float _startJumpTimeMoment;
    private Collision2D _lastCollision;

    private void Attach(Transform parent) {
        _rb.constraints = RigidbodyConstraints2D.FreezeAll;
        transform.parent = parent;
    }
    
    private void Detach() {
        transform.parent = null;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void Move(Vector2 direction) {
        _rb.velocity = direction * jumpSpeed;
    }

    private void Stop() {
        _rb.velocity = Vector2.zero;
    }
    
    private void StartFly() {
        Detach();
        transform.right = _jumpDirection;
        _isJumping = true;
        _startJumpTimeMoment = Time.fixedTime;
        // TODO change animation to fly
    }

    private void EndFly(Collision2D collision) {
        Stop();
        Attach(collision.transform);

        _isJumping = false;
        _endJumpMoment = Time.fixedTime;

        ContactPoint2D contact = collision.contacts[0];
        if (jumpMode == StickyEnemyBounceMode.Normal) {
            _jumpDirection = Quaternion.Euler(0, 0, normalJumpAngleOffset) * contact.normal;
        } else {
            _jumpDirection = Vector2.Reflect(_jumpDirection, contact.normal).normalized;
        }
        transform.right = -Vector2.Perpendicular(contact.normal);
        // TODO change animation to idle
    }

    private void HandleMovement() {
        if (_isJumping) {
            if (Time.fixedTime > _startJumpTimeMoment + _minJumpDuration && _lastCollision != null) {
                EndFly(_lastCollision);
            } else {
                Move(_jumpDirection);
            }
        } else {
            if (Time.fixedTime > _endJumpMoment + pauseBetweenJumps) {
                StartFly();
            }
        }
    }

    private void Awake() {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Start() {
        _endJumpMoment = Time.fixedTime;
        _jumpDirection = transform.up;
    }

    private void FixedUpdate() {
        HandleMovement();
    }

    private void OnCollisionEnter2D(Collision2D other) {
        _lastCollision = other;
    }

    private void OnCollisionStay2D(Collision2D other) {
        _lastCollision = other;
    }

    private void OnCollisionExit2D(Collision2D other) {
        _lastCollision = null;
    }
}
