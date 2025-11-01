using UnityEngine;

/// <summary>
/// Автоматическая настройка сцены со SpriteRenderer (без Canvas)
/// ОДНА КОМАНДА - полная пересборка сцены из ничего
/// Все объекты создаются программно, префабы опциональны
/// </summary>
public class CastleSceneSetup : MonoBehaviour
{
    [Header("Auto Setup")]
    [Tooltip("Автоматически настроить сцену при старте (для билда)")]
    public bool autoSetupOnStart = true;
    
    [Header("Scene Settings")]
    [Tooltip("Позиция замка (в мировых координатах)")]
    public Vector2 castlePosition = Vector2.zero;
    [Tooltip("Высота замка (в мировых единицах)")]
    public float castleHeight = 3f;
    [Tooltip("Ширина замка (в мировых единицах)")]
    public float castleWidth = 2f;
    
    [Header("Game Settings")]
    [Tooltip("Количество монстров")]
    public int monsterCount = 5;
    [Tooltip("Радиус патрулирования монстров")]
    public float monsterPatrolRadius = 6f;
    
    [Header("Hook Settings")]
    [Tooltip("Скорость полета крюка")]
    public float hookSpeed = 10f;
    [Tooltip("Скорость возврата крюка")]
    public float retractSpeed = 15f;
    [Tooltip("Радиус попадания крюка")]
    public float hookRadius = 0.6f;
    [Tooltip("Максимальная длина цепи (радиус круга для полета)")]
    public float maxChainLength = 5f;
    
    void Awake()
    {
        if (autoSetupOnStart)
        {
            if (FindObjectOfType<CastlePlayer>() == null || FindObjectOfType<HookController>() == null)
            {
                Debug.Log("Автоматическая настройка сцены в Awake...");
                CleanAndRebuild();
            }
        }
    }
    
    void Start()
    {
        if (autoSetupOnStart)
        {
            if (FindObjectOfType<CastlePlayer>() == null || FindObjectOfType<HookController>() == null)
            {
                Debug.LogWarning("Объекты не найдены в Start, пересобираю сцену...");
                CleanAndRebuild();
            }
        }
    }
    
    /// <summary>
    /// ОСНОВНАЯ КОМАНДА - полностью очищает и пересобирает всю сцену
    /// </summary>
    [ContextMenu("Clean And Rebuild Scene")]
    public void CleanAndRebuild()
    {
        Debug.Log("=== ПОЛНАЯ ПЕРЕСБОРКА СЦЕНЫ ===");
        
        // Удаляем все старые объекты игры
        CleanupOldObjects();
        
        // Создаем всё заново
        SetupCastleScene();
        
        Debug.Log("=== СЦЕНА ПОЛНОСТЬЮ ПЕРЕСОБРАНА! ===");
    }
    
    /// <summary>
    /// Удаляет все старые объекты игры
    /// </summary>
    void CleanupOldObjects()
    {
        Debug.Log("Очистка старых объектов...");
        
        // Удаляем все игровые объекты
        string[] objectNames = { "Background", "Castle", "Player", "Hook", "TouchController", "MonsterSpawner", "GameManager" };
        
        foreach (string objName in objectNames)
        {
            GameObject obj = GameObject.Find(objName);
            if (obj != null)
            {
                DestroyImmediate(obj);
                Debug.Log($"  Удален: {objName}");
            }
        }
        
        // Удаляем всех монстров
        MonsterController[] monsters = FindObjectsOfType<MonsterController>();
        foreach (var monster in monsters)
        {
            if (monster != null)
            {
                DestroyImmediate(monster.gameObject);
            }
        }
        
        Debug.Log("✓ Очистка завершена");
    }
    
    /// <summary>
    /// Настройка всей сцены
    /// </summary>
    [ContextMenu("Setup Castle Scene")]
    public void SetupScene()
    {
        SetupCastleScene();
    }
    
    /// <summary>
    /// Основной метод настройки сцены замка - восстанавливает все одним нажатием
    /// </summary>
    public void SetupCastleScene()
    {
        Debug.Log("=== Начинаю создание всей сцены со SpriteRenderer ===");
        
        // 1. Настраиваем камеру
        Camera mainCam = SetupCamera();
        
        // 2. Создаем фон
        SetupBackground(mainCam);
        
        // 3. Создаем игрока
        CastlePlayer player = SetupPlayer();
        
        // 5. Создаем крюк
        HookController hook = SetupHook(player);
        
        // 6. Создаем контроллер тапа
        CastleGameTouchController touchController = SetupTouchController(hook, player);
        
        // 7. Создаем спавнер монстров
        MonsterSpawner spawner = SetupMonsterSpawner();
        
        // 8. Создаем GameManager и связываем все
        CastleGameManager gameManager = SetupGameManager(player, hook, touchController, spawner);
        
        Debug.Log("=== Настройка сцены завершена! Все объекты используют SpriteRenderer ===");
    }
    
