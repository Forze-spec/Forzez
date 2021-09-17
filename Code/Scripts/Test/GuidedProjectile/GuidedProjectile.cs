using UnityEngine;
using System.Linq;
using System.Runtime.InteropServices;

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class GuidedProjectile : MonoBehaviour
{
    // TODO limit this value (mb between 0.01 and 50)
    public float rotationSpeed = 3f;
    public float returnSpeed = 3f;

    private Vector3 _controlVector;

    private float _startTime;

    protected Rigidbody2D myRigidbody;

    public float startSpeed = 20f;
    public float timeToLive = 1.5f;
    public int damage = 40;
    public string[] damagedUnitTags = { "Enemy" };

    protected int currentShootThroughNumber = 0;

    public int shootThroughNumber = 0;
    public string[] ignoredUnitTags = { "Player", "Shell" };


    private bool _controlRotate;
    private bool _controlReturn;

    //private bool _controlCollision;

    private Vector3 _vectorPlayer;



    private void Awake()
    {
        myRigidbody = GetComponent<Rigidbody2D>();
        _startTime = Time.time;
    }

    private void OnTriggerEnter2D(Collider2D hitInfo)
    {
        //HandleCollision(hitInfo);
        _controlRotate = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        //if (Input.GetKeyDown("q")){}

        myRigidbody.velocity = new Vector2(0.0f, 0.0f);
        myRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;

    }

    private void Update()
    {
        //HandleTTL();

        _controlVector.x = Input.GetAxisRaw("Horizontal");
        _controlVector.y = Input.GetAxisRaw("Vertical");

        //_controlCollision = true;

        if (Input.GetKeyDown("e"))
        {
            _controlRotate = true;
            _controlReturn = false;
        }

        if (Input.GetKeyDown("q"))
        {
            Return();
            _controlRotate = false;
        }

        if (_controlRotate)
        {
            if (transform.position != null)
            {
                // GameManager.Instance.player.Actions.isMotionEnabled = false;
            }
            else
            {
                // GameManager.Instance.player.Actions.isMotionEnabled = true;
            }
        }

    }

    private void Rotate(Vector3 direction, float multiplier = 1) //расчет угла для управляющего поворота 
    {
        if (_controlRotate)
        {
            multiplier = Mathf.Clamp(multiplier, 0, 1);

            var velocityValue = myRigidbody.velocity.magnitude;        
            var rotationAngle = Vector3.SignedAngle(myRigidbody.velocity, direction, Vector3.forward);
            var partedRotationAngle = rotationAngle * multiplier;
            var newVelocityDirection = (Quaternion.Euler(0, 0, partedRotationAngle) * myRigidbody.velocity).normalized;
            myRigidbody.velocity = newVelocityDirection * velocityValue;
        }
        
    }


    private void ReturnRotate(Vector3 direction, float multiplier = 1) //расчет угла при возвращении
    {
        if (_controlReturn)
        {
            multiplier = Mathf.Clamp(multiplier, 0, 1);

            var velocityValue = myRigidbody.velocity.magnitude;

            _vectorPlayer = (GameManager.Instance.player.Core.transform.position - transform.position).normalized;

            var rotationAngle = Vector3.SignedAngle(myRigidbody.velocity, direction, Vector3.forward);
            var partedRotationAngle = rotationAngle * multiplier;
            var newVelocityDirection = (Quaternion.Euler(0, 0, partedRotationAngle) * myRigidbody.velocity).normalized;
            myRigidbody.velocity = newVelocityDirection * velocityValue;
        }
       
    }

    private void Return() //возврат снаряда к игроку
    {
        _controlReturn = true;
        gameObject.GetComponent<CircleCollider2D>().enabled = false;
        StartMoving(_vectorPlayer);
    }

    private void FixedUpdate()
    {
        if (_controlRotate)
        {
            Rotate(_controlVector, rotationSpeed * Time.fixedDeltaTime);
        }

        if (_controlReturn)
        {
            ReturnRotate(_vectorPlayer, returnSpeed * Time.fixedDeltaTime);
        }
    }

    public void StartMoving(Vector3 direction)
    {
        var norm = direction.normalized;
        myRigidbody.velocity = norm * startSpeed;

        if (_controlRotate)
        {
            Rotate(norm);
        }
        
        if (_controlReturn)
        {
            ReturnRotate(norm);
        }
        
    }
    
    protected void HandleTTL()
    {
        if (Time.time - _startTime > timeToLive)
        {
            Destroy(gameObject);
        }
    }
    
    protected virtual void HandleCollision(Collider2D hitInfo)
    {
        var targetObject = hitInfo.gameObject;
        var targetTag = targetObject.tag;

        if (ignoredUnitTags.Contains(targetTag))
        {
            return;
        }

        if (damagedUnitTags.Contains(targetTag))
        {
            var targetUnit = targetObject.GetComponent<UnitCore>();
            if (targetUnit != null)
            {
                targetUnit.TakeDamage(damage);
            }

            if (currentShootThroughNumber >= shootThroughNumber)
            {
                Destroy(gameObject);
            }
            else
            {
                currentShootThroughNumber += 1;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
}