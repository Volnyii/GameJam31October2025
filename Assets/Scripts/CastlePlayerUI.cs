using UnityEngine;

/// <summary>
/// Контроллер игрока со SpriteRenderer
/// Назначьте спрайт в инспекторе и настройте размеры через playerScale или Transform.localScale
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CastlePlayerUI : MonoBehaviour
{
    [Header("Position Settings")]
    [Tooltip("Использовать явную позицию или рассчитывать из castlePosition")]
    public bool useCustomPosition = false;
    [Tooltip("Явная позиция игрока в мировых координатах (используется если useCustomPosition = true)")]
    public Vector3 playerPosition = Vector3.zero;
    
    [Header("Castle Position (для расчета, если useCustomPosition = false)")]
    [Tooltip("Высота вершины замка (используется только если useCustomPosition = false)")]
    public float castleTopHeight = 3f;
    [Tooltip("Позиция замка (используется только если useCustomPosition = false)")]
    public Vector2 castlePosition = Vector2.zero;
    
    // [Header("References")]
    //[Tooltip("Спрайт игрока (можно назначить в инспекторе)")]
   // public Sprite playerSprite;
    [Tooltip("Аниматор для игрока (опционально, для анимированных персонажей)")]
    public Animator animator;
    
    [Header("Player Size Settings")]
    [Tooltip("Размер игрока (масштаб спрайта). Можно настроить здесь или через Transform.localScale в редакторе")]
    [Range(0.001f, 5f)]
    public float playerScale = 1f;
    [Tooltip("Автоматически применять playerScale к Transform.localScale (если false, используйте Transform.localScale в редакторе)")]
    public bool usePlayerScale = true;
    
    [Header("Fishing Animation")]
    [Tooltip("Время анимации замаха (в секундах)")]
    [Range(0.1f, 2f)]
    public float windupDuration = 0.5f;
    [Tooltip("Угол наклона при замахе (в градусах)")]
    [Range(0f, 45f)]
    public float windupAngle = 25f;
    [Tooltip("Смещение при замахе назад (в мировых единицах)")]
    [Range(0f, 1f)]
    public float windupBackOffset = 0.2f;
    
    private SpriteRenderer spriteRenderer;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isWindingUp = false;
    private float windupProgress = 0f;
    private float lastAppliedScale = -1f; // Отслеживаем последнее примененное значение
    
    void Awake()
    {
        SetupPlayerComponents();
    }
    
    void Start()
    {
        SetupPlayer();
    }
    
    /// <summary>
    /// Автоматически настраивает компоненты игрока
    /// </summary>
    [ContextMenu("Setup Player Components")]
    public void SetupPlayerComponents()
    {
        // Получаем или создаем SpriteRenderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        spriteRenderer.sortingOrder = 5;
        spriteRenderer.sortingLayerName = "Default";
        
        spriteRenderer.color = Color.white;
        
        // Получаем или находим Animator (для анимированных персонажей)
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        // Применяем размер если включен usePlayerScale
        if (usePlayerScale)
        {
            ApplyPlayerScale();
        }
    }
    
    /// <summary>
    /// Применяет настройки размера игрока (масштаб)
    /// Можно вызвать вручную для применения изменений размера
    /// </summary>
    public void ApplyPlayerScale()
    {
        if (usePlayerScale)
        {
            Vector3 targetScale = new Vector3(playerScale, playerScale, 1f);
            transform.localScale = targetScale;
            lastAppliedScale = playerScale;
        }
    }
    
    /// <summary>
    /// Вызывается при изменении значений в инспекторе (в редакторе)
    /// </summary>
    void OnValidate()
    {
        // Применяем масштаб если значение изменилось в редакторе
        if (usePlayerScale && lastAppliedScale != playerScale && Application.isPlaying == false)
        {
            // В редакторе применяем напрямую
            if (spriteRenderer != null || GetComponent<SpriteRenderer>() != null)
            {
                Vector3 targetScale = new Vector3(playerScale, playerScale, 1f);
                transform.localScale = targetScale;
                lastAppliedScale = playerScale;
            }
        }
        
        // Применяем позицию если она была изменена в редакторе и используется явная позиция
        if (useCustomPosition && Application.isPlaying == false)
        {
            transform.position = playerPosition;
            originalPosition = playerPosition;
        }
    }
    
    public void SetupPlayer()
    {
        // Определяем позицию: явная или расчетная 
        Vector3 pos;
        if (useCustomPosition)
        {
            // Используем явную позицию
            pos = playerPosition;
        }
        else
        {
            // Рассчитываем позицию на вершине замка
            pos = new Vector3(castlePosition.x, castlePosition.y + castleTopHeight, 0);
        }
        
        transform.position = pos;
        originalPosition = pos;
        originalRotation = transform.localRotation;
        
        // Применяем размер если включен usePlayerScale
        if (usePlayerScale)
        {
            ApplyPlayerScale();
        }
    }
    
    /// <summary>
    /// Устанавливает позицию игрока вручную
    /// </summary>
    public void SetPosition(Vector3 position)
    {
        playerPosition = position;
        if (useCustomPosition)
        {
            transform.position = position;
            originalPosition = position;
        }
    }
    
    void Update()
    {
        if (isWindingUp)
        {
            UpdateWindupAnimation();
        }
        else if (windupProgress > 0f)
        {
            // Возвращаемся в исходное положение
            windupProgress = Mathf.Max(0f, windupProgress - Time.deltaTime * 2f);
            ApplyWindupTransform(windupProgress);
        }
        
        // Применяем размер только если значение изменилось (не каждый кадр)
        if (usePlayerScale && Mathf.Abs(lastAppliedScale - playerScale) > 0.001f)
        {
            ApplyPlayerScale();
        }
        
        // Обновляем позицию если она была изменена в инспекторе и используется явная позиция
        if (useCustomPosition && Vector3.Distance(transform.position, playerPosition) > 0.001f)
        {
            transform.position = playerPosition;
            originalPosition = playerPosition;
        }
    }
    
    /// <summary>
    /// Начинает анимацию замаха (как рыбак замахивается удочкой)
    /// </summary>
    public void StartWindup()
    {
        if (spriteRenderer == null) return;
        
        isWindingUp = true;
        originalPosition = transform.position;
        originalRotation = transform.localRotation;
        
        // Если есть аниматор, запускаем триггер анимации замаха
        if (animator != null)
        {
            animator.SetTrigger("Windup");
        }
    }
    
    /// <summary>
    /// Обновляет анимацию замаха
    /// </summary>
    void UpdateWindupAnimation()
    {
        if (spriteRenderer == null) return;
        
        windupProgress += Time.deltaTime / windupDuration;
        windupProgress = Mathf.Clamp01(windupProgress);
        
        ApplyWindupTransform(windupProgress);
    }
    
    /// <summary>
    /// Применяет трансформацию замаха
    /// </summary>
    void ApplyWindupTransform(float progress)
    {
        if (spriteRenderer == null) return;
        
        // Кривая анимации (ease-out для плавности)
        float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
        
        // Наклон назад и вверх (как рыбак замахивается)
        float angle = -windupAngle * easedProgress;
        transform.localRotation = Quaternion.Euler(0, 0, angle);
        
        // Небольшое смещение назад при замахе (в мировых единицах)
        Vector3 offset = new Vector3(-windupBackOffset * easedProgress, windupBackOffset * 0.5f * easedProgress, 0);
        transform.position = originalPosition + offset;
    }
    
    /// <summary>
    /// Завершает замах и делает бросок
    /// </summary>
    public void Cast(float castPower = 1f)
    {
        if (spriteRenderer == null) return;
        
        isWindingUp = false;
        
        // Если есть аниматор, запускаем триггер анимации броска
        if (animator != null)
        {
            animator.SetTrigger("Cast");
            animator.SetFloat("CastPower", castPower);
        }
        
        // Анимация броска - быстрое движение вперед
        StartCoroutine(CastAnimation(castPower));
    }
    
    /// <summary>
    /// Анимация броска крюка
    /// </summary>
    System.Collections.IEnumerator CastAnimation(float power)
    {
        float castDuration = 0.2f;
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.localRotation;
        
        // Быстрое движение вперед и вниз (бросок)
        while (elapsed < castDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / castDuration;
            
            // Движение вперед при броске (в мировых единицах)
            float forwardOffset = windupBackOffset * 1.5f * (1f - t) * power;
            float downOffset = windupBackOffset * 0.3f * (1f - t) * power;
            
            transform.position = originalPosition + new Vector3(forwardOffset, -downOffset, 0);
            
            // Возврат угла
            float angle = -windupAngle * (1f - t);
            transform.localRotation = Quaternion.Euler(0, 0, angle);
            
            yield return null;
        }
        
        // Возвращаемся в исходное положение
        while (windupProgress > 0f)
        {
            windupProgress = Mathf.Max(0f, windupProgress - Time.deltaTime * 3f);
            ApplyWindupTransform(windupProgress);
            yield return null;
        }
        
        // Финальная позиция
        transform.position = originalPosition;
        transform.localRotation = originalRotation;
    }
    
    /// <summary>
    /// Прерывает замах без броска
    /// </summary>
    public void CancelWindup()
    {
        isWindingUp = false;
        // windupProgress будет постепенно уменьшаться в Update
    }
    
    void CreatePlayerSprite()
    {
        // Создаем спрайт только если он не назначе
        
        Texture2D texture = new Texture2D(64, 64);
        Color[] pixels = new Color[64 * 64];
        
        Color playerColor = new Color(0.2f, 0.6f, 0.9f);
        
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float distFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(32, 32));
                
                if (distFromCenter < 25f)
                {
                    pixels[y * 64 + x] = playerColor;
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
    
    public Vector2 Position
    {
        get { return transform.position; }
        set { transform.position = new Vector3(value.x, value.y, 0); }
    }
}
