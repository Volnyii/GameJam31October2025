using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Спавнер монстров со SpriteRenderer с поддержкой пула разных типов монстров
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }
    
    [Header("Settings")]
    [Tooltip("Общее количество монстров на сцене")]
    public int monsterCount = 5;
    [Tooltip("Радиус патрулирования вокруг замка (в мировых единицах)")]
    public float spawnRadius = 6f;
    [Tooltip("Центр замка (в мировых координатах)")]
    public Vector2 castleCenter = Vector2.zero;
    [Tooltip("Уровень земли (Y координата)")]
    public float groundLevel = 0f;
    
    [Header("Spawn Lanes (Полосы спавна монстров)")]
    [Tooltip("Y координаты трех полос для спавна монстров (в мировых координатах)")]
    public float[] spawnLaneY = new float[3] { -2f, 0f, 2f };
    [Tooltip("Показывать визуализацию полос в редакторе")]
    public bool showLaneGizmos = true;
    [Tooltip("Цвет визуализации полос (только в редакторе)")]
    public Color laneGizmoColor = new Color(1f, 0f, 0f, 0.5f);
    [Tooltip("Длина линии визуализации полосы")]
    public float laneGizmoLength = 20f;
    
    [Header("Monster Pool System")]
    [Tooltip("Массив типов монстров (можно задать разные префабы)")]
    public MonsterPoolData[] monsterTypes = new MonsterPoolData[0];
    
    [Header("Monster Pools")]
    [Tooltip("Доступные аниматоры (применяются ко всем типам монстров)")]
    public RuntimeAnimatorController[] availableAnimators;
    
    private List<GameObject> activeMonsters = new List<GameObject>();
    
    // Пул для каждого типа монстра отдельно
    private Dictionary<GameObject, Queue<GameObject>> monsterPools = new Dictionary<GameObject, Queue<GameObject>>();
    
    // Словарь для быстрого поиска типа монстра по GameObject
    private Dictionary<GameObject, GameObject> monsterTypeMap = new Dictionary<GameObject, GameObject>();
    
    // Флаг блокировки спавна (для победы)
    private bool isSpawningBlocked = false;
    
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
                    newLanes[i] = groundLevel + (i - 1) * 2f;
                }
            }
            else
            {
                // Дефолтные значения (относительно groundLevel)
                newLanes = new float[3] { groundLevel - 2f, groundLevel, groundLevel + 2f };
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
            Debug.LogWarning("MonsterSpawner: массив monsterTypes пуст! Добавьте хотя бы один MonsterPoolData.");
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
    /// Возвращает null если нет префабов (тогда монстр создается программно)
    /// </summary>
    GameObject GetRandomMonsterPrefab()
    {
        // Если есть массив типов, используем его
        if (monsterTypes != null && monsterTypes.Length > 0)
        {
            // Фильтруем валидные типы
            var validTypes = monsterTypes.Where(t => t != null && t.monsterPrefab != null).ToArray();
            if (validTypes.Length == 0) return null; // Нет префабов - создаем программно
            
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
        // prefab может быть null - это нормально, монстр создастся программно
        
        GameObject monster = null;
        MonsterController monsterController = null;
        
        // Пытаемся взять из пула
        if (prefab != null && monsterPools.ContainsKey(prefab))
        {
            Queue<GameObject> pool = monsterPools[prefab];
            if (pool.Count > 0)
            {
                monster = pool.Dequeue();
                // Восстанавливаем масштаб из префаба при активации из пула
                Vector3 prefabScale = prefab.transform.localScale;
                monster.transform.localScale = prefabScale; // Всегда восстанавливаем масштаб из префаба
                
                // Обновляем originalScale в контроллере
                monsterController = monster.GetComponent<MonsterController>();
                if (monsterController != null)
                {
                    monsterController.SetOriginalScale(prefabScale);
                }
                
                monster.SetActive(true);
            }
        }
        
        // Если в пуле нет, создаем новый
        if (monster == null)
        {
            monster = CreateMonsterFromPrefab(prefab);
        }
        
        // Сохраняем связь монстра с его типом (только если есть prefab)
        if (prefab != null && !monsterTypeMap.ContainsKey(monster))
        {
            monsterTypeMap[monster] = prefab;
        }
        
        // Получаем или создаем контроллер монстра
        if (monsterController == null)
        {
            monsterController = monster.GetComponent<MonsterController>();
            if (monsterController == null)
            {
                monsterController = monster.AddComponent<MonsterController>();
            }
        }
        
        // Устанавливаем позицию на окружности вокруг замка (в мировых координатах)
        // Монстры распределяются между тремя полосами по Y
        float angleStep = 360f / monsterCount;
        int currentIndex = activeMonsters.Count;
        float angle = (angleStep * currentIndex + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
        
        // Выбираем полосу для этого монстра (равномерное распределение)
        // Убеждаемся, что массив полос инициализирован
        if (spawnLaneY == null || spawnLaneY.Length == 0)
        {
            // Если массив не инициализирован, используем дефолтные значения
            spawnLaneY = new float[3] { groundLevel - 2f, groundLevel, groundLevel + 2f };
        }
        
        int laneIndex = currentIndex % spawnLaneY.Length;
        float laneY = spawnLaneY[laneIndex];
        
        // Добавляем небольшую случайную вариацию по Y в пределах полосы (±0.3 единицы)
        float laneYWithVariation = laneY + Random.Range(-0.3f, 0.3f);
        
        Vector3 position = new Vector3(
            castleCenter.x + Mathf.Cos(angle) * spawnRadius,
            laneYWithVariation,
            0
        );
        
        // Проверяем минимальное расстояние от игрока и крюка
        Vector3 safePosition = GetSafeSpawnPosition(position);
        
        monsterController.transform.position = safePosition;
        // Устанавливаем Y центр на полосе
        monsterController.centerPosition = new Vector2(castleCenter.x, laneY);
        monsterController.patrolRadius = spawnRadius;
        
        // Устанавливаем фиксированную Y позицию на полосе (но не блокируем, чтобы монстр мог двигаться в пределах полосы)
        monsterController.fixedY = laneY;
        monsterController.lockYPosition = false; // Не блокируем Y, чтобы монстр мог патрулировать по своей полосе
        
        if (availableAnimators != null && availableAnimators.Length > 0)
        {
            int animIndex = Random.Range(0, availableAnimators.Length);
            monsterController.SetAnimatorController(animIndex);
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
        
        Debug.Log($"Спавн монстра #{currentIndex + 1} (тип: {monsterTypeName}) на позиции {safePosition}, полоса {laneIndex + 1} (Y={laneY:F2}), всего активных: {activeMonsters.Count}");
        
        return monster;
    }
    
    /// <summary>
    /// Рисует визуализацию полос в редакторе
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showLaneGizmos || spawnLaneY == null || spawnLaneY.Length == 0) return;
        
        // Рисуем линии полос
        for (int i = 0; i < spawnLaneY.Length && i < 3; i++)
        {
            float laneY = spawnLaneY[i];
            
            // Определяем точки полосы (горизонтальные линии)
            Vector3 leftPoint = new Vector3(castleCenter.x - laneGizmoLength * 0.5f, laneY, 0);
            Vector3 rightPoint = new Vector3(castleCenter.x + laneGizmoLength * 0.5f, laneY, 0);
            Vector3 centerPoint = new Vector3(castleCenter.x, laneY, 0);
            
            // Рисуем линию полосы
            Color lineColor = i == 0 ? new Color(1f, 0f, 0f, 0.7f) : 
                              i == 1 ? new Color(0f, 1f, 0f, 0.7f) : 
                              new Color(0f, 0f, 1f, 0.7f);
            Gizmos.color = lineColor;
            Gizmos.DrawLine(leftPoint, rightPoint);
            
            // Рисуем точки на концах полосы
            Gizmos.DrawSphere(leftPoint, 0.15f);
            Gizmos.DrawSphere(rightPoint, 0.15f);
            
            // Рисуем центральную точку полосы (у замка) - более яркая
            Color centerColor = i == 0 ? Color.red : i == 1 ? Color.green : Color.blue;
            Gizmos.color = centerColor;
            Gizmos.DrawSphere(centerPoint, 0.25f);
            
            // Рисуем маркер номера полосы
            Gizmos.color = new Color(centerColor.r, centerColor.g, centerColor.b, 1f);
            Gizmos.DrawWireSphere(centerPoint, 0.35f);
        }
    }
    
    /// <summary>
    /// Получает безопасную позицию для спавна, избегая игрока и крюка
    /// </summary>
    Vector3 GetSafeSpawnPosition(Vector3 originalPosition)
    {
        const float minDistanceFromPlayer = 3f; // Минимальное расстояние от игрока в мировых единицах
        const float minDistanceFromHook = 2f; // Минимальное расстояние от крюка в мировых единицах
        const int maxAttempts = 10; // Максимальное количество попыток найти безопасную позицию
        
        Vector3 safePos = originalPosition;
        
        // Получаем позиции игрока и крюка
        Vector3 playerPos = Vector3.zero;
        Vector3 hookPos = Vector3.zero;
        
        // Ищем игрока (SpriteRenderer версия)
        CastlePlayerUI player = FindObjectOfType<CastlePlayerUI>();
        if (player != null)
        {
            SpriteRenderer playerSpriteRenderer = player.GetComponent<SpriteRenderer>();
            if (playerSpriteRenderer != null)
            {
                playerPos = player.transform.position;
            }
        }
        
        // Ищем крюк (SpriteRenderer версия)
        HookController hook = FindObjectOfType<HookController>();
        if (hook != null)
        {
            hookPos = hook.transform.position;
        }
        
        // Проверяем расстояние и корректируем если нужно
        float distanceToPlayer = Vector3.Distance(safePos, playerPos);
        float distanceToHook = Vector3.Distance(safePos, hookPos);
        
        int attempts = 0;
        while ((distanceToPlayer < minDistanceFromPlayer || distanceToHook < minDistanceFromHook) && attempts < maxAttempts)
        {
            // Сдвигаем позицию дальше от игрока/крюка
            Vector3 directionAway = Vector3.zero;
            
            if (distanceToPlayer < minDistanceFromPlayer)
            {
                Vector3 dirFromPlayer = (safePos - playerPos).normalized;
                if (dirFromPlayer.magnitude < 0.1f)
                {
                    // Если позиция слишком близко или совпадает, выбираем случайное направление
                    float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    dirFromPlayer = new Vector3(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle), 0);
                }
                directionAway += dirFromPlayer * (minDistanceFromPlayer - distanceToPlayer);
            }
            
            if (distanceToHook < minDistanceFromHook)
            {
                Vector3 dirFromHook = (safePos - hookPos).normalized;
                if (dirFromHook.magnitude < 0.1f)
                {
                    float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    dirFromHook = new Vector3(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle), 0);
                }
                directionAway += dirFromHook * (minDistanceFromHook - distanceToHook);
            }
            
            safePos += directionAway;
            
            // Обновляем расстояния
            distanceToPlayer = Vector3.Distance(safePos, playerPos);
            distanceToHook = Vector3.Distance(safePos, hookPos);
            attempts++;
        }
        
        if (attempts > 0)
        {
            Debug.Log($"Скорректирована позиция монстра (SpriteRenderer): было {originalPosition}, стало {safePos} (попыток: {attempts})");
        }
        
        return safePos;
    }
    
    /// <summary>
    /// Создает монстра из указанного префаба или программно если префаб null
    /// </summary>
    GameObject CreateMonsterFromPrefab(GameObject prefab)
    {
        GameObject monster;
        
        if (prefab == null)
        {
            // Если префаб не указан, создаем монстра программно
            monster = new GameObject("Monster");
            MonsterController monController = monster.AddComponent<MonsterController>();
            monController.SetupMonsterComponents();
            
            // Настраиваем базовые параметры
            monController.centerPosition = castleCenter;
            monController.patrolRadius = spawnRadius;
            monController.enableScreenWrap = true;
            
            Debug.Log("  Монстр создан программно (без префаба)");
            return monster;
        }
        
        // Создаем из префаба (сохраняем масштаб из префаба)
        monster = Instantiate(prefab);
        monster.name = "Monster";
        
        // Сохраняем масштаб из префаба (если он был установлен)
        Vector3 prefabScale = prefab.transform.localScale;
        if (prefabScale != Vector3.one)
        {
            monster.transform.localScale = prefabScale;
        }
        
        // Если префаб не настроен, автоматически настраиваем
        MonsterController monsterController = monster.GetComponent<MonsterController>();
        if (monsterController == null)
        {
            monsterController = monster.AddComponent<MonsterController>();
        }
        
        // Сохраняем масштаб в контроллере монстра для правильного отражения
        monsterController.SetOriginalScale(prefabScale);
        
        // Автоматически настраиваем компоненты если нужно
        monsterController.SetupMonsterComponents();
        
        // Настраиваем базовые параметры
        monsterController.centerPosition = castleCenter;
        monsterController.patrolRadius = spawnRadius;
        monsterController.enableScreenWrap = true;
        
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
            if (!isSpawningBlocked)
            {
                StartCoroutine(SpawnMonsterDelayed());
            }
            return;
        }
        
        monster.SetActive(false);
        
        // Обновляем счетчик активных монстров для типа
        UpdateActiveCounts();
        
        // Не спавним нового монстра если спавн заблокирован
        if (!isSpawningBlocked)
        {
            StartCoroutine(SpawnMonsterDelayed());
        }
    }
    
    /// <summary>
    /// Останавливает спавн новых монстров
    /// </summary>
    public void StopSpawning()
    {
        isSpawningBlocked = true;
        Debug.Log("MonsterSpawner: Спавн остановлен");
    }
    
    /// <summary>
    /// Возобновляет спавн новых монстров
    /// </summary>
    public void ResumeSpawning()
    {
        isSpawningBlocked = false;
        Debug.Log("MonsterSpawner: Спавн возобновлен");
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
        if (!isSpawningBlocked)
        {
            SpawnMonster();
        }
    }
    
    [ContextMenu("Cycle Monster Animations")]
    public void CycleMonsterAnimations()
    {
        if (availableAnimators == null || availableAnimators.Length <= 1) return;
        
        foreach (var monster in activeMonsters)
        {
            if (monster == null || !monster.activeSelf) continue;
            
            MonsterController monsterController = monster.GetComponent<MonsterController>();
            if (monsterController != null && !monsterController.IsDead)
            {
                int newIndex = (monsterController.currentAnimatorIndex + 1) % availableAnimators.Length;
                monsterController.SetAnimatorController(newIndex);
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
