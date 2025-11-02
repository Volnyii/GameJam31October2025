using UnityEngine;

/// <summary>
/// Компонент для обработки столкновений монстра с крюком
/// Содержит BoxCollider2D для определения размеров и обработки столкновений
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MonsterHitbox : MonoBehaviour
{
    private BoxCollider2D boxCollider;
    private MonsterController monsterController;
    
    void Awake()
    {
        SetupComponents();
    }
    
    void SetupComponents()
    {
        // Добавляем Rigidbody2D для работы коллайдера (kinematic, чтобы не влиять на физику)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.isKinematic = true; // Kinematic чтобы не влиять на движение монстра
        rb.gravityScale = 0f; // Отключаем гравитацию
        
        // Получаем или создаем BoxCollider2D
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // Настраиваем как триггер
        boxCollider.isTrigger = true;
        
        // Получаем MonsterController
        monsterController = GetComponent<MonsterController>();
        if (monsterController == null)
        {
            monsterController = GetComponentInParent<MonsterController>();
        }
        
        // Автоматически устанавливаем размер коллайдера по спрайту
        SetupColliderSize();
    }
    
    /// <summary>
    /// Настраивает размер коллайдера по спрайту монстра (уменьшенный размер)
    /// </summary>
    void SetupColliderSize()
    {
        if (boxCollider == null) return;
        
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInParent<SpriteRenderer>();
        }
        
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // Используем уменьшенный размер спрайта (70% от исходного)
            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            boxCollider.size = spriteSize * 0.7f;
        }
        else
        {
            // Размер по умолчанию (уменьшенный)
            boxCollider.size = new Vector2(0.7f, 0.7f);
        }
    }
    
    /// <summary>
    /// Вызывается при столкновении с другим триггером (крюком)
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        // Проверяем что это крюк
        HookController hookController = other.GetComponent<HookController>();
        if (hookController != null || 
            other.CompareTag("Hook") ||
            other.name.Contains("Hook"))
        {
            // Проверяем что крюк еще не убил другого монстра
            if (hookController != null && hookController.HasHitMonster())
            {
                return; // Крюк уже убил монстра
            }
            
            // Вызываем метод смерти у монстра
            if (monsterController != null && !monsterController.IsDead)
            {
                Debug.Log($"Крюк попал в монстра {gameObject.name} через коллайдер");
                monsterController.Die();
                
                // Отмечаем что крюк попал в монстра
                if (hookController != null)
                {
                    hookController.MarkMonsterHit();
                }
            }
        }
    }
    
    /// <summary>
    /// Обновляет размер коллайдера (можно вызвать вручную если спрайт изменился)
    /// </summary>
    public void UpdateColliderSize()
    {
        SetupColliderSize();
    }
    
    /// <summary>
    /// Получает размеры коллайдера
    /// </summary>
    public Vector2 GetSize()
    {
        if (boxCollider != null)
        {
            return boxCollider.size;
        }
        return Vector2.zero;
    }
    
    /// <summary>
    /// Получает границы коллайдера в мировых координатах
    /// </summary>
    public Bounds GetBounds()
    {
        if (boxCollider != null)
        {
            return boxCollider.bounds;
        }
        return new Bounds(transform.position, Vector3.zero);
    }
}

