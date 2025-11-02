using UnityEngine;

/// <summary>
/// Контроллер крюка с SpriteRenderer
/// Крюк летит по дуге от игрока до точки тапа
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HookController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform игрока (назначается автоматически)")]
    public Transform playerTransform;
    
    [Header("Settings")]
    [Tooltip("Скорость полета крюка (для кривой Безье - нормализуется по расстоянию)")]
    public float hookSpeed = 2f; // Уменьшено для лучшей видимости кривой
    [Tooltip("Скорость возврата крюка")]
    public float retractSpeed = 15f;
    [Tooltip("Радиус для попадания в монстров")]
    public float hookRadius = 0.6f;
    [Tooltip("Максимальная длина цепи (радиус круга для полета)")]
    public float maxChainLength = 5f;
    
    [Header("Arc Settings")]
    [Tooltip("Высота дуги полета (в мировых единицах)")]
    [Range(1f, 10f)]
    public float arcHeight = 3f;
    [Tooltip("Смещение пика дуги (0 = середина, -1 = начало, 1 = конец)")]
    [Range(-1f, 1f)]
    public float arcPeakOffset = 0f;
    
    [Header("Line Settings")]
    [Tooltip("Толщина линии от игрока до крюка (цепь)")]
    [Range(0.01f, 0.2f)]
    public float lineThickness = 0.03f; // Тонкая металлическая цепь
    [Tooltip("Цвет линии от игрока до крюка (цепь)")]
    public Color lineColor = new Color(0.7f, 0.7f, 0.75f, 1f); // Металлический серый цвет
    
    [Header("Circular Arc")]
    [Tooltip("Угол дуги в градусах (больше = длиннее дуга)")]
    [Range(45f, 180f)]
    public float arcAngleDegrees = 90f; // Угол дуги от руки до цели
    
    [Header("Fishing Rod Effect")]
    [Tooltip("Скорость вращения крюка во время полета (градусов в секунду)")]
    [Range(0f, 720f)]
    public float hookRotationSpeed = 360f;
    [Tooltip("Количество точек для провисающей цепи (больше = плавнее)")]
    [Range(2, 30)]
    public int chainPoints = 20; // Больше точек для более плавной кривой
    [Tooltip("Сила провисания цепи во время полета")]
    [Range(0f, 3f)]
    public float chainSagAmount = 1.2f; // Заметное провисание во время полета
    [Tooltip("Сила провисания цепи в покое (когда крюк в руке - должно быть больше для мягкости)")]
    [Range(0f, 5f)]
    public float restChainSagAmount = 2.5f; // Сильное провисание в покое (как на картинках)
    [Tooltip("Показывать крюк и цепь даже когда не брошен (в руке игрока)")]
    public bool showHookAtRest = true;
    
    [Header("Hook Size Settings")]
    [Tooltip("Размер крюка (масштаб спрайта). Меньше значение = меньше крюк")]
    [Range(0.01f, 1f)]
    public float hookScale = 0.15f; // Маленький размер крюка
    [Tooltip("Сохранять размер крюка постоянным во время рендеринга (не масштабировать с камерой)")]
    public bool maintainConstantSize = true;
    
    private SpriteRenderer spriteRenderer;
    private LineRenderer lineRenderer;
    private bool isFlying = false;
    private bool isRetracting = false;
    private bool hasHitMonster = false; // Флаг что крюк уже убил монстра
    
    // Бросок по кривой Безье
    private enum FlightPhase
    {
        Flying,        // Фаза 1: Полет по кривой Безье
        Returning     // Фаза 2: Возврат
    }
    private FlightPhase currentPhase = FlightPhase.Flying;
    private Vector3 playerCenter; // Центр игрока
    private bool castDirectionRight; // true = вправо, false = влево
    private Vector3 tapWorldPosition; // Мировая позиция тапа
    
    // Точки для кривой Безье
    private Vector3 bezierStartPos; // Стартовая точка (рука)
    private Vector3 bezierMidPoint; // Средняя точка дуги
    private Vector3 bezierEndPos; // Конечная точка (точка тапа)
    private float bezierProgress = 0f; // Прогресс по кривой Безье (0-1)
    private float bezierDistance = 0f; // Длина кривой (для нормализации скорости)
    
    void Awake()
    {
        SetupHookComponents();
    }
    
    void Start()
    {
        SetupHook();
    }
    
    /// <summary>
    /// Автоматически настраивает компоненты крюка
    /// </summary>
    [ContextMenu("Setup Hook Components")]
    public void SetupHookComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        spriteRenderer.sortingOrder = 5; // Выше чем у других объектов
        spriteRenderer.sortingLayerName = "Default";
        
        // Загружаем спрайт крюка из папки Sprites
        if (spriteRenderer.sprite == null)
        {
            LoadHookSprite();
        }
        
        // Убеждаемся что спрайт видим
        if (spriteRenderer.sprite != null)
        {
            spriteRenderer.color = Color.white;
        }
        
        // Устанавливаем размер крюка
        ApplyHookScale();
        
        // Добавляем Rigidbody2D для работы коллайдера (kinematic, чтобы не влиять на физику)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.isKinematic = true; // Kinematic чтобы не влиять на движение крюка
        rb.gravityScale = 0f; // Отключаем гравитацию
        
        // Добавляем коллайдер для обнаружения столкновений с монстрами
        CircleCollider2D hookCollider = GetComponent<CircleCollider2D>();
        if (hookCollider == null)
        {
            hookCollider = gameObject.AddComponent<CircleCollider2D>();
        }
        hookCollider.isTrigger = true;
        // Уменьшенный радиус для более точного попадания
        hookCollider.radius = hookRadius * 0.4f; // Уменьшаем до 40% от исходного размера
        // Устанавливаем тег для упрощения обнаружения
        if (!gameObject.CompareTag("Hook"))
        {
            gameObject.tag = "Hook";
        }
        
        // Создаем LineRenderer для линии
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            
            // Проверяем что компонент действительно создан
            if (lineRenderer == null)
            {
                Debug.LogError("Не удалось создать LineRenderer компонент!");
                return; // Не можем продолжить без LineRenderer
            }
            
            // Пытаемся найти подходящий шейдер для 2D
            Material lineMaterial = null;
            
            // Попробуем несколько вариантов шейдеров
            string[] shaderNames = { "Unlit/Color", "Sprites/Default", "Standard" };
            Shader foundShader = null;
            
            foreach (string shaderName in shaderNames)
            {
                foundShader = Shader.Find(shaderName);
                if (foundShader != null)
                {
                    Debug.Log($"Найден шейдер для цепи: {shaderName}");
                    break;
                }
            }
            
            if (foundShader != null)
            {
                try
                {
                    lineMaterial = new Material(foundShader);
                    if (lineMaterial != null)
                    {
                        lineMaterial.color = lineColor;
                        lineRenderer.material = lineMaterial;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Ошибка при создании материала: {e.Message}");
                }
            }
            else
            {
                // Если не нашли шейдер, пробуем создать стандартный материал
                Debug.LogWarning("Не найден подходящий шейдер для LineRenderer, пробуем Standard");
                Shader standardShader = Shader.Find("Standard");
                if (standardShader != null)
                {
                    try
                    {
                        lineMaterial = new Material(standardShader);
                        if (lineMaterial != null)
                        {
                            lineMaterial.color = lineColor;
                            lineRenderer.material = lineMaterial;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Ошибка при создании Standard материала: {e.Message}");
                    }
                }
                
                // Если всё равно не получилось, создаем материал без шейдера (будет использоваться дефолтный)
                // Если не удалось создать материал, пробуем вообще без материала (цвет будет использоваться через startColor/endColor)
                if (lineRenderer != null && lineRenderer.material == null)
                {
                    Debug.LogError("КРИТИЧНО: Не удалось создать материал для LineRenderer! Цепь может не отображаться!");
                }
            }
            
            // Убеждаемся что lineRenderer всё ещё существует перед настройкой
            if (lineRenderer != null)
            {
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            // Тонкая металлическая цепь
            lineRenderer.startWidth = lineThickness;
            lineRenderer.endWidth = lineThickness;
            lineRenderer.positionCount = chainPoints; // Используем больше точек для плавной провисающей цепи
            lineRenderer.sortingOrder = 4; // Увеличили для видимости
            lineRenderer.useWorldSpace = true;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            
            // Устанавливаем начальные позиции (чтобы линия не была нулевой длины)
            if (playerTransform != null)
            {
                for (int i = 0; i < chainPoints; i++)
                {
                    lineRenderer.SetPosition(i, playerTransform.position);
                }
            }
            else
            {
                for (int i = 0; i < chainPoints; i++)
                {
                    lineRenderer.SetPosition(i, transform.position);
                }
            }
            }
        }
        
        // В стартовой позиции показываем крюк в руке с мягко провисающей цепью
        if (showHookAtRest)
        {
            SetupRestPosition();
        }
        else
        {
            // Отключаем компоненты по умолчанию (включатся когда крюк будет брошен)
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }
    }
    
    void SetupHook()
    {
        // Убеждаемся что компоненты инициализированы
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }
        
        if (spriteRenderer != null)
        {
            if (spriteRenderer.sprite == null)
            {
                LoadHookSprite();
            }
            
            // Крюк всегда виден (белый цвет для спрайта)
            spriteRenderer.color = Color.white;
            
            if (!showHookAtRest)
            {
                spriteRenderer.enabled = false;
            }
        }
        
        if (lineRenderer != null)
        {
            if (!showHookAtRest)
            {
                lineRenderer.enabled = false;
            }
        }
        
        // Применяем размер крюка
        ApplyHookScale();
    }
    
    /// <summary>
    /// Применяет настройки размера крюка (масштаб)
    /// </summary>
    void ApplyHookScale()
    {
        // Всегда применяем установленный размер
        Vector3 targetScale = new Vector3(hookScale, hookScale, 1f);
        transform.localScale = targetScale;
        
        if (maintainConstantSize)
        {
            // В Update будем постоянно проверять и восстанавливать размер
            // чтобы он не менялся во время рендеринга
        }
    }
    
    /// <summary>
    /// Настраивает стартовую позицию крюка в руке игрока с мягко провисающей цепью
    /// </summary>
    void SetupRestPosition()
    {
        if (playerTransform == null) return;
        
        // Крюк находится рядом с игроком (в руке)
        transform.position = playerTransform.position + Vector3.right * 0.3f; // Немного справа от игрока
        transform.rotation = Quaternion.identity; // Не вращаем в покое
        
        // Убеждаемся что размер крюка установлен правильно
        ApplyHookScale();
        
        // Показываем крюк
        if (spriteRenderer != null)
        {
            if (spriteRenderer.sprite == null)
            {
                CreateHookSprite();
            }
            spriteRenderer.enabled = true;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 5;
        }
        
        // Показываем провисающую цепь
        if (lineRenderer != null)
        {
            if (lineRenderer.positionCount != chainPoints)
            {
                lineRenderer.positionCount = chainPoints;
            }
            
            lineRenderer.enabled = true;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            // Тонкая металлическая цепь
            lineRenderer.startWidth = lineThickness;
            lineRenderer.endWidth = lineThickness;
            lineRenderer.sortingOrder = 4;
            
            // Обновляем провисающую цепь от игрока до крюка (с большим провисанием в покое)
            CalculateSaggingChain(playerTransform.position, transform.position, isAtRest: true);
        }
    }
    
    /// <summary>
    /// Бросает крюк в указанную мировую позицию по кривой Безье
    /// Определяет сторону (лево/право) и создает красивую дугу
    /// </summary>
    public void CastHook(Vector3 targetWorldPos)
    {
        if (isFlying || isRetracting || playerTransform == null) return;
        
        // Сохраняем позицию тапа
        tapWorldPosition = targetWorldPos;
        
        // Центр объекта (игрока)
        playerCenter = playerTransform.position;
        
        // Позиция руки
        Vector3 handPosition = playerCenter + (castDirectionRight ? Vector3.right : Vector3.left) * 0.3f;
        
        // Определяем сторону тапа относительно героя (лево/право)
        Vector3 toTarget = targetWorldPos - playerCenter;
        castDirectionRight = toTarget.x > 0; // true = вправо, false = влево
        
        // Обновляем позицию руки с правильным направлением
        handPosition = playerCenter + (castDirectionRight ? Vector3.right : Vector3.left) * 0.3f;
        
        // Вычисляем точки для кривой Безье
        bezierStartPos = handPosition;
        bezierEndPos = targetWorldPos;
        
        // ======================= ИСПРАВЛЕННЫЙ РАСЧЁТ ДУГИ =======================

// Ограничиваем дальность броска
        // Определяем направление до цели
        Vector3 dir = targetWorldPos - handPosition;
        float distance = dir.magnitude;

// Используем maxChainLength только для расчёта формы дуги (высота)
        float clampedDistance = Mathf.Min(distance, maxChainLength);
        float height = Mathf.Clamp(clampedDistance * 0.3f, 1f, 4f);

// Определяем сторону дуги (влево/вправо)
        float direction = (targetWorldPos.x > playerCenter.x) ? 1f : -1f;
        Vector3 perpendicular = Vector3.Cross(Vector3.forward, dir.normalized);

// Средняя точка дуги (от руки к цели) — теперь всегда доходит до точки тапа!
        bezierEndPos = targetWorldPos;
        bezierMidPoint = handPosition + dir * 0.5f + perpendicular * direction * height + Vector3.up * height * 0.7f;

        // Приблизительно: расстояние старт->середина + середина->конец
        float distToMid = Vector3.Distance(bezierStartPos, bezierMidPoint);
        float distMidToEnd = Vector3.Distance(bezierMidPoint, bezierEndPos);
        bezierDistance = distToMid + distMidToEnd;
        
        // Инициализация броска
        currentPhase = FlightPhase.Flying;
        bezierProgress = 0f;
        isFlying = true;
        isRetracting = false;
        hasHitMonster = false; // Сбрасываем флаг попадания при новом броске
        
        // Включаем коллайдер обратно при новом броске
        CircleCollider2D hookCollider = GetComponent<CircleCollider2D>();
        if (hookCollider != null)
        {
            hookCollider.enabled = true;
        }
        
        // Начинаем с позиции руки
        transform.position = handPosition;
        
        // Сбрасываем вращение
        transform.rotation = Quaternion.identity;
        
        // Убеждаемся что spriteRenderer инициализирован
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                CreateHookSprite();
            }
        }
        
        // Убеждаемся что спрайт загружен
        if (spriteRenderer.sprite == null)
        {
            LoadHookSprite();
        }
        
        spriteRenderer.enabled = true;
        spriteRenderer.color = Color.white; // Белый цвет для спрайта
        spriteRenderer.sortingOrder = 5;
        
        // Устанавливаем размер крюка согласно настройкам
        ApplyHookScale();
        
        Debug.Log($"Крюк активирован:");
        Debug.Log($"  - Позиция: {transform.position}");
        Debug.Log($"  - Спрайт: {(spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "NULL")}");
        Debug.Log($"  - Enabled: {spriteRenderer.enabled}");
        Debug.Log($"  - Цвет: {spriteRenderer.color}");
        Debug.Log($"  - SortingOrder: {spriteRenderer.sortingOrder}");
        Debug.Log($"  - Размер спрайта: {(spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.ToString() : "NULL")}");
        
        // Настраиваем LineRenderer
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                SetupHookComponents(); // Переинициализируем если нужно
            }
        }
        
        // Убеждаемся что lineRenderer инициализирован
        if (lineRenderer == null)
        {
            Debug.LogError("Не удалось инициализировать LineRenderer для цепи!");
            return; // Не можем продолжить без LineRenderer
        }
        
        // Если material == null, пытаемся создать
        if (lineRenderer.material == null)
        {
            Debug.LogWarning("Material цепи отсутствует, создаю заново...");
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
                if (lineRenderer.material != null)
                {
                    lineRenderer.material.color = lineColor;
                }
            }
        }
        
        lineRenderer.enabled = true;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        // Тонкая металлическая цепь
        lineRenderer.startWidth = lineThickness;
        lineRenderer.endWidth = lineThickness;
        lineRenderer.sortingOrder = 4;
        
        // Устанавливаем начальные позиции для всех точек цепи
        if (playerTransform != null)
        {
            CalculateSaggingChain(playerTransform.position, transform.position);
        }
        
        Debug.Log($"Цепь активирована:");
        Debug.Log($"  - Enabled: {lineRenderer.enabled}");
        Debug.Log($"  - Цвет: {lineColor}");
        Debug.Log($"  - Толщина: {lineThickness}");
        if (playerTransform != null)
        {
            Debug.Log($"  - Позиции: от {playerTransform.position} до {transform.position}");
        }
        Debug.Log($"  - Material: {(lineRenderer.material != null ? lineRenderer.material.name : "NULL")}");
        if (lineRenderer.material != null && lineRenderer.material.shader != null)
        {
            Debug.Log($"  - Shader: {lineRenderer.material.shader.name}");
        }
    }
    
    void Update()
    {
        if (isFlying)
        {
            UpdateFlight();
            // Вращаем крюк только во время растяжки и падения (не в возврате)
            // Вращение останавливаем только если крюк завис или достиг конечной точки
            if (spriteRenderer != null && spriteRenderer.enabled && currentPhase != FlightPhase.Returning)
            {
                transform.Rotate(0f, 0f, hookRotationSpeed * Time.deltaTime);
            }
        }
        else if (isRetracting)
        {
            UpdateRetract();
            // Замедляем вращение при возврате
            if (spriteRenderer != null && spriteRenderer.enabled)
            {
                transform.Rotate(0f, 0f, hookRotationSpeed * 0.5f * Time.deltaTime);
            }
        }
        else if (showHookAtRest && !isFlying && !isRetracting)
        {
            // Обновляем позицию покоя (крюк в руке с мягко провисающей цепью)
            SetupRestPosition();
        }
        
        // Обновляем цепь всегда, когда она должна быть видна
        if (playerTransform != null && lineRenderer != null)
        {
            if (isFlying || isRetracting || (showHookAtRest && !isFlying && !isRetracting))
            {
                UpdateLineRenderer();
            }
        }
        
        // Поддерживаем постоянный размер крюка во время рендеринга
        if (maintainConstantSize)
        {
            Vector3 currentScale = transform.localScale;
            Vector3 targetScale = new Vector3(hookScale, hookScale, 1f);
            if (currentScale != targetScale)
            {
                transform.localScale = targetScale;
            }
        }
    }
    
    /// <summary>
    /// Обновляет движение крюка - полет по кривой Безье → возврат
    /// </summary>
    void UpdateFlight()
    {
        if (!isFlying) return;
        
        switch (currentPhase)
        {
            case FlightPhase.Flying:
                UpdateFlyingPhase();
                break;
            case FlightPhase.Returning:
                UpdateReturningPhase();
                break;
        }
        
        CheckMonsterHit();
    }
    
    /// <summary>
    /// ФАЗА 1: Крюк летит по кривой Безье к точке тапа
    /// </summary>
    void UpdateFlyingPhase()
    {
        // Вычисляем скорость с учетом длины кривой (нормализация)
        // Скорость = единицы в секунду / длина кривой
        // Это делает полет более плавным и заметным независимо от расстояния
        float normalizedSpeed = hookSpeed / Mathf.Max(bezierDistance, 0.1f);
        
        // Увеличиваем прогресс по кривой Безье (нормализованная скорость)
        bezierProgress += Time.deltaTime * normalizedSpeed;
        
        // Если достигли конца кривой или точки тапа
        if (bezierProgress >= 1f)
        {
            bezierProgress = 1f;
            transform.position = bezierEndPos;
            // Достигли точки тапа - сразу начинаем возврат (не останавливаемся)
            currentPhase = FlightPhase.Returning;
            return;
        }
        
        // Получаем позицию на кривой Безье
        Vector3 arcPosition = GetBezierPoint(bezierStartPos, bezierMidPoint, bezierEndPos, bezierProgress);
        transform.position = arcPosition;
    }
    
    /// <summary>
    /// ФАЗА 2: Возврат крюка по кратчайшей траектории
    /// </summary>
    void UpdateReturningPhase()
    {
        Vector3 handPosition = playerCenter + (castDirectionRight ? Vector3.right : Vector3.left) * 0.3f;
        float distanceToHand = Vector3.Distance(transform.position, handPosition);
        
        if (distanceToHand < 0.1f)
        {
            // Вернулись к игроку
            transform.position = handPosition;
            isFlying = false;
            
            // Возвращаемся в состояние покоя
            if (showHookAtRest)
            {
                SetupRestPosition();
            }
            else
            {
                spriteRenderer.enabled = false;
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = false;
                }
            }
            return;
        }
        
        // Движемся по прямой к руке (кратчайшая траектория)
        transform.position = Vector3.Lerp(transform.position, handPosition, Time.deltaTime * retractSpeed * 0.3f);
    }
    
    /// <summary>
    /// Получает точку на кривой Безье через квадратичную Безье-кривую
    /// </summary>
    Vector3 GetBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Pow(1 - t, 2) * p0 +
               2 * (1 - t) * t * p1 +
               Mathf.Pow(t, 2) * p2;
    }
    
    void StartRetract()
    {
        isRetracting = true;
    }
    
    void UpdateRetract()
    {
        if (playerTransform == null) return;
        
        Vector3 handPosition = playerTransform.position + (castDirectionRight ? Vector3.right : Vector3.left) * 0.3f;
        transform.position = Vector3.MoveTowards(transform.position, handPosition, retractSpeed * Time.deltaTime);
        
        if (Vector3.Distance(transform.position, handPosition) < 0.1f)
        {
            isRetracting = false;
            
            // Возвращаемся в состояние покоя
            if (showHookAtRest)
            {
                SetupRestPosition();
            }
            else
            {
                spriteRenderer.enabled = false;
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = false;
                }
            }
        }
    }
    
    /// <summary>
    /// Обновляет отображение линии от игрока до крюка с эффектом провисающей веревки
    /// </summary>
    void UpdateLineRenderer()
    {
        if (lineRenderer == null || playerTransform == null) return;
        
        if (!lineRenderer.enabled)
        {
            lineRenderer.enabled = true;
        }
        
        Vector3 playerPos = playerTransform.position;
        Vector3 hookPos = transform.position;
        float distance = Vector3.Distance(playerPos, hookPos);
        
        if (distance < 0.1f)
        {
            lineRenderer.enabled = false;
            return;
        }
        
        // Используем цвет веревки (коричнево-оранжевый, как на картинках)
        Color chainColor = lineColor;
        lineRenderer.startColor = chainColor;
        lineRenderer.endColor = chainColor; // Одинаковый цвет для лучшей видимости
        
        // Заметная толщина цепи для хорошей видимости (как на картинках)
        float visibleThickness = Mathf.Max(lineThickness, 0.08f); // Минимум для хорошей видимости
        lineRenderer.startWidth = visibleThickness;
        lineRenderer.endWidth = visibleThickness;
        
        // Убеждаемся что материал правильно настроен
        if (lineRenderer.material != null)
        {
            lineRenderer.material.color = chainColor;
        }
        
        // Обновляем количество точек если изменилось
        if (lineRenderer.positionCount != chainPoints)
        {
            lineRenderer.positionCount = chainPoints;
        }
        
        // Создаем провисающую цепь (как веревка удочки)
        CalculateSaggingChain(playerPos, hookPos);
    }
    
    /// <summary>
    /// Вычисляет позиции точек для цепи:
    /// - Во время полета (Stretching/Falling): ровная прямая линия от руки до крюка
    /// - В покое: провисание под действием силы тяжести
    /// - Во время возврата: ровная прямая линия
    /// </summary>
    void CalculateSaggingChain(Vector3 start, Vector3 end, bool isAtRest = false)
    {
        if (lineRenderer == null) return;
        
        lineRenderer.textureMode = LineTextureMode.Stretch;
        
        int points = Mathf.Max(chainPoints, 2); // Минимум 2 точки
        
        // Во время полета (Stretching/Falling) или возврата - прямая линия
        if (isFlying || isRetracting)
        {
            // РОВНАЯ ПРЯМАЯ ЛИНИЯ от руки до крюка
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                Vector3 linearPos = Vector3.Lerp(start, end, t);
                // добавим лёгкое провисание при полёте
                float sag = Mathf.Sin(Mathf.PI * t) * chainSagAmount * 0.2f;
                linearPos.y -= sag;
                lineRenderer.SetPosition(i, linearPos);
            }
            return;
        }
        
        // В ПОКОЕ - провисание под действием силы тяжести
        if (isAtRest)
        {
            // Вычисляем горизонтальное расстояние
            float horizontalDistance = Vector3.Distance(
                new Vector3(start.x, 0, start.z), 
                new Vector3(end.x, 0, end.z)
            );
            
            // Максимальная длина провисания
            float sagMultiplier = restChainSagAmount;
            float sagHeight = Mathf.Max(horizontalDistance * sagMultiplier, 0.8f);
            
            // Если крюк ниже игрока, уменьшаем провисание
            float heightDiff = end.y - start.y;
            if (heightDiff < 0)
            {
                sagHeight *= Mathf.Clamp01(1f + heightDiff / (Mathf.Max(horizontalDistance, 0.5f) * 0.5f));
            }
            
            // Кривая провисания (параболическая форма)
            float sagCurve = 4.5f;
            
            // Создаем точки цепи с провисанием
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1); // От 0 до 1
                
                // Линейная интерполяция по X и Z
                Vector3 linearPos = Vector3.Lerp(start, end, t);
                
                // Добавляем провисание по Y (параболическая форма)
                float sagY = sagCurve * sagHeight * t * (1f - t);
                
                // Применяем провисание (опускаем цепь вниз)
                linearPos.y -= sagY;
                
                lineRenderer.SetPosition(i, linearPos);
            }
        }
        else
        {
            // По умолчанию - прямая линия
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                Vector3 linearPos = Vector3.Lerp(start, end, t);
                lineRenderer.SetPosition(i, linearPos);
            }
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
        currentPhase = FlightPhase.Returning;
    }
    
    void CheckMonsterHit()
    {
        if (!isFlying || currentPhase != FlightPhase.Flying || hasHitMonster) return;
        
        Vector3 hookPos = transform.position;
        
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (spawner != null && spawner.ActiveMonsters != null && spawner.ActiveMonsters.Count > 0)
        {
            if (CheckMonstersInSpawner(spawner, hookPos))
            {
                return;
            }
        }
        
        // Fallback - ищем все монстры в сцене
        MonsterController[] allMonsters = FindObjectsOfType<MonsterController>();
        
        foreach (var monster in allMonsters)
        {
            if (monster == null || !monster.gameObject.activeInHierarchy || monster.IsDead) continue;
            
            Vector3 monsterPos = monster.transform.position;
            float distance = Vector3.Distance(hookPos, monsterPos);
            
            if (distance <= hookRadius)
            {
                monster.Die();
                // Попадание в монстра - начинаем возврат
                currentPhase = FlightPhase.Returning;
                return;
            }
        }
    }
    
    bool CheckMonstersInSpawner(MonsterSpawner spawner, Vector3 hookPos)
    {
        if (spawner.ActiveMonsters == null || spawner.ActiveMonsters.Count == 0)
        {
            return false;
        }
        
        foreach (var monsterObj in spawner.ActiveMonsters)
        {
            if (monsterObj == null || !monsterObj.activeInHierarchy) continue;
            
            MonsterController monster = monsterObj.GetComponent<MonsterController>();
            if (monster == null || monster.IsDead) continue;
            
            Vector3 monsterPos = monster.transform.position;
            float distance = Vector3.Distance(hookPos, monsterPos);
            
            if (distance <= hookRadius)
            {
                monster.Die();
                // Попадание в монстра - начинаем возврат
                currentPhase = FlightPhase.Returning;
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Загружает спрайт крюка из Sprites/hook или создает его программно
    /// </summary>
    void LoadHookSprite()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }
        
        Sprite loadedSprite = null;
        
        #if UNITY_EDITOR
        // В редакторе используем AssetDatabase для загрузки из Assets/Sprites/hook.png
        loadedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/hook.png");
        #endif
        
        // Если не загрузили в редакторе, пробуем Resources
        if (loadedSprite == null)
        {
            loadedSprite = Resources.Load<Sprite>("Sprites/hook");
        }
        
        // Если не загрузили через Resources, пробуем StreamingAssets
        if (loadedSprite == null)
        {
            string spritePath = System.IO.Path.Combine(Application.streamingAssetsPath, "hook.png");
            if (System.IO.File.Exists(spritePath))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(spritePath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    loadedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }
        
        // Если не загрузили из StreamingAssets, пробуем прямой путь к Assets (только в редакторе)
        #if UNITY_EDITOR
        if (loadedSprite == null)
        {
            string fullPath = System.IO.Path.Combine(Application.dataPath, "Sprites", "hook.png");
            if (System.IO.File.Exists(fullPath))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(fullPath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    loadedSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }
        #endif
        
        if (loadedSprite != null)
        {
            spriteRenderer.sprite = loadedSprite;
            // Применяем размер крюка после загрузки спрайта
            ApplyHookScale();
            Debug.Log("✓ Спрайт крюка загружен из Sprites/hook");
        }
        else
        {
            // Если не удалось загрузить, создаем программно
            Debug.LogWarning("Спрайт крюка не найден в Sprites/hook, создаю программно");
            CreateHookSprite();
        }
    }
    
    void CreateHookSprite()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }
        
        // Увеличиваем размер крюка для лучшей видимости
        int size = 64; // Увеличили с 32 до 64 для лучшей видимости
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        
        // Яркий цвет крюка - красный для контраста с синей цепью
        Color hookColor = new Color(1f, 0.3f, 0.2f, 1f); // Яркий красный
        Color hookBorder = new Color(0.7f, 0.1f, 0.05f, 1f); // Темно-красная обводка
        Color hookHighlight = new Color(1f, 0.6f, 0.5f, 1f); // Светло-красный блик
        
        float center = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                
                if (distFromCenter < center * 0.75f) // 75% от радиуса
                {
                    // Основное тело крюка
                    pixels[y * size + x] = hookColor;
                    
                    // Добавляем блик в верхней части
                    if (y > center * 1.2f && distFromCenter < center * 0.5f)
                    {
                        pixels[y * size + x] = hookHighlight;
                    }
                }
                else if (distFromCenter < center * 0.88f) // Обводка
                {
                    pixels[y * size + x] = hookBorder;
                }
                else if (distFromCenter < center * 0.94f) // Полупрозрачная граница
                {
                    pixels[y * size + x] = new Color(hookBorder.r, hookBorder.g, hookBorder.b, 0.5f);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
        
        // Применяем размер крюка
        ApplyHookScale();
        
        Debug.Log($"Спрайт крюка создан: размер={size}x{size}, цвет={spriteRenderer.color}, scale={hookScale}");
    }
    
    public bool IsActive => isFlying || isRetracting;
}
