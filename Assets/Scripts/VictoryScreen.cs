using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран победы - показывает картинку при победе над всеми монстрами
/// При клике открывает страницу игры Hero Wars в магазине приложений
/// Работает только через Canvas UI
/// </summary>
public class VictoryScreen : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Панель победы (должна быть настроена в Canvas вручную)")]
    public GameObject victoryPanel;
    [Tooltip("Canvas для UI элементов")]
    public Canvas canvas;
    [Tooltip("Спрайт для картинки победы")]
    public Sprite victorySprite;
    
    [Header("Store Links")]
    [Tooltip("Ссылка на Google Play Store (Android) - Hero Wars")]
    public string androidStoreLink = "https://play.google.com/store/apps/details?id=com.nexters.herowars";
    [Tooltip("Ссылка на App Store (iOS) - Hero Wars")]
    public string iosStoreLink = "https://apps.apple.com/app/hero-wars/id1158967485";
    
    private Button victoryButton;
    private Image victoryImage;
    private bool isVictoryShown = false;
    
    void Awake()
    {
        // Настраиваем компоненты панели если она назначена
        if (victoryPanel != null)
        {
            SetupPanelComponents();
        }
        else
        {
            Debug.LogWarning("VictoryScreen: victoryPanel не назначен! Назначьте панель в инспекторе.");
        }
    }
    
    /// <summary>
    /// Настраивает компоненты панели (Button и Image) - настраивает только если их нет
    /// </summary>
    void SetupPanelComponents()
    {
        if (victoryPanel == null) return;
        
        // Получаем Button - не создаем, только настраиваем если есть
        victoryButton = victoryPanel.GetComponent<Button>();
        if (victoryButton != null)
        {
            // Настраиваем обработчик клика
            victoryButton.onClick.RemoveAllListeners();
            victoryButton.onClick.AddListener(OnVictoryClick);
        }
        else
        {
            Debug.LogWarning("VictoryScreen: На панели нет компонента Button! Добавьте Button в инспекторе.");
        }
        
        // Получаем Image - не создаем, только используем если есть
        victoryImage = victoryPanel.GetComponent<Image>();
        
        // Назначаем спрайт если он есть и Image есть
        if (victoryImage != null && victorySprite != null && victoryImage.sprite == null)
        {
            victoryImage.sprite = victorySprite;
        }
    }
    /// <summary>
    /// Показывает экран победы
    /// </summary>
    public void ShowVictory()
    {
        if (isVictoryShown)
        {
            Debug.LogWarning("VictoryScreen: Экран победы уже показан!");
            return;
        }
        
        if (victoryPanel == null)
        {
            Debug.LogError("VictoryScreen: victoryPanel не назначен! Назначьте панель в инспекторе.");
            return;
        }
        
        // Убеждаемся что компоненты настроены
        SetupPanelComponents();
        
        // Назначаем спрайт если он есть
        if (victorySprite != null && victoryImage != null && victoryImage.sprite == null)
        {
            victoryImage.sprite = victorySprite;
        }
        
        isVictoryShown = true;
        victoryPanel.SetActive(true);
        
        Debug.Log($"Экран победы показан! victoryPanel.activeSelf={victoryPanel.activeSelf}, isVictoryShown={isVictoryShown}");
    }
    
    /// <summary>
    /// Скрывает экран победы
    /// </summary>
    public void HideVictory()
    {
        isVictoryShown = false;
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// Обработчик клика по экрану победы - открывает магазин приложений
    /// </summary>
    public void OnVictoryClick()
    {
        string storeLink = GetStoreLink();
        
        if (!string.IsNullOrEmpty(storeLink))
        {
            Debug.Log($"Открываю магазин приложений: {storeLink}");
            Application.OpenURL(storeLink);
        }
        else
        {
            Debug.LogWarning("VictoryScreen: Не удалось определить платформу для открытия магазина");
        }
    }
    
    /// <summary>
    /// Получает ссылку на магазин в зависимости от платформы
    /// </summary>
    string GetStoreLink()
    {
        // Используем RuntimePlatform для определения платформы
        if (Application.platform == RuntimePlatform.Android)
        {
            return androidStoreLink;
        }
        else if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            return iosStoreLink;
        }
        
        // Для редактора или других платформ возвращаем Android ссылку по умолчанию
        Debug.Log("VictoryScreen: Платформа не мобильная, используем Android ссылку по умолчанию");
        return androidStoreLink;
    }
    
    /// <summary>
    /// Устанавливает спрайт для картинки победы
    /// </summary>
    public void SetVictorySprite(Sprite sprite)
    {
        victorySprite = sprite;
        
        if (victoryPanel != null)
        {
            SetupPanelComponents();
            
            if (victoryImage != null)
            {
                victoryImage.sprite = sprite;
                Debug.Log($"VictoryScreen: Спрайт назначен на Image: {(sprite != null ? sprite.name : "null")}");
            }
            else
            {
                Debug.LogWarning("VictoryScreen: victoryImage не найден на панели! Добавьте Image компонент.");
            }
        }
        else
        {
            Debug.LogWarning("VictoryScreen: victoryPanel не назначен! Назначьте панель в инспекторе.");
        }
    }
    
    public bool IsVictoryShown => isVictoryShown;
}

