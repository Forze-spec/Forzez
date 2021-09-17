using System.Linq;
using NaughtyAttributes;
using UnityEngine;

public class Trampoline : MonoBehaviour {
    public Vector2 force;
    public float maxTopNormalAngle = 10;

    [Tooltip("Ignored unit tags")]
    [Tag]
    public string[] ignoreTags;

    private void OnCollisionEnter2D(Collision2D other) {
        if (ignoreTags.Contains(other.gameObject.tag)) {
            return;
        }

        var rb = other.gameObject.GetComponent<Rigidbody2D>();
        if (rb == null) {
            Debug.LogWarning("Trampoline contacted object without RigidBody2D component");
        }
        
        var collisionAngle = Vector2.SignedAngle(Vector2.down, other.GetContact(0).normal);
        if (Mathf.Abs(collisionAngle) <= maxTopNormalAngle) {
            rb.AddForce(force, ForceMode2D.Impulse);
            // TODO run animation
        }
    }
}