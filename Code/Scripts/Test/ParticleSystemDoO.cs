using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystemDoO : MonoBehaviour
{
    public float VelocityLinearModifier = 2f;
    private ParticleSystem particle;
    private ParticleSystem.VelocityOverLifetimeModule velPs;
    private Vector2 directionOfImpact;

    void Awake(){
        particle = GetComponent<ParticleSystem>();
    }
    public void RunPS(Vector2? damageDirection) {
        Vector2 directionOfImpact = damageDirection.HasValue
            ? damageDirection.Value.normalized
             : new Vector2(0, 0);

        velPs = particle.velocityOverLifetime;
        Debug.Log(directionOfImpact);
        var directionX = directionOfImpact.x * VelocityLinearModifier;
        var directionY = directionOfImpact.y;
        velPs.x = directionX;
        velPs.y = directionY;
    }
}
