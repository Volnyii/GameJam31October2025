using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Контроллер крюка как UI элемента на Canvas
/// Крюк летит по прямой линии от игрока до точки тапа
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HookUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform игрока (назначается автоматически)")]
    public RectTransform playerRectTransform;
    [Tooltip("Картинка крюка (автоматически найдет если не назначена)")]
    public Image hookImage;
    [Tooltip("Линия веревки (создается автоматически)")]
    public Image lineImage;
    
    [Header("Settings")]
    [Tooltip("Скорость полета крюка")]
    public float hookSpeed = 10f;
    [Tooltip("Скорость возврата крюка")]
    public float retractSpeed = 15f;
    [Tooltip("Радиус для попадания в монстров (в пикселях)")]
    public float hookRadius = 60f;
    
    [Header("Line Settings")]
    [Tooltip("Толщина линии от игрока до крюка (в пикселях)")]
    [Range(5f, 50f)]
    public float lineThickness = 15f;
    [Tooltip("Цвет линии от игрока до крюка")]
    public Color lineColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    
    private RectTransform rectTransform;
    private bool isFlying = false;
    private bool isRetracting = false;
    private bool hasHitMonster = false; // Флаг что крюк уже убил монстра
    private Vector2 startPosition;
    private Vector2 targetPosition;
    private float flightProgress = 0f;
    
    void Awake()
    {
        SetupHookComponents();
    }
    
    void Start()
    {
        SetupHook();
    }
    
    /// <summary>
    /// Автоматически настраивает компоненты крюка для работы на Canvas
    /// </summary>
    [ContextMenu("Setup Hook Components")]
    public void SetupHookComponents()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }
        
        rectTransform.anchorMin = new Vector2(0.5f, 0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(32, 32);
        
        if (hookImage == null)
        {
            hookImage = GetComponent<Image>();
            if (hookImage == null)
            {
                hookImage = gameObject.AddComponent<Image>();
            }
        }
        
        if (hookImage.sprite == null)
        {
            CreateHookSprite();
        }
        
        hookImage.enabled = false;
        
        // Добавляем Rigidbody2D для работы коллайдера (kinematic, чтобы не влиять на физику)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.isKinematic = true; // Kinematic для UI элементов
        rb.gravityScale = 0f; // Отключаем гравитацию
        
        // Добавляем коллайдер для обнаружения столкновений с монстрами
        CircleCollider2D hookCollider = GetComponent<CircleCollider2D>();
        if (hookCollider == null)
        {
            hookCollider = gameObject.AddComponent<CircleCollider2D>();
        }
        hookCollider.isTrigger = true;
        // Конвертируем радиус из пикселей в мировые единицы и уменьшаем (примерно 100 пикселей = 1 единица)
        hookCollider.radius = (hookRadius * 0.4f) / 100f; // Уменьшаем до 40% от исходного размера
        
        // Устанавливаем тег для упрощения обнаружения
        if (!gameObject.CompareTag("Hook"))
        {
            gameObject.tag = "Hook";
        }
    }
    
    void SetupHook()
    {
        if (hookImage == null)
        {
            hookImage = GetComponent<Image>();
            if (hookImage == null)
            {
                hookImage = gameObject.AddComponent<Image>();
            }
        }
        
        if (hookImage.sprite == null)
        {
            CreateHookSprite();
        }
        
        hookImage.enabled = false;
        
        if (lineImage == null)
        {
            SetupHookLine();
        }
        
        if (lineImage != null)
        {
            lineImage.enabled = false;
        }
    }
    
    /// <summary>
    /// Бросает крюк в указанную позицию на Canvas
    /// </summary>
    public void CastHook(Vector2 targetPos)
    {
        if (isFlying || isRetracting || playerRectTransform == null) return;
        
        startPosition = playerRectTransform.anchoredPosition;
        targetPosition = targetPos;
        flightProgress = 0f;
        isFlying = true;
        isRetracting = false;
        hasHitMonster = false; // Сбрасываем флаг попадания при новом броске
        
        // Включаем коллайдер обратно при новом броске
        CircleCollider2D hookCollider = GetComponent<CircleCollider2D>();
        if (hookCollider != null)
        {
            hookCollider.enabled = true;
        }
        
        rectTransform.anchoredPosition = startPosition;
        
        if (hookImage == null)
        {
            hookImage = GetComponent<Image>();
            if (hookImage == null)
            {
                hookImage = gameObject.AddComponent<Image>();
            }
        }
        
        if (hookImage.sprite == null)
        {
            CreateHookSprite();
        }
        
        hookImage.enabled = true;
        
        if (lineImage == null)
        {
            SetupHookLine();
        }
        
        if (lineImage != null)
        {
            lineImage.enabled = true;
            lineImage.color = lineColor;
        }
    }
    
    void SetupHookLine()
    {
        if (lineImage != null) return;
        
        GameObject lineObj = new GameObject("HookLine");
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            lineObj.transform.SetParent(canvas.transform);
        }
        else if (transform.parent != null)
        {
            lineObj.transform.SetParent(transform.parent);
        }
        lineObj.transform.SetAsFirstSibling();
        
        RectTransform lineRect = lineObj.AddComponent<RectTransform>();
        lineRect.sizeDelta = new Vector2(lineThickness, 100);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.anchorMin = new Vector2(0.5f, 0f);
        lineRect.anchorMax = new Vector2(0.5f, 0f);
        
        lineImage = lineObj.AddComponent<Image>();
        lineImage.color = lineColor;
        
        CreateLineSprite();
    }
    
    void Update()
    {
        if (isFlying)
        {
            UpdateFlight();
        }
        else if (isRetracting)
        {
            UpdateRetract();
        }
        
        if ((isFlying || isRetracting) && playerRectTransform != null)
        {
            UpdateLineRenderer();
        }
    }
    
    /// <summary>
    /// Обновляет движение крюка по прямой линии
    /// </summary>
    void UpdateFlight()
    {
        if (!isFlying) return;
        
        float distance = Vector2.Distance(startPosition, targetPosition);
        
        if (distance < 0.1f)
        {
            rectTransform.anchoredPosition = targetPosition;
            CheckMonsterHit();
            isFlying = false;
            StartRetract();
            return;
        }
        
        flightProgress += Time.deltaTime * hookSpeed * 100f / distance;
        
        if (flightProgress >= 1f)
        {
            rectTransform.anchoredPosition = targetPosition;
            CheckMonsterHit();
            isFlying = false;
            StartRetract();
            return;
        }
        
        Vector2 currentPos = Vector2.Lerp(startPosition, targetPosition, flightProgress);
        rectTransform.anchoredPosition = currentPos;
        CheckMonsterHit();
    }
    
    void StartRetract()
    {
        isRetracting = true;
    }
    
    void UpdateRetract()
    {
        if (playerRectTransform == null) return;
        
        Vector2 targetPos = playerRectTransform.anchoredPosition;
        rectTransform.anchoredPosition = Vector2.MoveTowards(rectTransform.anchoredPosition, targetPos, retractSpeed * 100f * Time.deltaTime);
        
        if (Vector2.Distance(rectTransform.anchoredPosition, targetPos) < 10f)
        {
            isRetracting = false;
            if (hookImage != null)
            {
                hookImage.enabled = false;
            }
            if (lineImage != null)
            {
                lineImage.enabled = false;
            }
        }
    }
    
    /// <summary>
    /// Обновляет отображение линии от игрока до крюка
    /// </summary>
    void UpdateLineRenderer()
    {
        if (lineImage == null || playerRectTransform == null) return;
        
        if (!lineImage.enabled)
        {
            lineImage.enabled = true;
        }
        
        Vector2 playerPos = playerRectTransform.anchoredPosition;
        Vector2 hookPos = rectTransform.anchoredPosition;
        Vector2 direction = hookPos - playerPos;
        float distance = direction.magnitude;
        
        if (distance < 1f)
        {
            lineImage.enabled = false;
            return;
        }
        
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        RectTransform lineRect = lineImage.GetComponent<RectTransform>();
        if (lineRect != null)
        {
            lineRect.sizeDelta = new Vector2(lineThickness, distance);
            lineRect.anchoredPosition = (playerPos + hookPos) / 2f;
            lineRect.rotation = Quaternion.Euler(0, 0, angle - 90f);
            lineRect.pivot = new Vector2(0.5f, 0f);
            lineImage.color = lineColor;
        }
    }
    
    /// <summary>
    /// Проверяет, был ли уже убит монстр этим крюком
    /// </summary>
    public bool HasHitMonster()
    {
        return hasHitMonster;
    }
    
    /// <summary>
    /// Отмечает что крюк попал в монстра и отключает коллайдер
    /// </summary>
    public void MarkMonsterHit()
    {
        if (hasHitMonster) return;
        
        hasHitMonster = true;
        
        // Отключаем коллайдер чтобы не убивать других монстров
        CircleCollider2D hookCollider = GetComponent<CircleCollider2D>();
        if (hookCollider != null)
        {
            hookCollider.enabled = false;
        }
        
        // Попадание в монстра - начинаем возврат
        isFlying = false;
        StartRetract();
    }
    
    void CheckMonsterHit()
    {
        if (!isFlying || hasHitMonster) return;
        
        Vector2 hookPos = rectTransform.anchoredPosition;
        
        MonsterSpawnerUI spawner = MonsterSpawnerUI.Instance;
        if (spawner != null && spawner.ActiveMonsters != null && spawner.ActiveMonsters.Count > 0)
        {
            if (CheckMonstersInSpawner(spawner, hookPos))
            {
                return;
            }
        }
        
        // Fallback - ищем все монстры на Canvas
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            MonsterUI[] allMonsters = canvas.GetComponentsInChildren<MonsterUI>();
            
            foreach (var monster in allMonsters)
            {
                if (monster == null || !monster.gameObject.activeInHierarchy || monster.IsDead) continue;
                
                Vector2 monsterPos = monster.Position;
                float distance = Vector2.Distance(hookPos, monsterPos);
                
                if (distance <= hookRadius)
                {
                    monster.Die();
                    isFlying = false;
                    StartRetract();
                    return;
                }
            }
        }
    }
    
    bool CheckMonstersInSpawner(MonsterSpawnerUI spawner, Vector2 hookPos)
    {
        if (spawner.ActiveMonsters == null || spawner.ActiveMonsters.Count == 0)
        {
            return false;
        }
        
        foreach (var monsterObj in spawner.ActiveMonsters)
        {
            if (monsterObj == null || !monsterObj.activeInHierarchy) continue;
            
            MonsterUI monster = monsterObj.GetComponent<MonsterUI>();
            if (monster == null || monster.IsDead) continue;
            
            Vector2 monsterPos = monster.Position;
            float distance = Vector2.Distance(hookPos, monsterPos);
            
            if (distance <= hookRadius)
            {
                monster.Die();
                isFlying = false;
                StartRetract();
                return true;
            }
        }
        
        return false;
    }
    
    void CreateLineSprite()
    {
        if (lineImage == null) return;
        
        int width = Mathf.Max(20, Mathf.RoundToInt(lineThickness));
        int height = 100;
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        
        Color baseColor = Color.white;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distFromCenter = Mathf.Abs(x - width / 2f);
                float edgeDistance = width / 2f;
                
                if (distFromCenter < edgeDistance - 2f)
                {
                    pixels[y * width + x] = baseColor;
                }
                else if (distFromCenter < edgeDistance)
                {
                    float alpha = 1f - ((distFromCenter - (edgeDistance - 2f)) / 2f);
                    pixels[y * width + x] = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                }
                else
                {
                    pixels[y * width + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 100f);
        lineImage.sprite = sprite;
        lineImage.color = lineColor;
    }
    
    void CreateHookSprite()
    {
        if (hookImage == null)
        {
            hookImage = GetComponent<Image>();
            if (hookImage == null)
            {
                hookImage = gameObject.AddComponent<Image>();
            }
        }
        
        Texture2D texture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        Color hookColor = Color.gray;
        
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float distFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                
                if (distFromCenter < 14f)
                {
                    pixels[y * 32 + x] = hookColor;
                }
                else if (distFromCenter < 15f)
                {
                    pixels[y * 32 + x] = new Color(hookColor.r * 0.5f, hookColor.g * 0.5f, hookColor.b * 0.5f, 1f);
                }
                else
                {
                    pixels[y * 32 + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
        if (hookImage != null)
        {
            hookImage.sprite = sprite;
            hookImage.color = Color.white;
        }
    }
    
    public bool IsActive => isFlying || isRetracting;
}
