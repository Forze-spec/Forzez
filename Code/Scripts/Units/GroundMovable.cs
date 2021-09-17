using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

class ContactsCacheElement {
    public List<ContactPoint2D> groundContacts;
    public List<ContactPoint2D> restContacts;
    public int groundContactsCount;
}

class ContactsCache {
    public Dictionary<int, ContactsCacheElement> perCollider;
    public int totalGroundContactsCount;

    public ContactsCache() {
        perCollider = new Dictionary<int, ContactsCacheElement>();
        totalGroundContactsCount = 0;
    }
}

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class GroundMovable : MonoBehaviour {
    
    [Tooltip("Maximum angle of the traversed surface (min angle between x axis and surface tangent)")]
    public float maxGroundAngle = 60;
    
    // END INSPECTOR DECLARATION
    
    private ContactsCache _contactsCache = new ContactsCache();
    
    protected Collider2D Collider;   // коллайдер объекта
    protected Rigidbody2D Rb;  // rigidbody объекта
    protected float? ForwardMoveOffsetAngle = 0;  // текущий угол наклона вектора движения (используется для перемещения под уклоном)
    
    public event ValueUpdatedEvent<bool> IsGroundedChanged;
    private bool _isGrounded;
    public bool IsGrounded {  // Стоит ли объект на поверхности или находится в воздухе
        get => _isGrounded;
        set {
            var oldValue = _isGrounded;
            _isGrounded = value;
            if (oldValue != _isGrounded) {
                IsGroundedChanged?.Invoke(_isGrounded);
            }
        }
    }
    
    public event ValueUpdatedEvent<int> DirectionChanged; 
    public int Direction {  // Направление юнита (1 - право, -1 - лево)
        get => Math.Sign(transform.localScale.x);
        set {
            var currentSignum = Math.Sign(transform.localScale.x);
            var targetSignum = Math.Sign(value);
            if (currentSignum * targetSignum >= 0) {  // Если произведение меньше нуля, то направление уже верное; 0 не меняет направления
                return;
            }
            
            transform.localScale = new Vector3(  // Поворачиваем
                targetSignum * Mathf.Abs(transform.localScale.x), 
                transform.localScale.y, 
                transform.localScale.z
            );
            RecalculateForwardAngle();
            DirectionChanged?.Invoke(Math.Sign(transform.localScale.x));
        }
    }

    public bool CanMoveForward => ForwardMoveOffsetAngle.HasValue;

    public event ValueUpdatedEvent<Collision2D> SurfaceCollisionEntered;
    public event ValueUpdatedEvent<Collision2D> SurfaceCollisionExited;

    // Функция подсчета угла движения вперед (выравнивает скорость при движении по наклонным поверхностям)
    private void RecalculateForwardAngle() {
        // проверяем, может ли юнит идти вперед (есть ли впереди препятсвие)
        // аггрегируем контакты (не с полом) для всех коллайдеров и проверяем их углы нормалей
        var stopForwardMove = _contactsCache.perCollider.Values
            .Select(elem => elem.restContacts)
            .Aggregate(
                new List<ContactPoint2D>(), 
                (total, elem) => total.Concat(elem).ToList()
            ).Where(elem => {
                var upAngle = Vector2.SignedAngle(Vector2.up, elem.normal);
                var absUpAngle = Mathf.Abs(upAngle);
                return Math.Sign(upAngle) == Direction && absUpAngle < maxGroundAngle * 2;
            }).Any();
        
        // если впереди препятствие - угол движения считать не нужно
        if (stopForwardMove) {
            ForwardMoveOffsetAngle = null;
            return;
        }
        
        var forwardMoveAngle = 0f;
        // если есть контакты с полом
        if (_contactsCache.totalGroundContactsCount > 0) {
            ContactPoint2D forwardContact;
            // аггрегируем все контакты с полом всех коллайдеров и в зависимости от
            // направления юнита берем либо самый левый либо самый правый
            if (Direction > 0) {
                forwardContact = _contactsCache.perCollider.Values
                    .Where(elem => elem.groundContacts.Count > 0)
                    .Select(elem => elem.groundContacts.Last())
                    .OrderBy(elem => elem.point.x).Last();
            } else {
                forwardContact = _contactsCache.perCollider.Values
                    .Where(elem => elem.groundContacts.Count > 0)
                    .Select(elem => elem.groundContacts.First())
                    .OrderBy(elem => elem.point.x).First();
            }

            forwardMoveAngle = Vector2.SignedAngle(Vector2.up, forwardContact.normal);
        }
        
        ForwardMoveOffsetAngle = forwardMoveAngle;
    }

    // Функция, предотвращающая ненужные переходы в состояние падения
    // Кидает raycast пока персонаж не касается с полом, но находится близко к нему
    private IEnumerator CheckGroundWithRaycast() {
        while (_contactsCache.totalGroundContactsCount == 0 && IsGrounded) {
            var bounds = Collider.bounds;
            var raycastStartPoint = bounds.center;
            var raycastLength = bounds.extents.y + Config.IsGroundedRaycastLength;
        
            var hit = Physics2D.Raycast(
                raycastStartPoint, Vector2.down, raycastLength, Config.ObstacleLayerMask
            );
            
            Debug.DrawRay(raycastStartPoint, Vector2.down * raycastLength, Color.yellow, 0.02f);

            if (hit.collider == null) {
                IsGrounded = false;
                break;
            }
            
            yield return new WaitForFixedUpdate();
        }
    }
    
    public void Initialize() {
        Collider = GetComponent<Collider2D>();
        if (!Collider) {
            throw new NullReferenceException("No Collider2D component on player object");
        }
        
        Rb = GetComponent<Rigidbody2D>();
        if (!Rb) {
            throw new NullReferenceException("No Rigidbody2D component on player object");
        }
        
        // очищаем кэш контактов при смене сцены - старые контакты бесполезны
        SceneManager.sceneLoaded += (scene, mode) => {
            _contactsCache.perCollider.Clear();
            _contactsCache.totalGroundContactsCount = 0;
        };
    }
    
    private void OnCollisionEnter2D(Collision2D other) {
        var colliderId = other.collider.GetInstanceID();
        // в начале коллизии добавляем пустой элемент в кэш
        _contactsCache.perCollider.Add(colliderId, new ContactsCacheElement {
            groundContacts = new List<ContactPoint2D>(),
            restContacts = new List<ContactPoint2D>(),
            groundContactsCount = 0
        });
        SurfaceCollisionEntered?.Invoke(other);
    }
    
    private void OnCollisionStay2D(Collision2D other) {
        // разделяем контакты коллизии на пол и остальное
        var splitted = CustomMappers.SplitOn(other.contacts,
            elem => Mathf.Abs(Vector2.SignedAngle(Vector2.up, elem.normal)) <= maxGroundAngle
        );

        var colliderId = other.collider.GetInstanceID();
        var groundContacts = splitted.Item1.OrderBy(elem => elem.point.x).ToList();
        var restContacts = splitted.Item2.ToList();
        var newGroundContactsCount = groundContacts.Count;

        // получаем кол-во старых контактов с полом данного коллайдера
        // чтобы обновить общий счетчик контаков с полом (totalGroundContactsCount)
        var success = _contactsCache.perCollider.TryGetValue(colliderId, out var oldCache);
        var oldGroundContactsCount = success ? oldCache.groundContactsCount : 0;

        // обновляем кэш и общий счетчик
        var elemRef = _contactsCache.perCollider[colliderId];
        elemRef.groundContacts = groundContacts;
        elemRef.restContacts = restContacts;
        elemRef.groundContactsCount = newGroundContactsCount;
        _contactsCache.totalGroundContactsCount += newGroundContactsCount - oldGroundContactsCount;

        // пересчитываем угол движения вперед
        RecalculateForwardAngle();
        
        IsGrounded = _contactsCache.totalGroundContactsCount > 0;
        
        foreach (var elem in groundContacts) {
            Debug.DrawRay(elem.point, elem.normal, Color.red, 0.02f);
        }
    }

    private void OnCollisionExit2D(Collision2D other) {
        // в конце коллизии удаляем кэш и уменьшаем общее кол-во контактов
        // с полом на кол-во контактов с полом данного коллайдера
        var colliderId = other.collider.GetInstanceID();
        var colliderGroundContactsCount = _contactsCache.perCollider[colliderId].groundContactsCount;
        _contactsCache.perCollider.Remove(colliderId);
        _contactsCache.totalGroundContactsCount -= colliderGroundContactsCount;

        // Если в момент конца коллизии нет прямого контакта с полом - проверяем пол через райкаст
        if (_contactsCache.totalGroundContactsCount == 0) {
            StartCoroutine(CheckGroundWithRaycast());
            ForwardMoveOffsetAngle = 0;
        }
        
        SurfaceCollisionExited?.Invoke(other);
    }
}