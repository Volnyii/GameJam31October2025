using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Спавнер монстров на Canvas с поддержкой пула разных типов монстров
/// </summary>
public class MonsterSpawnerUI : MonoBehaviour
{
    public static MonsterSpawnerUI Instance { get; private set; }
    
    [Header("Settings")]
    [Tooltip("Общее количество монстров на сцене")]
    public int monsterCount = 5;
    [Tooltip("Радиус патрулирования вокруг замка")]
    public float spawnRadius = 6f;
    [Tooltip("Центр замка (в единицах Canvas)")]
    public Vector2 castleCenter = Vector2.zero;
    
    [Header("Spawn Lanes (Полосы спавна монстров)")]
    [Tooltip("Y координаты трех полос для спавна монстров (в пикселях Canvas)")]
    public float[] spawnLaneY = new float[3] { 200f, 400f, 600f };
    [Tooltip("Показывать визуализацию полос в редакторе")]
    public bool showLaneGizmos = true;
    [Tooltip("Цвет визуализации полос (только в редакторе)")]
    public Color laneGizmoColor = new Color(1f, 0f, 0f, 0.5f);
    [Tooltip("Длина линии визуализации полосы")]
    public float laneGizmoLength = 1000f;
    
    [Header("Monster Pool System")]
    [Tooltip("Массив типов монстров (можно задать разные префабы)")]
    public MonsterPoolData[] monsterTypes = new MonsterPoolData[0];
    
    [Header("Monster Pools")]
    [Tooltip("Доступные аниматоры (применяются ко всем типам монстров)")]
    public RuntimeAnimatorController[] availableAnimators;
    
    private Canvas parentCanvas;
    private List<GameObject> activeMonsters = new List<GameObject>();
    
    // Пул для каждого типа монстра отдельно
    private Dictionary<GameObject, Queue<GameObject>> monsterPools = new Dictionary<GameObject, Queue<GameObject>>();
    
    // Словарь для быстрого поиска типа монстра по GameObject
    private Dictionary<GameObject, GameObject> monsterTypeMap = new Dictionary<GameObject, GameObject>();
    
    public List<GameObject> ActiveMonsters => activeMonsters;
    
