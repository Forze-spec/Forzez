using UnityEngine;

public static class Config {
    // частота вызова проверки isGrounded
    public const float DetectAreaRaycastRepeatRate = .2f;
    
    // длина луча при проверке isGrounded
    public const float IsGroundedRaycastLength = 0.5f;
    
    // маска физических слоев явных материальных препятствий (стен, дверей, полов, двигаемых объектов и т.д.)
    public static readonly int ObstacleLayerMask = LayerMask.GetMask(Layers.StaticObjects);
    
    // погрешность расстояния, используемая при его расчете
    public const float DistancePrecision = 0.1f;
    
    // погрешность сравнения цифр с плавающей точкой
    public const float FloatPrecision = 0.000001f;

    // угол между вектором ввода и вертикальной осью, позволяющий определить как отклонен стик (вертикально или горизонтально)
    public const float MoveAxisAngleOffset = 30f;

    public const float FallingVelocityThreshold = 1f;

    public const float OneWayFallingThrowTimeout = 0.25f;
}
