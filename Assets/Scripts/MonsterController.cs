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
    }
    
    void Update()
    {
        if (isDead) return;
        
        // Убеждаемся что монстр на нижней границе камеры
        MaintainBottomPosition();
        
        MoveTowardsTarget();
        
        // Применяем обтекание экрана
        if (enableScreenWrap)
        {
            WrapAroundScreen();
        }
    }
    
    /// <summary>
    /// Поддерживает позицию монстра на нижней границе камеры
    /// </summary>
    void MaintainBottomPosition()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }
        
        // Вычисляем нижнюю границу камеры
        float screenHeight = 2f * mainCamera.orthographicSize;
        float bottomY = mainCamera.transform.position.y - screenHeight / 2f;
        
        // Устанавливаем Y на нижнюю границу, сохраняя X
        Vector3 pos = transform.position;
        pos.y = bottomY;
        transform.position = pos;
    }
    
    void MoveTowardsTarget()
    {
        if (spriteRenderer == null) return;
        
        // Движение только по горизонтали, Y всегда на нижней границе камеры
        Vector3 currentPos = transform.position;
        Vector3 targetPos = currentTarget;
        
        // Двигаемся только по X, Y остается на нижней границе
        currentPos.x = Mathf.MoveTowards(currentPos.x, targetPos.x, moveSpeed * Time.deltaTime);
        
        // Поддерживаем Y на нижней границе камеры
        if (mainCamera != null)
        {
            float screenHeight = 2f * mainCamera.orthographicSize;
            currentPos.y = mainCamera.transform.position.y - screenHeight / 2f;
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
        float distanceToTarget = Mathf.Abs(transform.position.x - currentTarget.x);
        if (distanceToTarget < 0.2f)
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
        float bottomY = mainCamera.transform.position.y - screenHeight / 2f;
        
        Vector3 pos = transform.position;
        
        // Если монстр вышел за левый край - появляется справа
        if (pos.x < leftBound)
        {
            pos.x = rightBound - 0.5f; // Немного отступ от края
            pos.y = bottomY; // Поддерживаем нижнюю границу
            transform.position = pos;
            SetRandomTarget();
        }
        // Если монстр вышел за правый край - появляется слева
        else if (pos.x > rightBound)
        {
            pos.x = leftBound + 0.5f; // Немного отступ от края
            pos.y = bottomY; // Поддерживаем нижнюю границу
            transform.position = pos;
            SetRandomTarget();
        }
    }
    
    void SetRandomTarget()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                // Если камера не найдена, используем простую позицию
                currentTarget = new Vector3(transform.position.x + Random.Range(-5f, 5f), transform.position.y, 0);
                return;
            }
        }
        
        // Вычисляем границы камеры
        float screenHeight = 2f * mainCamera.orthographicSize;
        float screenWidth = screenHeight * mainCamera.aspect;
        float leftBound = mainCamera.transform.position.x - screenWidth / 2f;
        float rightBound = mainCamera.transform.position.x + screenWidth / 2f;
        float bottomY = mainCamera.transform.position.y - screenHeight / 2f;
        
        // Выбираем случайную точку по горизонтали на нижней границе камеры
        float randomX = Random.Range(leftBound + 1f, rightBound - 1f);
        currentTarget = new Vector3(randomX, bottomY, 0);
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