    /// <summary>
    /// Проверяет и исправляет настройки полос при изменении в редакторе
    /// </summary>
    void OnValidate()
    {
        // Убеждаемся, что массив полос содержит ровно 3 элемента
        if (spawnLaneY == null || spawnLaneY.Length != 3)
        {
            float[] newLanes = new float[3];
            if (spawnLaneY != null && spawnLaneY.Length > 0)
            {
                // Копируем существующие значения
                for (int i = 0; i < 3 && i < spawnLaneY.Length; i++)
                {
                    newLanes[i] = spawnLaneY[i];
                }
                // Заполняем недостающие значения
                for (int i = spawnLaneY.Length; i < 3; i++)
                {
                    newLanes[i] = 200f + i * 200f;
                }
            }
            else
            {
                // Дефолтные значения
                newLanes = new float[3] { 200f, 400f, 600f };
            }
            spawnLaneY = newLanes;
        }
    }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            parentCanvas = FindObjectOfType<Canvas>();
        }
        
        // Инициализируем пулы
        InitializePools();
        
        SpawnMonsters();
    }
    
    /// <summary>
    /// Инициализирует пулы для всех типов монстров
    /// </summary>
    void InitializePools()
    {
        // Проверяем, есть ли типы монстров
        if (monsterTypes == null || monsterTypes.Length == 0)
        {
            Debug.LogWarning("MonsterSpawnerUI: массив monsterTypes пуст! Добавьте хотя бы один MonsterPoolData.");
            return;
        }
        
        // Инициализируем пулы для каждого типа
        foreach (var monsterType in monsterTypes)
        {
            if (monsterType.monsterPrefab == null) continue;
            
            Queue<GameObject> pool = new Queue<GameObject>();
            monsterPools[monsterType.monsterPrefab] = pool;
            
            // Создаем минимальное количество для пула
            for (int i = 0; i < monsterType.poolMinSize; i++)
            {
                GameObject precreated = CreateMonsterFromPrefab(monsterType.monsterPrefab);
                // Убеждаемся что масштаб из префаба сохранен
                precreated.transform.localScale = monsterType.monsterPrefab.transform.localScale;
                precreated.SetActive(false);
                pool.Enqueue(precreated);
            }
            
            Debug.Log($"✓ Пул инициализирован для {monsterType.monsterTypeName}: {monsterType.poolMinSize} объектов");
        }
    }
    
    void SpawnMonsters()
    {
        for (int i = 0; i < monsterCount; i++)
        {
            SpawnMonster();
        }
    }
    
    /// <summary>
    /// Выбирает случайный тип монстра на основе весов спавна
    /// </summary>
    GameObject GetRandomMonsterPrefab()
    {
        // Если есть массив типов, используем его
        if (monsterTypes != null && monsterTypes.Length > 0)
        {
            // Фильтруем валидные типы
            var validTypes = monsterTypes.Where(t => t != null && t.monsterPrefab != null).ToArray();
            if (validTypes.Length == 0) return null;
            
            // Вычисляем общий вес
            int totalWeight = validTypes.Sum(t => t.spawnWeight);
            if (totalWeight == 0) return validTypes[0].monsterPrefab;
            
            // Выбираем случайный тип на основе весов
            int random = Random.Range(0, totalWeight);
            int currentWeight = 0;
            
            foreach (var type in validTypes)
            {
                currentWeight += type.spawnWeight;
                if (random < currentWeight)
                {
                    return type.monsterPrefab;
                }
            }
            
            return validTypes[validTypes.Length - 1].monsterPrefab;
        }
        
        return null;
    }
    
    GameObject SpawnMonster()
    {
        GameObject prefab = GetRandomMonsterPrefab();
        if (prefab == null)
        {
            Debug.LogError("Не найден префаб монстра для спавна! Убедитесь, что массив monsterTypes не пуст и все префабы назначены.");
            return null;
        }
        
        GameObject monster = null;
        MonsterUI monsterUI = null;
        
        // Пытаемся взять из пула
        if (monsterPools.ContainsKey(prefab))
        {
            Queue<GameObject> pool = monsterPools[prefab];
            if (pool.Count > 0)
            {
                monster = pool.Dequeue();
                // Восстанавливаем масштаб из префаба при активации из пула
                Vector3 prefabScale = prefab.transform.localScale;
                monster.transform.localScale = prefabScale; // Всегда восстанавливаем масштаб из префаба
                
                // Обновляем originalScale в контроллере
                monsterUI = monster.GetComponent<MonsterUI>();
                if (monsterUI != null)
                {
                    monsterUI.SetOriginalScale(prefabScale);
                }
                
                monster.SetActive(true);
            }
        }
        
        // Если в пуле нет, создаем новый
        if (monster == null)
        {
            monster = CreateMonsterFromPrefab(prefab);
        }
        
        // Сохраняем связь монстра с его типом
        if (!monsterTypeMap.ContainsKey(monster))
        {
            monsterTypeMap[monster] = prefab;
        }
        
        // Получаем или создаем контроллер монстра
        if (monsterUI == null)
        {
            monsterUI = monster.GetComponent<MonsterUI>();
            if (monsterUI == null)
            {
                monsterUI = monster.AddComponent<MonsterUI>();
            }
        }
        
        // Устанавливаем позицию на окружности вокруг замка
        // Монстры распределяются между тремя полосами по Y
        float angleStep = 360f / monsterCount;
        int currentIndex = activeMonsters.Count;
        float angle = (angleStep * currentIndex + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
        
        // Выбираем полосу для этого монстра (равномерное распределение)
        // Убеждаемся, что массив полос инициализирован
        if (spawnLaneY == null || spawnLaneY.Length == 0)
        {
            // Если массив не инициализирован, используем дефолтные значения
            spawnLaneY = new float[3] { 200f, 400f, 600f };
        }
        
        int laneIndex = currentIndex % spawnLaneY.Length;
        float laneY = spawnLaneY[laneIndex];
        
        // Добавляем небольшую случайную вариацию по Y в пределах полосы (±30px)
        float laneYWithVariation = laneY + Random.Range(-30f, 30f);
        
        Vector2 position = new Vector2(
            castleCenter.x + Mathf.Cos(angle) * spawnRadius * 100f, // Масштаб: 1 unit = 100px
            laneYWithVariation // Y координата из выбранной полосы
        );
        
        // Проверяем минимальное расстояние от игрока и крюка
        Vector2 safePosition = GetSafeSpawnPosition(position);
        
        monsterUI.Position = safePosition;
        // centerPosition должен быть в пикселях для Canvas (0,0 - центр Canvas)
        monsterUI.centerPosition = new Vector2(castleCenter.x * 100f, laneY); // Устанавливаем Y центр на полосе
        monsterUI.patrolRadius = spawnRadius;
        
        // Устанавливаем фиксированную Y позицию на полосе (но не блокируем, чтобы монстр мог двигаться в пределах полосы)
        monsterUI.fixedY = laneY;
        monsterUI.lockYPosition = false; // Не блокируем Y, чтобы монстр мог патрулировать по своей полосе
        
        // Убеждаемся что у монстра есть RectTransform и он правильно настроен
        RectTransform monsterRect = monster.GetComponent<RectTransform>();
        if (monsterRect == null)
        {
            monsterRect = monster.AddComponent<RectTransform>();
        }
        
        // Настраиваем якоря для правильного позиционирования на Canvas
        monsterRect.anchorMin = new Vector2(0.5f, 0f);
        monsterRect.anchorMax = new Vector2(0.5f, 0f);
        monsterRect.pivot = new Vector2(0.5f, 0.5f);
        monsterRect.anchoredPosition = position;
        
        Debug.Log($"Монстр создан: позиция={position}, центр={monsterUI.centerPosition}, радиус патруля={spawnRadius}");
        
        if (availableAnimators != null && availableAnimators.Length > 0)
        {
            int animIndex = Random.Range(0, availableAnimators.Length);
            monsterUI.SetAnimatorController(animIndex);
        }
        
        // Убеждаемся что монстр на Canvas
        if (parentCanvas != null && monster.transform.parent != parentCanvas.transform)
        {
            monster.transform.SetParent(parentCanvas.transform, false);
        }
        
        // Проверяем что монстр не дублируется в списке
        if (!activeMonsters.Contains(monster))
        {
            activeMonsters.Add(monster);
        }
        
        // Обновляем счетчики
        UpdateActiveCounts();
        
        string monsterTypeName = "Unknown";
        if (monsterTypes != null && monsterTypes.Length > 0)
        {
            var type = monsterTypes.FirstOrDefault(t => t.monsterPrefab == prefab);
            if (type != null) monsterTypeName = type.monsterTypeName;
        }
        
        Debug.Log($"Спавн монстра #{currentIndex + 1} (тип: {monsterTypeName}) на позиции {safePosition}, полоса {laneIndex + 1} (Y={laneY:F1}), всего активных: {activeMonsters.Count}");
        
        return monster;
    }
    
    /// <summary>
    /// Рисует визуализацию полос в редакторе
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showLaneGizmos || spawnLaneY == null || spawnLaneY.Length == 0) return;
        
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;
        
        Camera camera = canvas.worldCamera ?? Camera.main;
        if (camera == null) return;
        
        // Получаем размеры Canvas
        Vector2 canvasSize = canvasRect.sizeDelta;
        float canvasWidth = canvasSize.x > 0 ? canvasSize.x : Screen.width;
        
        // Конвертируем Canvas координаты в мировые для визуализации
        for (int i = 0; i < spawnLaneY.Length && i < 3; i++)
        {
            float laneY = spawnLaneY[i];
            
            // Определяем точки полосы в Canvas координатах
            Vector2 canvasPointLeft = new Vector2(-canvasWidth * 0.5f, laneY);
            Vector2 canvasPointRight = new Vector2(canvasWidth * 0.5f, laneY);
            Vector2 canvasPointCenter = new Vector2(castleCenter.x * 100f, laneY);
            
            // Конвертируем в мировые координаты
            Vector3 worldPointLeft = CanvasLocalToWorld(canvas, canvasPointLeft, camera);
            Vector3 worldPointRight = CanvasLocalToWorld(canvas, canvasPointRight, camera);
            Vector3 worldPointCenter = CanvasLocalToWorld(canvas, canvasPointCenter, camera);
            
            // Рисуем линию полосы
            Color lineColor = i == 0 ? new Color(1f, 0f, 0f, 0.7f) : 
                              i == 1 ? new Color(0f, 1f, 0f, 0.7f) : 
                              new Color(0f, 0f, 1f, 0.7f);
            Gizmos.color = lineColor;
            Gizmos.DrawLine(worldPointLeft, worldPointRight);
            
            // Рисуем точки на концах полосы
            Gizmos.DrawSphere(worldPointLeft, 0.15f);
            Gizmos.DrawSphere(worldPointRight, 0.15f);
            
            // Рисуем центральную точку полосы (у замка) - более яркая
            Color centerColor = i == 0 ? Color.red : i == 1 ? Color.green : Color.blue;
            Gizmos.color = centerColor;
            Gizmos.DrawSphere(worldPointCenter, 0.25f);
            
            // Рисуем маркер номера полосы (текст через Handles, но проще через сферу побольше)
            Gizmos.color = new Color(centerColor.r, centerColor.g, centerColor.b, 1f);
            Gizmos.DrawWireSphere(worldPointCenter, 0.35f);
        }
    }
    
    /// <summary>
    /// Конвертирует локальную координату Canvas в мировую координату для визуализации
    /// </summary>
    Vector3 CanvasLocalToWorld(Canvas canvas, Vector2 localPosition, Camera camera)
    {
        if (canvas == null || camera == null) return Vector3.zero;
        
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return Vector3.zero;
        
        // Конвертируем локальную позицию Canvas в мировую позицию
        // Для Screen Space - Overlay используем прямую конвертацию
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Для Screen Space Overlay конвертируем через экранные координаты
            Vector2 screenPos = new Vector2(
                Screen.width * 0.5f + localPosition.x,
                Screen.height * 0.5f + localPosition.y
            );
            Vector3 worldPos = camera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
            return worldPos;
        }
        else
        {
            // Для Screen Space - Camera или World Space используем RectTransform
            Vector3[] corners = new Vector3[4];
            canvasRect.GetWorldCorners(corners);
            
            // Получаем размеры Canvas в мировых единицах
            float canvasWidth = Vector3.Distance(corners[0], corners[3]);
            float canvasHeight = Vector3.Distance(corners[0], corners[1]);
            
            // Конвертируем localPosition в мировую позицию относительно углов Canvas
            float normalizedX = (localPosition.x / canvasRect.sizeDelta.x) + 0.5f;
            float normalizedY = (localPosition.y / canvasRect.sizeDelta.y) + 0.5f;
            
            Vector3 worldPos = Vector3.Lerp(
                Vector3.Lerp(corners[0], corners[1], normalizedY),
                Vector3.Lerp(corners[3], corners[2], normalizedY),
                normalizedX
            );
            
            return worldPos;
        }
    }
    
    /// <summary>
    /// Получает безопасную позицию для спавна, избегая игрока и крюка
    /// </summary>
    Vector2 GetSafeSpawnPosition(Vector2 originalPosition)
    {
        const float minDistanceFromPlayer = 300f; // Минимальное расстояние от игрока в пикселях
        const float minDistanceFromHook = 200f; // Минимальное расстояние от крюка в пикселях
        const int maxAttempts = 10; // Максимальное количество попыток найти безопасную позицию
        
        Vector2 safePos = originalPosition;
        
        // Получаем позиции игрока и крюка в координатах Canvas
        Vector2 playerPos = Vector2.zero;
        Vector2 hookPos = Vector2.zero;
        
        // Ищем игрока (может быть UI или SpriteRenderer)
        CastlePlayerUI player = FindObjectOfType<CastlePlayerUI>();
        if (player != null)
        {
            // Проверяем наличие SpriteRenderer (игрок использует SpriteRenderer, а не UI)
            SpriteRenderer playerSpriteRenderer = player.GetComponent<SpriteRenderer>();
            if (playerSpriteRenderer != null)
            {
                // Игрок использует SpriteRenderer - конвертируем мировые координаты в Canvas координаты
                Camera mainCam = Camera.main;
                if (mainCam != null && parentCanvas != null)
                {
                    Vector3 worldPos = player.transform.position;
                    Vector2 screenPos = mainCam.WorldToScreenPoint(worldPos);
                    RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, screenPos, parentCanvas.worldCamera ?? mainCam, out playerPos);
                }
            }
            else
            {
                // Игрок использует UI (RectTransform) - но CastlePlayerUI всегда использует SpriteRenderer теперь
                // Этот код оставлен на случай, если где-то еще используется UI версия
                RectTransform playerRect = player.GetComponent<RectTransform>();
                if (playerRect != null)
                {
                    playerPos = playerRect.anchoredPosition;
                }
                else
                {
                    // Если нет RectTransform, значит это SpriteRenderer версия
                    // Конвертируем мировые координаты
                    Camera mainCam = Camera.main;
                    if (mainCam != null && parentCanvas != null)
                    {
                        Vector3 worldPos = player.transform.position;
                        Vector2 screenPos = mainCam.WorldToScreenPoint(worldPos);
                        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            canvasRect, screenPos, parentCanvas.worldCamera ?? mainCam, out playerPos);
                    }
                }
            }
        }
        
        // Ищем крюк (UI версия)
        HookUI hook = FindObjectOfType<HookUI>();
        if (hook != null)
        {
            RectTransform hookRect = hook.GetComponent<RectTransform>();
            if (hookRect != null)
            {
                hookPos = hookRect.anchoredPosition;
            }
        }
        
        // Проверяем расстояние и корректируем если нужно
        float distanceToPlayer = Vector2.Distance(safePos, playerPos);
        float distanceToHook = Vector2.Distance(safePos, hookPos);
        
        int attempts = 0;
        while ((distanceToPlayer < minDistanceFromPlayer || distanceToHook < minDistanceFromHook) && attempts < maxAttempts)
        {
            // Сдвигаем позицию дальше от игрока/крюка
            Vector2 directionAway = Vector2.zero;
            
            if (distanceToPlayer < minDistanceFromPlayer)
            {
                Vector2 dirFromPlayer = (safePos - playerPos).normalized;
                if (dirFromPlayer.magnitude < 0.1f)
                {
                    // Если позиция слишком близко или совпадает, выбираем случайное направление
                    float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    dirFromPlayer = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
                }
                directionAway += dirFromPlayer * (minDistanceFromPlayer - distanceToPlayer);
            }
            
            if (distanceToHook < minDistanceFromHook)
            {
                Vector2 dirFromHook = (safePos - hookPos).normalized;
                if (dirFromHook.magnitude < 0.1f)
                {
                    float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    dirFromHook = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
                }
                directionAway += dirFromHook * (minDistanceFromHook - distanceToHook);
            }
            
            safePos += directionAway;
            
            // Обновляем расстояния
            distanceToPlayer = Vector2.Distance(safePos, playerPos);
            distanceToHook = Vector2.Distance(safePos, hookPos);
            attempts++;
        }
        
        if (attempts > 0)
        {
            Debug.Log($"Скороректирована позиция монстра: было {originalPosition}, стало {safePos} (попыток: {attempts})");
        }
        
        return safePos;
    }
    
    /// <summary>
    /// Создает монстра из указанного префаба
    /// </summary>
    GameObject CreateMonsterFromPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            // Если префаб не указан, создаем монстра программно
            GameObject mons = new GameObject("Monster");
            MonsterUI monUI = mons.AddComponent<MonsterUI>();
            monUI.SetupMonsterComponents();
            
            if (parentCanvas != null)
            {
                mons.transform.SetParent(parentCanvas.transform, false);
            }
            
            return mons;
        }
        
        // Создаем из префаба (сохраняем масштаб из префаба)
        var monster = Instantiate(prefab);
        
        // Сохраняем масштаб из префаба
        Vector3 prefabScale = prefab.transform.localScale;
        monster.transform.localScale = prefabScale; // Всегда используем масштаб из префаба
        
        // Если префаб не настроен, автоматически настраиваем
        MonsterUI monsterUI = monster.GetComponent<MonsterUI>();
        if (monsterUI == null)
        {
            monsterUI = monster.AddComponent<MonsterUI>();
        }
        
        // Сохраняем масштаб в контроллере монстра для правильного отражения
        monsterUI.SetOriginalScale(prefabScale);
        
        // Автоматически настраиваем компоненты если нужно
        monsterUI.SetupMonsterComponents();
        
        // Убеждаемся что монстр на Canvas
        if (parentCanvas != null)
        {
            monster.transform.SetParent(parentCanvas.transform, false);
            // После SetParent нужно снова установить масштаб из префаба
            monster.transform.localScale = prefabScale;
        }
        
        return monster;
    }
    
    /// <summary>
    /// Возвращает монстра в соответствующий пул
    /// </summary>
    public void ReturnMonster(GameObject monster)
    {
        if (monster == null) return;
        
        activeMonsters.Remove(monster);
        
        // Находим тип монстра и возвращаем в соответствующий пул
        if (monsterTypeMap.ContainsKey(monster))
        {
            GameObject prefab = monsterTypeMap[monster];
            
            if (monsterPools.ContainsKey(prefab))
            {
                monsterPools[prefab].Enqueue(monster);
            }
            else
            {
                // Создаем новый пул если его нет
                Queue<GameObject> newPool = new Queue<GameObject>();
                newPool.Enqueue(monster);
                monsterPools[prefab] = newPool;
            }
            
            monsterTypeMap.Remove(monster);
        }
        else
        {
            // Если тип не определен, уничтожаем монстра
            Debug.LogWarning($"ReturnMonster: не найден тип монстра для {monster.name}, уничтожаю");
            Destroy(monster);
            StartCoroutine(SpawnMonsterDelayed());
            return;
        }
        
        monster.SetActive(false);
        
        // Обновляем счетчик активных монстров для типа
        UpdateActiveCounts();
        
        StartCoroutine(SpawnMonsterDelayed());
    }
    
    /// <summary>
    /// Обновляет счетчики активных монстров для каждого типа
    /// </summary>
    void UpdateActiveCounts()
    {
        if (monsterTypes == null) return;
        
        foreach (var type in monsterTypes)
        {
            type.activeCount = activeMonsters.Count(m => 
                m != null && monsterTypeMap.ContainsKey(m) && monsterTypeMap[m] == type.monsterPrefab);
        }
    }
    
    System.Collections.IEnumerator SpawnMonsterDelayed()
    {
        yield return new WaitForSeconds(1f);
        SpawnMonster();
    }
    
    [ContextMenu("Cycle Monster Animations")]
    public void CycleMonsterAnimations()
    {
        if (availableAnimators == null || availableAnimators.Length <= 1) return;
        
        foreach (var monster in activeMonsters)
        {
            if (monster == null || !monster.activeSelf) continue;
            
            MonsterUI monsterUI = monster.GetComponent<MonsterUI>();
            if (monsterUI != null && !monsterUI.IsDead)
            {
                int newIndex = (monsterUI.currentAnimatorIndex + 1) % availableAnimators.Length;
                monsterUI.SetAnimatorController(newIndex);
            }
        }
    }
    
    /// <summary>
    /// Получает информацию о состоянии пулов (для отладки)
    /// </summary>
    [ContextMenu("Print Pool Status")]
    public void PrintPoolStatus()
    {
        Debug.Log("=== Статус пулов монстров ===");
        Debug.Log($"Всего активных монстров: {activeMonsters.Count}");
        Debug.Log($"Всего монстров в пулах: {monsterPools.Values.Sum(p => p.Count)}");
        
        if (monsterTypes != null && monsterTypes.Length > 0)
        {
            foreach (var type in monsterTypes)
            {
                if (type == null || type.monsterPrefab == null) continue;
                
                int inPool = 0;
                if (monsterPools.ContainsKey(type.monsterPrefab))
                {
                    inPool = monsterPools[type.monsterPrefab].Count;
                }
                
                Debug.Log($"{type.monsterTypeName}: активных={type.activeCount}, в пуле={inPool}, вес={type.spawnWeight}");
            }
        }
    }
}

