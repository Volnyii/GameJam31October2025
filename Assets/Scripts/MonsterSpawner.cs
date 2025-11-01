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
    
    [Header("Monster Pool System")]
    [Tooltip("Массив типов монстров (можно задать разные префабы)")]
    public MonsterPoolData[] monsterTypes = new MonsterPoolData[0];
    
    [Header("Legacy Support (для обратной совместимости)")]
    [Tooltip("Старый способ - один префаб (если массив типов пуст)")]
    public GameObject monsterPrefab;
    
    [Header("Monster Pools")]
    [Tooltip("Доступные аниматоры (применяются ко всем типам монстров)")]
    public RuntimeAnimatorController[] availableAnimators;
    
    private List<GameObject> activeMonsters = new List<GameObject>();
    
    // Пул для каждого типа монстра отдельно
    private Dictionary<GameObject, Queue<GameObject>> monsterPools = new Dictionary<GameObject, Queue<GameObject>>();
    
    // Пул для программно созданных монстров (когда prefab == null)
    private Queue<GameObject> programmaticMonsterPool = new Queue<GameObject>();
    
    // Словарь для быстрого поиска типа монстра по GameObject
    private Dictionary<GameObject, GameObject> monsterTypeMap = new Dictionary<GameObject, GameObject>();
    
    public List<GameObject> ActiveMonsters => activeMonsters;
    
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
            // Если нет, используем старый способ с одним префабом
            if (monsterPrefab != null)
            {
                Queue<GameObject> pool = new Queue<GameObject>();
                monsterPools[monsterPrefab] = pool;
                
                // Создаем предварительные объекты для пула
                for (int i = 0; i < 3; i++)
                {
                    GameObject precreated = CreateMonsterFromPrefab(monsterPrefab);
                    // Убеждаемся что масштаб из префаба сохранен
                    precreated.transform.localScale = monsterPrefab.transform.localScale;
                    precreated.SetActive(false);
                    pool.Enqueue(precreated);
                }
            }
            else
            {
                // Если префаба нет, создаем программно созданных монстров для пула
                for (int i = 0; i < 3; i++)
                {
                    GameObject precreated = CreateMonsterFromPrefab(null);
                    precreated.SetActive(false);
                    programmaticMonsterPool.Enqueue(precreated);
                }
                Debug.Log("✓ Пул программно созданных монстров инициализирован: 3 объекта");
            }
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
        
        // Иначе используем старый способ (может быть null - это нормально)
        return monsterPrefab;
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
        else if (prefab == null && programmaticMonsterPool.Count > 0)
        {
            // Берем программно созданного монстра из пула
            monster = programmaticMonsterPool.Dequeue();
            monster.SetActive(true);
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
        float angleStep = 360f / monsterCount;
        int currentIndex = activeMonsters.Count;
        float angle = (angleStep * currentIndex + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
        
        Vector3 position = new Vector3(
            castleCenter.x + Mathf.Cos(angle) * spawnRadius,
            groundLevel + Mathf.Sin(angle) * spawnRadius * 0.3f,
            0
        );
        
        monsterController.transform.position = position;
        monsterController.centerPosition = castleCenter;
        monsterController.patrolRadius = spawnRadius;
        
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
        
        Debug.Log($"Спавн монстра #{currentIndex + 1} (тип: {monsterTypeName}) на позиции {position}, всего активных: {activeMonsters.Count}");
        
        return monster;
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
            // Если тип не определен, пробуем найти по старому способу
            if (monsterPrefab != null)
            {
                if (!monsterPools.ContainsKey(monsterPrefab))
                {
                    monsterPools[monsterPrefab] = new Queue<GameObject>();
                }
                monsterPools[monsterPrefab].Enqueue(monster);
            }
            else
            {
                // Если нет prefab, возвращаем в пул программно созданных
                programmaticMonsterPool.Enqueue(monster);
            }
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
