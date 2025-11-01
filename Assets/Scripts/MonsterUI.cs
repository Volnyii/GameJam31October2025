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
    }
    
    void Update()
    {
        if (isDead) return;
        
        // Убеждаемся что монстр на нижней границе экрана
        MaintainBottomPosition();
        
        MoveTowardsTarget();
        
        // Применяем обтекание экрана
        if (enableScreenWrap)
        {
            WrapAroundScreen();
        }
    }
    
    /// <summary>
    /// Поддерживает позицию монстра на нижней границе экрана
    /// </summary>
    void MaintainBottomPosition()
    {
        if (rectTransform == null) return;
        
        // Для Canvas в ScreenSpaceOverlay нижняя граница примерно 100-150 пикселей от низа
        float bottomY = 100f; // Отступ от низа экрана в пикселях
        
        Vector2 pos = rectTransform.anchoredPosition;
        pos.y = bottomY;
        rectTransform.anchoredPosition = pos;
    }
    
    void MoveTowardsTarget()
    {
        if (rectTransform == null) return;
        
        // Движение только по горизонтали, Y всегда на нижней границе
        Vector2 currentPos = rectTransform.anchoredPosition;
        Vector2 targetPos = currentTarget;
        
        // Двигаемся только по X, Y остается на нижней границе
        float pixelsPerSecond = moveSpeed * 100f; // 1 unit = 100px
        currentPos.x = Mathf.MoveTowards(currentPos.x, targetPos.x, pixelsPerSecond * Time.deltaTime);
        
        // Поддерживаем Y на нижней границе экрана
        currentPos.y = 100f; // Нижняя граница в пикселях
        
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
        float distanceToTarget = Mathf.Abs(rectTransform.anchoredPosition.x - currentTarget.x);
        if (distanceToTarget < 20f) // Порог в пикселях
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
        float bottomY = 100f; // Нижняя граница экрана
        
        Vector2 pos = rectTransform.anchoredPosition;
        
        // Если монстр вышел за левый край - появляется справа
        if (pos.x < leftBound)
        {
            pos.x = rightBound - 50f; // Немного отступ от края
            pos.y = bottomY; // Поддерживаем нижнюю границу
            rectTransform.anchoredPosition = pos;
            SetRandomTarget();
        }
        // Если монстр вышел за правый край - появляется слева
        else if (pos.x > rightBound)
        {
            pos.x = leftBound + 50f; // Немного отступ от края
            pos.y = bottomY; // Поддерживаем нижнюю границу
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
        float bottomY = 100f; // Нижняя граница экрана в пикселях
        
        // Выбираем случайную точку по горизонтали на нижней границе экрана
        float randomX = Random.Range(leftBound, rightBound);
        currentTarget = new Vector2(randomX, bottomY);
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

