using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Управление UI игры: фон, HUD, счет
/// </summary>
public class CastleUIManager : MonoBehaviour
{
    public static CastleUIManager Instance { get; private set; }
    
    [Header("UI References")]
    public Canvas canvas;
    public Image backgroundImage;
    [Tooltip("Ссылка на VictoryScreen компонент (найдет автоматически если не назначен)")]
    public VictoryScreen victoryScreen;
    
    [Header("Settings")]
    public Color backgroundColor = new Color(0.2f, 0.4f, 0.6f);
    
    private int monstersKilled = 0;
    private int totalMonstersToKill = 0;
    private bool isVictoryAchieved = false;
    
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
        // Находим VictoryScreen если не назначен
        if (victoryScreen == null)
        {
            victoryScreen = FindObjectOfType<VictoryScreen>();
            if (victoryScreen == null)
            {
                Debug.LogWarning("CastleUIManager: VictoryScreen не найден! Добавьте компонент VictoryScreen на сцену.");
            }
            else
            {
                Debug.Log("CastleUIManager: VictoryScreen найден автоматически.");
            }
        }
        
        // Обновляем общее количество монстров
        UpdateTotalMonstersCount();
    }
    
    /// <summary>
    /// Обновляет общее количество монстров для победы
    /// </summary>
    void UpdateTotalMonstersCount()
    {
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (spawner != null)
        {
            totalMonstersToKill = spawner.monsterCount;
            Debug.Log($"CastleUIManager: Общее количество монстров для победы = {totalMonstersToKill}");
            return;
        }
        
        MonsterSpawnerUI spawnerUI = MonsterSpawnerUI.Instance;
        if (spawnerUI != null)
        {
            totalMonstersToKill = spawnerUI.monsterCount;
            Debug.Log($"CastleUIManager: Общее количество монстров для победы = {totalMonstersToKill}");
            return;
        }
        
        Debug.LogWarning("CastleUIManager: Не найден MonsterSpawner или MonsterSpawnerUI! Невозможно определить общее количество монстров.");
    }
    
    /// <summary>
    /// Обновляет счет убитых монстров
    /// </summary>
    public void OnMonsterKilled()
    {
        if (isVictoryAchieved) return;
        
        monstersKilled++;
        Debug.Log($"CastleUIManager: Монстр убит! Всего убито: {monstersKilled}/{totalMonstersToKill}");
        
        CheckVictory();
    }
    
    /// <summary>
    /// Проверяет условие победы
    /// </summary>
    public void CheckVictory()
    {
        if (isVictoryAchieved) return;
        
        // Обновляем общее количество на случай изменения
        UpdateTotalMonstersCount();
        
        if (monstersKilled >= totalMonstersToKill && totalMonstersToKill > 0)
        {
            isVictoryAchieved = true;
            BlockMonsterSpawning();
            ShowVictory();
        }
    }
    
    /// <summary>
    /// Блокирует спавн монстров после победы
    /// </summary>
    void BlockMonsterSpawning()
    {
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (spawner != null)
        {
            spawner.StopSpawning();
        }
        
        MonsterSpawnerUI spawnerUI = MonsterSpawnerUI.Instance;
        if (spawnerUI != null)
        {
            spawnerUI.StopSpawning();
        }
    }
    
    /// <summary>
    /// Показывает экран победы
    /// </summary>
    void ShowVictory()
    {
        if (victoryScreen == null)
        {
            Debug.LogError("CastleUIManager: VictoryScreen не найден! Невозможно показать экран победы.");
            return;
        }
        
        Debug.Log("CastleUIManager: Условие победы выполнено! Показываю экран победы...");
        victoryScreen.ShowVictory();
    }
    
    /// <summary>
    /// Скрывает экран победы
    /// </summary>
    void HideVictory()
    {
        if (victoryScreen != null)
        {
            victoryScreen.HideVictory();
        }
    }
    
    /// <summary>
    /// Сбрасывает счет и возобновляет игру
    /// </summary>
    public void ResetScore()
    {
        monstersKilled = 0;
        isVictoryAchieved = false;
        
        // Возобновляем спавн монстров
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (spawner != null)
        {
            spawner.ResumeSpawning();
        }
        
        MonsterSpawnerUI spawnerUI = MonsterSpawnerUI.Instance;
        if (spawnerUI != null)
        {
            spawnerUI.ResumeSpawning();
        }
        
        HideVictory();
        
        // Обновляем общее количество монстров
        UpdateTotalMonstersCount();
    }
}


