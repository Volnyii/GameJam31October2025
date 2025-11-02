using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Быстрая настройка системы пула монстров
/// Добавьте на объект со MonsterSpawnerUI для упрощенной настройки
/// </summary>
public class MonsterPoolSetupHelper : MonoBehaviour
{
    [Header("Быстрая настройка")]
    [Tooltip("Префабы монстров для быстрой настройки (автоматически создаст массив типов)")]
    public GameObject[] monsterPrefabs = new GameObject[0];
    
    [Tooltip("Использовать одинаковый вес для всех типов?")]
    public bool useSameWeight = true;
    
    [Tooltip("Вес спавна (если useSameWeight = true)")]
    [Range(1, 10)]
    public int defaultWeight = 5;
    
    [Tooltip("Минимальный размер пула для каждого типа")]
    [Range(0, 20)]
    public int defaultPoolSize = 2;
    
    [ContextMenu("Автоматическая настройка из префабов")]
    public void AutoSetupFromPrefabs()
    {
        MonsterSpawnerUI spawner = GetComponent<MonsterSpawnerUI>();
        if (spawner == null)
        {
            Debug.LogError("MonsterSpawnerUI не найден на этом объекте!");
            return;
        }
        
        if (monsterPrefabs == null || monsterPrefabs.Length == 0)
        {
            Debug.LogWarning("Список префабов пуст! Добавьте префабы монстров в monsterPrefabs.");
            return;
        }
        
        // Фильтруем только валидные префабы
        var validPrefabs = monsterPrefabs.Where(p => p != null).ToArray();
        if (validPrefabs.Length == 0)
        {
            Debug.LogError("Не найдено валидных префабов!");
            return;
        }
        
        // Создаем массив типов
        spawner.monsterTypes = new MonsterPoolData[validPrefabs.Length];
        
        for (int i = 0; i < validPrefabs.Length; i++)
        {
            spawner.monsterTypes[i] = new MonsterPoolData
            {
                monsterPrefab = validPrefabs[i],
                monsterTypeName = validPrefabs[i].name,
                spawnWeight = useSameWeight ? defaultWeight : 5,
                poolMinSize = defaultPoolSize
            };
        }
        
        Debug.Log($"✓ Автоматически настроено {validPrefabs.Length} типов монстров из префабов");
        Debug.Log($"  Все типы имеют вес={defaultWeight}, размер пула={defaultPoolSize}");
    }
    
    [ContextMenu("Настроить веса равномерно")]
    public void SetupEqualWeights()
    {
        MonsterSpawnerUI spawner = GetComponent<MonsterSpawnerUI>();
        if (spawner == null || spawner.monsterTypes == null) return;
        
        int weightPerType = 100 / spawner.monsterTypes.Length;
        
        foreach (var type in spawner.monsterTypes)
        {
            if (type != null)
            {
                type.spawnWeight = Mathf.Clamp(weightPerType / 10, 1, 10);
            }
        }
        
        Debug.Log($"✓ Веса настроены равномерно: {weightPerType / 10} для каждого типа");
    }
    
    [ContextMenu("Обновить имена типов из префабов")]
    public void UpdateTypeNamesFromPrefabs()
    {
        MonsterSpawnerUI spawner = GetComponent<MonsterSpawnerUI>();
        if (spawner == null || spawner.monsterTypes == null) return;
        
        foreach (var type in spawner.monsterTypes)
        {
            if (type != null && type.monsterPrefab != null)
            {
                type.monsterTypeName = type.monsterPrefab.name;
            }
        }
        
        Debug.Log("✓ Имена типов обновлены из имен префабов");
    }
    
    [ContextMenu("Проверить настройку пулов")]
    public void ValidatePoolSetup()
    {
        MonsterSpawnerUI spawner = GetComponent<MonsterSpawnerUI>();
        if (spawner == null)
        {
            Debug.LogError("MonsterSpawnerUI не найден!");
            return;
        }
        
        Debug.Log("=== Проверка настройки пулов монстров ===");
        
        if (spawner.monsterTypes == null || spawner.monsterTypes.Length == 0)
        {
            Debug.LogError("❌ Массив типов монстров пуст! Добавьте хотя бы один MonsterPoolData через автоматическую настройку.");
            return;
        }
        
        int validTypes = 0;
        int invalidTypes = 0;
        int totalWeight = 0;
        
        foreach (var type in spawner.monsterTypes)
        {
            if (type == null)
            {
                invalidTypes++;
                Debug.LogWarning("⚠ Обнаружен null элемент в массиве типов");
                continue;
            }
            
            if (type.monsterPrefab == null)
            {
                invalidTypes++;
                Debug.LogWarning($"⚠ Тип '{type.monsterTypeName}' не имеет назначенного префаба!");
                continue;
            }
            
            validTypes++;
            totalWeight += type.spawnWeight;
            
            Debug.Log($"✓ {type.monsterTypeName}: префаб={type.monsterPrefab.name}, вес={type.spawnWeight}, мин.пул={type.poolMinSize}");
        }
        
        Debug.Log($"\nИтого: {validTypes} валидных, {invalidTypes} невалидных типов");
        Debug.Log($"Общий вес спавна: {totalWeight}");
        
        if (validTypes == 0)
        {
            Debug.LogError("❌ Нет валидных типов монстров!");
        }
        else if (invalidTypes > 0)
        {
            Debug.LogWarning($"⚠ Есть {invalidTypes} невалидных типов. Рекомендуется их исправить.");
        }
        else
        {
            Debug.Log("✓ Все типы настроены правильно!");
        }
    }
}


