using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Контроллер монстра как UI элемента на Canvas
/// Автоматически настраивает все необходимые компоненты для работы на Canvas
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MonsterUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Картинка монстра (автоматически найдет если не назначена)")]
    public Image monsterImage;
    [Tooltip("Аниматор для монстра (опционально)")]
    public Animator animator;
    
    [Header("Movement")]
    public float moveSpeed = 1.5f;
    public float patrolRadius = 5f;
    public Vector2 centerPosition = Vector2.zero;
    public bool enableScreenWrap = true;
    [Tooltip("Закреплять Y координату на одной линии (если false, монстр может двигаться по своей полосе)")]
    public bool lockYPosition = false;
    [Tooltip("Y координата для фиксации (используется только если lockYPosition = true)")]
    public float fixedY = 100f;
    
    [Header("Animation")]
    public RuntimeAnimatorController[] animatorControllers;
    private int _currentAnimatorIndex = 0;
    
    private RectTransform rectTransform;
    private Vector2 currentTarget;
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
        if (rectTransform != null && originalScale == Vector3.one && rectTransform.localScale != Vector3.one)
        {
            originalScale = rectTransform.localScale;
        }
        
        // Если цель не установлена, устанавливаем её
        if (currentTarget == Vector2.zero)
        {
            SetRandomTarget();
        }
    }
    
    /// <summary>
    /// Автоматически настраивает все компоненты для работы на Canvas
    /// </summary>
    [ContextMenu("Setup Monster Components")]
    public void SetupMonsterComponents()
    {
        // RectTransform (обязателен)
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }
        
        // Настраиваем якоря для Canvas
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // Размер монстра
        rectTransform.sizeDelta = new Vector2(64, 64);
        
        // Image (обязателен для отображения)
        if (monsterImage == null)
        {
            monsterImage = GetComponent<Image>();
        }
        
        if (monsterImage == null)
        {
            monsterImage = gameObject.AddComponent<Image>();
        }
        
        // Animator (опционально, но создаем если нужен)
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            // Не создаем автоматически, только если уже есть
        }
        
        // Применяем аниматор контроллер
        UpdateAnimator();
        
        // Создаем спрайт только если он не назначен вручную
        // Если спрайт уже есть (из префаба или назначен вручную), используем его
        if (monsterImage.sprite == null)
        {
            CreateDefaultSprite();
        }
        
        // Добавляем компонент для обработки столкновений с крюком
        MonsterHitboxUI hitbox = GetComponent<MonsterHitboxUI>();
        if (hitbox == null)
        {
            hitbox = gameObject.AddComponent<MonsterHitboxUI>();
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
        if (rectTransform == null) return;
        
        Vector2 pos = rectTransform.anchoredPosition;
        pos.y = fixedY;
        rectTransform.anchoredPosition = pos;
    }
    
    void MoveTowardsTarget()
    {
        if (rectTransform == null) return;
        
        Vector2 currentPos = rectTransform.anchoredPosition;
        Vector2 targetPos = currentTarget;
        
        // Двигаемся к цели с учетом скорости
        float pixelsPerSecond = moveSpeed * 100f; // 1 unit = 100px
        
        // Двигаемся по X к цели
        currentPos.x = Mathf.MoveTowards(currentPos.x, targetPos.x, pixelsPerSecond * Time.deltaTime);
        
        // Двигаемся по Y к цели (только если Y не заблокирован)
        if (!lockYPosition)
        {
            currentPos.y = Mathf.MoveTowards(currentPos.y, targetPos.y, pixelsPerSecond * Time.deltaTime);
            
            // Ограничиваем отклонение по Y от центра полосы (±40px от centerPosition.y)
            if (centerPosition.y != 0)
            {
                float maxDeviation = 40f;
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
        
        rectTransform.anchoredPosition = currentPos;
        
        // Поворачиваем спрайт в сторону движения (сохраняем масштаб из префаба)
        float currentX = rectTransform.anchoredPosition.x;
        float targetX = currentTarget.x;
        
        // Сохраняем исходный масштаб при первом использовании
        if (originalScale == Vector3.one && rectTransform.localScale != Vector3.one && rectTransform.localScale != new Vector3(-1, 1, 1))
        {
            originalScale = rectTransform.localScale;
        }
        
        if (currentX < targetX)
        {
            // Смотрим вправо - используем исходный масштаб
            rectTransform.localScale = originalScale;
        }
        else if (currentX > targetX)
        {
            // Смотрим влево - отражаем по X, сохраняя Y и Z
            rectTransform.localScale = new Vector3(-originalScale.x, originalScale.y, originalScale.z);
        }
        
        // Если достигли цели или очень близко, выбираем новую
        float distanceX = Mathf.Abs(rectTransform.anchoredPosition.x - currentTarget.x);
        float distanceY = Mathf.Abs(rectTransform.anchoredPosition.y - currentTarget.y);
        float totalDistance = Mathf.Sqrt(distanceX * distanceX + distanceY * distanceY);
        
        if (totalDistance < 30f) // Порог в пикселях (общее расстояние)
        {
            SetRandomTarget();
        }
    }
    
    void WrapAroundScreen()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        
        // Для ScreenSpaceOverlay используем размер reference resolution
        float screenWidth = 1080f; // Reference resolution width в пикселях
        float leftBound = -screenWidth / 2f;
        float rightBound = screenWidth / 2f;
        
        Vector2 pos = rectTransform.anchoredPosition;
        
        // Если монстр вышел за левый край - появляется справа (на той же Y позиции)
        if (pos.x < leftBound)
        {
            pos.x = rightBound - 50f; // Немного отступ от края
            // Сохраняем Y позицию (не сбрасываем на bottomY)
            rectTransform.anchoredPosition = pos;
            SetRandomTarget();
        }
        // Если монстр вышел за правый край - появляется слева (на той же Y позиции)
        else if (pos.x > rightBound)
        {
            pos.x = leftBound + 50f; // Немного отступ от края
            // Сохраняем Y позицию (не сбрасываем на bottomY)
            rectTransform.anchoredPosition = pos;
            SetRandomTarget();
        }
    }
    
    void SetRandomTarget()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        
        // Для ScreenSpaceOverlay используем размер reference resolution
        float screenWidth = 1080f; // Reference resolution width в пикселях
        float leftBound = -screenWidth / 2f + 50f; // Отступ от краев
        float rightBound = screenWidth / 2f - 50f;
        
        // Используем текущую Y позицию монстра (для патрулирования на своей полосе)
        // Если Y заблокирован, используем фиксированную позицию
        float targetY = lockYPosition ? fixedY : rectTransform.anchoredPosition.y;
        
        // Если Y не заблокирован, добавляем небольшую вариацию (±30px от центра патрулирования)
        // Используем centerPosition.y как центр полосы, если он установлен
        if (!lockYPosition)
        {
            float laneCenterY = centerPosition.y != 0 ? centerPosition.y : rectTransform.anchoredPosition.y;
            targetY = laneCenterY + Random.Range(-30f, 30f);
        }
        
        // Выбираем случайную точку по горизонтали на текущей полосе монстра
        float randomX = Random.Range(leftBound, rightBound);
        currentTarget = new Vector2(randomX, targetY);
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
        if (animatorControllers != null && animatorControllers.Length > 0)
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
        Vector2 startScale = rectTransform.localScale;
        Color startColor = monsterImage.color;
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            rectTransform.localScale = Vector2.Lerp(startScale, Vector2.zero, t);
            monsterImage.color = Color.Lerp(startColor, Color.clear, t);
            
            yield return null;
        }
        
        // Возвращаем в пул или уничтожаем
        if (MonsterSpawnerUI.Instance != null)
        {
            MonsterSpawnerUI.Instance.ReturnMonster(gameObject);
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
        monsterImage.sprite = sprite;
        monsterImage.color = Color.white;
    }
    
    public bool IsDead => isDead;
    public int currentAnimatorIndex => _currentAnimatorIndex;
    
    public Vector2 Position
    {
        get { return rectTransform.anchoredPosition; }
        set { rectTransform.anchoredPosition = value; }
    }
}