    Camera SetupCamera()
    {
        Camera mainCam = Camera.main;
        
        if (mainCam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            mainCam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            camObj.AddComponent<AudioListener>();
            Debug.Log("  Создана новая камера");
        }
        
        mainCam.orthographic = true;
        mainCam.orthographicSize = 10f;
        mainCam.backgroundColor = new Color(0.1f, 0.3f, 0.5f);
        mainCam.transform.position = new Vector3(castlePosition.x, castlePosition.y + castleHeight / 2f, -10);
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        
        Debug.Log("✓ Камера настроена");
        return mainCam;
    }
    
    SpriteRenderer SetupBackground(Camera cam)
    {
        GameObject bgObj = new GameObject("Background");
        SpriteRenderer bgRenderer = bgObj.AddComponent<SpriteRenderer>();
        
        Sprite bgSprite = null;
        
        #if UNITY_EDITOR
        // В редакторе используем AssetDatabase для загрузки из Assets/Sprites/bg.png
        bgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/bg.png");
        #endif
        
        // Если не загрузили в редакторе, пробуем Resources
        if (bgSprite == null)
        {
            bgSprite = Resources.Load<Sprite>("Sprites/bg");
        }
        
        // Если не загрузили через Resources, пробуем StreamingAssets
        if (bgSprite == null)
        {
            string bgPath = System.IO.Path.Combine(Application.streamingAssetsPath, "bg.png");
            if (System.IO.File.Exists(bgPath))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(bgPath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    bgSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }
        
        // Если не загрузили из StreamingAssets, пробуем прямой путь к Assets (только в редакторе)
        #if UNITY_EDITOR
        if (bgSprite == null)
        {
            string fullPath = System.IO.Path.Combine(Application.dataPath, "Sprites", "bg.png");
            if (System.IO.File.Exists(fullPath))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(fullPath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    bgSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }
        #endif
        
        if (bgSprite == null)
        {
            // Если не нашли, создаем программно
            bgSprite = CreateBackgroundSprite();
            Debug.LogWarning("Спрайт фона не найден в Sprites/bg, создан программно");
        }
        else
        {
            Debug.Log("✓ Спрайт фона загружен из Sprites/bg");
        }
        
        bgRenderer.sprite = bgSprite;
        bgRenderer.color = Color.white;
        bgRenderer.sortingOrder = 0;
        
        // Позиционируем и масштабируем фон
        bgObj.transform.position = cam.transform.position + Vector3.forward * 1f;
        bgObj.transform.localScale = new Vector3(0.015f, 0.015f, 1f);
        
        Debug.Log("✓ Фон создан и масштабирован под размер камеры");
        return bgRenderer;
    }
    
    SpriteRenderer SetupCastle()
    {
        GameObject castleObj = new GameObject("Castle");
        SpriteRenderer castleRenderer = castleObj.AddComponent<SpriteRenderer>();
        
        castleRenderer.sprite = CreateCastleSprite();
        castleRenderer.color = Color.white;
        castleRenderer.sortingOrder = 1;
        
        // Позиционируем замок
        castleObj.transform.position = new Vector3(castlePosition.x, castlePosition.y, 0);
        castleObj.transform.localScale = new Vector3(castleWidth, castleHeight, 1f);
        
        Debug.Log("✓ Замок создан программно");
        return castleRenderer;
    }
    
    CastlePlayer SetupPlayer()
    {
        GameObject playerObj = new GameObject("Player");
        CastlePlayer player = playerObj.AddComponent<CastlePlayer>();
        
        // Автоматически настраиваем компоненты
        player.SetupPlayerComponents();
        
        player.castlePosition = castlePosition;
        player.castleTopHeight = castleHeight;
        
        Debug.Log("✓ Игрок создан программно");
        return player;
    }
    
    HookController SetupHook(CastlePlayer player)
    {
        GameObject hookObj = new GameObject("Hook");
        HookController hook = hookObj.AddComponent<HookController>();
        
        // Автоматически настраиваем компоненты
        hook.SetupHookComponents();
        
        // Связываем крюк с игроком
        hook.playerTransform = player.transform;
        
        // Настройка параметров крюка
        hook.hookSpeed = hookSpeed;
        hook.retractSpeed = retractSpeed;
        hook.hookRadius = hookRadius;
        hook.maxChainLength = maxChainLength; // Максимальная длина цепи (радиус круга для полета)
        hook.lineThickness = 0.12f; // Заметная толщина цепи для видимости (как на картинках)
        hook.lineColor = new Color(0.8f, 0.5f, 0.2f, 1f); // Коричнево-оранжевый цвет веревки (как на картинках)
        
        // Эффект удочки - вращение и провисающая цепь
        hook.hookRotationSpeed = 360f; // Крюк вращается во время полета (как у удочки)
        hook.chainPoints = 20; // Больше точек для более плавной провисающей цепи (как на картинках)
        hook.chainSagAmount = 1.2f; // Заметное провисание цепи во время полета
        hook.restChainSagAmount = 2.5f; // Сильное провисание в покое (как на картинках - мягкая веревка)
        hook.showHookAtRest = true; // Показывать крюк в руке с мягко провисающей цепью
        
        Debug.Log("✓ Крюк создан и настроен программно (трехфазный бросок: растяжка → падение → возврат)");
        return hook;
    }
    
    CastleGameTouchController SetupTouchController(HookController hook, CastlePlayer player)
    {
        GameObject touchObj = new GameObject("TouchController");
        CastleGameTouchController touchController = touchObj.AddComponent<CastleGameTouchController>();
        
        // Явно назначаем все ссылки
        touchController.hookController = hook;
        touchController.playerTransform = player.transform;
        touchController.playerController = player;
        
        // Принудительно вызываем инициализацию ссылок (камера будет найдена в Start)
        // Но можно также попробовать найти камеру сразу
        Camera cam = Camera.main;
        if (cam != null)
        {
            Debug.Log($"TouchController: камера найдена: {cam.name}");
        }
        
        Debug.Log("✓ Контроллер тапа создан и ссылки назначены");
        Debug.Log($"  - hookController: {touchController.hookController != null}");
        Debug.Log($"  - playerTransform: {touchController.playerTransform != null}");
        Debug.Log($"  - playerController: {touchController.playerController != null}");
        
        return touchController;
    }
    
    MonsterSpawner SetupMonsterSpawner()
    {
        GameObject spawnerObj = new GameObject("MonsterSpawner");
        MonsterSpawner spawner = spawnerObj.AddComponent<MonsterSpawner>();
        
        spawner.castleCenter = castlePosition;
        spawner.groundLevel = 0f;
        spawner.monsterCount = monsterCount;
        spawner.spawnRadius = monsterPatrolRadius;
        
        // Монстры будут создаваться программно (без префабов)
        spawner.monsterPrefab = null;
        spawner.monsterTypes = new MonsterPoolData[0];
        
        Debug.Log("✓ Спавнер монстров создан (монстры создаются программно)");
        return spawner;
    }
    
    CastleGameManager SetupGameManager(CastlePlayer player, HookController hook, 
        CastleGameTouchController touchController, MonsterSpawner spawner)
    {
        GameObject gmObj = new GameObject("GameManager");
        CastleGameManager gm = gmObj.AddComponent<CastleGameManager>();
        
        gm.player = player;
        gm.hook = hook;
        gm.touchController = touchController;
        gm.monsterSpawner = spawner;
        gm.castlePosition = castlePosition;
        
        Debug.Log("✓ GameManager создан и все компоненты связаны");
        return gm;
    }
    
    Sprite CreateCastleSprite()
    {
        int width = 200;
        int height = 300;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        
        Color castleColor = new Color(0.5f, 0.4f, 0.3f);
        Color darkColor = new Color(0.3f, 0.2f, 0.1f);
        Color lightColor = new Color(0.6f, 0.5f, 0.4f);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float centerX = width / 2f;
                float distFromCenterX = Mathf.Abs(x - centerX);
                
                if (distFromCenterX < 80f)
                {
                    if (y < height * 0.1f)
                    {
                        if (distFromCenterX < 90f)
                            pixels[y * width + x] = darkColor;
                        else
                            pixels[y * width + x] = Color.clear;
                    }
                    else if (y < height * 0.95f)
                    {
                        bool isBrick = ((x / 10) + (y / 10)) % 2 == 0;
                        pixels[y * width + x] = isBrick ? castleColor : lightColor;
                        
                        if (distFromCenterX > 75f)
                            pixels[y * width + x] = darkColor;
                    }
                    else
                    {
                        if (distFromCenterX < 40f)
                            pixels[y * width + x] = castleColor;
                        else
                            pixels[y * width + x] = Color.clear;
                    }
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
        return sprite;
    }
    
    Sprite CreateBackgroundSprite()
    {
        int width = 512;
        int height = 1024;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        
        Color topColor = new Color(0.1f, 0.3f, 0.5f);
        Color bottomColor = new Color(0.05f, 0.2f, 0.4f);
        
        for (int y = 0; y < height; y++)
        {
            float t = (float)y / height;
            Color color = Color.Lerp(bottomColor, topColor, t);
            
            for (int x = 0; x < width; x++)
            {
                pixels[y * width + x] = color;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        return sprite;
    }
}
