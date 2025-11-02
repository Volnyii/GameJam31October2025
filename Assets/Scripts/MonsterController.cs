using UnityEngine;

/// <summary>
/// Контроллер монстра со SpriteRenderer
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MonsterController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float patrolRadius = 5f;
    public Vector2 centerPosition = Vector2.zero;
    public bool enableScreenWrap = true;
    [Tooltip("Закреплять Y координату на одной линии (если false, монстр может двигаться по своей полосе)")]
    public bool lockYPosition = false;
    [Tooltip("Y координата для фиксации (используется только если lockYPosition = true)")]
    public float fixedY = 0f;
    
    [Header("Animation")]
    public RuntimeAnimatorController[] animatorControllers;
    private int _currentAnimatorIndex = 0;
    
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Vector3 currentTarget;
    private bool isDead = false;
    private Camera mainCamera;
    private Vector3 originalScale = Vector3.one; // Сохраняем исходный масштаб из префаба
    
    /// <summary>
    /// Устанавливает исходный масштаб из префаба (для сохранения при отражении)
    /// </summary>
    public void SetOriginalScale(Vector3 scale)
    {
        originalScale = scale;
    }
    
    void Awake()
    {
        SetupMonsterComponents();
    }
    
    void Start()
    {
        mainCamera = Camera.main;
        
        // Сохраняем исходный масштаб из префаба (если он не был установлен ранее)
        if (originalScale == Vector3.one && transform.localScale != Vector3.one)
        {
            originalScale = transform.localScale;
        }
        
        // Если цель не установлена, устанавливаем её
        if (currentTarget == Vector3.zero)
        {
            SetRandomTarget();
        }
    }
    
    /// <summary>
    /// Автоматически настраивает компоненты монстра
    /// </summary>
    [ContextMenu("Setup Monster Components")]
    public void SetupMonsterComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        spriteRenderer.sortingOrder = 4;
        
        // Animator (опционально)
        animator = GetComponent<Animator>();
        // Не создаем автоматически, только если уже есть
        
        // Применяем аниматор контроллер
        UpdateAnimator();
        
        // Создаем спрайт только если он не назначен вручную
        if (spriteRenderer.sprite == null)
        {
            CreateDefaultSprite();
        }
        
        // Добавляем компонент для обработки столкновений с крюком
        MonsterHitbox hitbox = GetComponent<MonsterHitbox>();
        if (hitbox == null)
        {
            hitbox = gameObject.AddComponent<MonsterHitbox>();
        }
    }
    
    void Update()
    {
        if (isDead) return;
        
        // Убеждаемся что монстр на фиксированной Y позиции (только если включено)
        if (lockYPosition)
        {
            MaintainFixedYPosition();
        }
        
        MoveTowardsTarget();
        
        // Применяем обтекание экрана
        if (enableScreenWrap)
        {
            WrapAroundScreen();
        }
    }
    
    /// <summary>
    /// Поддерживает позицию монстра на фиксированной Y координате (только если lockYPosition = true)
    /// </summary>
    void MaintainFixedYPosition()
    {
        Vector3 pos = transform.position;
        pos.y = fixedY;
        transform.position = pos;
    }
    
    void MoveTowardsTarget()
    {
        if (spriteRenderer == null) return;
        
        Vector3 currentPos = transform.position;
        Vector3 targetPos = currentTarget;
        
        // Двигаемся к цели с учетом скорости
        // Двигаемся по X к цели
        currentPos.x = Mathf.MoveTowards(currentPos.x, targetPos.x, moveSpeed * Time.deltaTime);
        
        // Двигаемся по Y к цели (только если Y не заблокирован)
        if (!lockYPosition)
        {
            currentPos.y = Mathf.MoveTowards(currentPos.y, targetPos.y, moveSpeed * Time.deltaTime);
            
            // Ограничиваем отклонение по Y от центра полосы (±0.5 единицы от centerPosition.y)
            if (centerPosition.y != 0)
            {
                float maxDeviation = 0.5f;
                float minY = centerPosition.y - maxDeviation;
                float maxY = centerPosition.y + maxDeviation;
                currentPos.y = Mathf.Clamp(currentPos.y, minY, maxY);
            }
        }
        else
        {
            // Если Y заблокирован, используем фиксированную позицию
            currentPos.y = fixedY;
        }
        
        transform.position = currentPos;
        
        // Поворачиваем спрайт в сторону движения (сохраняем масштаб из префаба)
        float currentX = transform.position.x;
        float targetX = currentTarget.x;
        
        // Сохраняем исходный масштаб при первом использовании
        if (originalScale == Vector3.one && transform.localScale != Vector3.one && transform.localScale != new Vector3(-1, 1, 1))
        {
            originalScale = transform.localScale;
        }
        
        if (currentX < targetX)
        {
            // Смотрим вправо - используем исходный масштаб
            transform.localScale = originalScale;
        }
        else if (currentX > targetX)
        {
            // Смотрим влево - отражаем по X, сохраняя Y и Z
            transform.localScale = new Vector3(-originalScale.x, originalScale.y, originalScale.z);
        }
        
        // Если достигли цели или очень близко, выбираем новую
        float distanceX = Mathf.Abs(transform.position.x - currentTarget.x);
        float distanceY = Mathf.Abs(transform.position.y - currentTarget.y);
        float totalDistance = Mathf.Sqrt(distanceX * distanceX + distanceY * distanceY);
        
        if (totalDistance < 0.3f) // Порог в мировых единицах (общее расстояние)
        {
            SetRandomTarget();
        }
    }
    
    void WrapAroundScreen()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }
        
        // Получаем границы экрана в мировых координатах
        float screenHeight = 2f * mainCamera.orthographicSize;
        float screenWidth = screenHeight * mainCamera.aspect;
        
        float leftBound = mainCamera.transform.position.x - screenWidth / 2f;
        float rightBound = mainCamera.transform.position.x + screenWidth / 2f;
        
        Vector3 pos = transform.position;
        
        // Если монстр вышел за левый край - появляется справа (на той же Y позиции)
        if (pos.x < leftBound)
        {
            pos.x = rightBound - 0.5f; // Немного отступ от края
            // Сохраняем Y позицию (не сбрасываем на bottomY)
            transform.position = pos;
            SetRandomTarget();
        }
        // Если монстр вышел за правый край - появляется слева (на той же Y позиции)
        else if (pos.x > rightBound)
        {
            pos.x = leftBound + 0.5f; // Немного отступ от края
            // Сохраняем Y позицию (не сбрасываем на bottomY)
            transform.position = pos;
            SetRandomTarget();
        }
    }
    
    void SetRandomTarget()
    {
        float targetY;
        float randomX;
        
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // Если камера не найдена, используем простую позицию относительно центра патрулирования
                randomX = centerPosition.x != 0 ? centerPosition.x + Random.Range(-patrolRadius, patrolRadius) : transform.position.x + Random.Range(-5f, 5f);
                targetY = lockYPosition ? fixedY : transform.position.y;
                if (!lockYPosition && centerPosition.y != 0)
                {
                    targetY = centerPosition.y + Random.Range(-0.3f, 0.3f);
                }
                currentTarget = new Vector3(randomX, targetY, 0);
                return;
            }
        }
        
        // Вычисляем границы камеры
        float screenHeight = 2f * mainCamera.orthographicSize;
        float screenWidth = screenHeight * mainCamera.aspect;
        float leftBound = mainCamera.transform.position.x - screenWidth / 2f;
        float rightBound = mainCamera.transform.position.x + screenWidth / 2f;
        
        // Используем текущую Y позицию монстра (для патрулирования на своей полосе)
        // Если Y заблокирован, используем фиксированную позицию
        targetY = lockYPosition ? fixedY : transform.position.y;
        
        // Если Y не заблокирован, добавляем небольшую вариацию (±0.3 единицы от центра патрулирования)
        // Используем centerPosition.y как центр полосы, если он установлен
        if (!lockYPosition)
        {
            float laneCenterY = centerPosition.y != 0 ? centerPosition.y : transform.position.y;
            targetY = laneCenterY + Random.Range(-0.3f, 0.3f);
        }
        
        // Выбираем случайную точку по горизонтали на текущей полосе монстра
        randomX = Random.Range(leftBound + 1f, rightBound - 1f);
        currentTarget = new Vector3(randomX, targetY, 0);
    }
    
    public void SetAnimatorController(int index)
    {
        if (animatorControllers != null && index >= 0 && index < animatorControllers.Length)
        {
            _currentAnimatorIndex = index;
            UpdateAnimator();
        }
    }
    
    void UpdateAnimator()
    {
        if (animator != null && animatorControllers != null && animatorControllers.Length > 0)
        {
            int indexToUse = Mathf.Clamp(_currentAnimatorIndex, 0, animatorControllers.Length - 1);
            if (animatorControllers[indexToUse] != null)
            {
                animator.runtimeAnimatorController = animatorControllers[indexToUse];
            }
        }
    }
    
    public void Die()
    {
        if (isDead) return;
        
        isDead = true;
        Debug.Log($"Монстр {gameObject.name} умирает от попадания крюка");
        
        // Уведомляем UI Manager о смерти монстра
        if (CastleUIManager.Instance != null)
        {
            CastleUIManager.Instance.OnMonsterKilled();
        }
        
        StartCoroutine(DeathAnimation());
    }
    
    System.Collections.IEnumerator DeathAnimation()
    {
        float duration = 0.3f;
        Vector3 startScale = transform.localScale;
        Color startColor = spriteRenderer.color;
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            spriteRenderer.color = Color.Lerp(startColor, Color.clear, t);
            
            yield return null;
        }
        
        // Возвращаем в пул или уничтожаем
        if (MonsterSpawner.Instance != null)
        {
            MonsterSpawner.Instance.ReturnMonster(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void CreateDefaultSprite()
    {
        Texture2D texture = new Texture2D(64, 64);
        Color[] pixels = new Color[64 * 64];
        
        Color monsterColor = new Color(0.8f, 0.2f, 0.2f);
        
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float distFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                
                if (distFromCenter < 25f)
                {
                    pixels[y * 64 + x] = monsterColor;
                }
                else if (distFromCenter < 28f)
                {
                    pixels[y * 64 + x] = Color.black;
                }
                else
                {
                    pixels[y * 64 + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;
    }
    
    public bool IsDead => isDead;
    public int currentAnimatorIndex => _currentAnimatorIndex;
    
    public Vector3 Position
    {
        get { return transform.position; }
        set { transform.position = value; }
    }
}
