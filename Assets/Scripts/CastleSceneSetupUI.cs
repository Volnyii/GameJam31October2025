using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Автоматическая настройка сцены с всеми объектами на Canvas
/// Все игровые объекты будут UI элементами, видимыми в редакторе
/// </summary>
public class CastleSceneSetupUI : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Префаб игрока (если не назначен, создастся автоматически)")]
    public GameObject playerPrefab;
    [Tooltip("Префаб монстра (можно назначить в MonsterSpawner)")]
    public GameObject monsterPrefab;
    [Tooltip("Префаб крюка (если не назначен, создастся автоматически)")]
    public GameObject hookPrefab;
    
    [Header("Auto Setup")]
    [Tooltip("Автоматически настроить сцену при старте (для билда)")]
    public bool autoSetupOnStart = true;
    
    void Awake()
    {
        // Настройка в Awake для раннего выполнения
        if (autoSetupOnStart)
        {
            // Проверяем, есть ли уже Canvas с объектами
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null || canvas.transform.childCount == 0)
            {
                Debug.Log("Автоматическая настройка сцены в Awake...");
                SetupScene();
            }
        }
    }
    
    void Start()
    {
        // Дополнительная проверка в Start на случай если Awake не сработал
        if (autoSetupOnStart)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null || canvas.transform.childCount == 0)
            {
                Debug.LogWarning("Canvas не найден в Start, пытаюсь создать...");
                SetupScene();
            }
            else
            {
                // Убеждаемся что Canvas активен и включен
                if (!canvas.gameObject.activeSelf || !canvas.enabled)
                {
                    Debug.LogWarning("Canvas найден, но неактивен! Активирую...");
                    canvas.gameObject.SetActive(true);
                    canvas.enabled = true;
                }
            }
        }
    }
    
    [ContextMenu("Setup Castle Scene UI")]
    public void SetupScene()
    {
        Debug.Log("=== Начинаю автоматическую настройку сцены с UI элементами ===");
        
        // 1. Создаем Canvas (самое важное!)
        Canvas canvas = SetupCanvas();
        if (canvas == null)
        {
            Debug.LogError("Не удалось создать Canvas! Прерываем настройку.");
            return;
        }
        
        // 2. Создаем фоновое изображение
        Image background = SetupBackground(canvas);
        
        // 3. Создаем игрока
        CastlePlayerUI player = SetupPlayer(canvas, null);
        
        // 5. Создаем крюк
        HookUI hook = SetupHook(canvas, player);
        
        // 6. Создаем индикатор стрелки (UI версия)
        ArrowIndicatorUI arrow = SetupArrowUI(canvas);
        
        // 7. Создаем контроллер тапа
        CastleGameTouchControllerUI touchController = SetupTouchController(canvas, hook, arrow, player);
        
        // 8. Создаем спавнер монстров
        MonsterSpawnerUI spawner = SetupMonsterSpawner(canvas, null);
        
        // 9. Создаем UI Manager
        CastleUIManager uiManager = SetupUI(canvas);
        
        // 10. Создаем GameManager
        CastleGameManagerUI gameManager = SetupGameManager(canvas, player, hook, touchController, spawner, arrow);
        
        // 11. Настраиваем вертикальную ориентацию для WebGL
        SetupWebGLPortrait();
        
        // Финальная проверка - убеждаемся что все видимо
        VerifySetup(canvas, background);
        
        Debug.Log("=== Настройка сцены завершена! Все объекты на Canvas ===");
    }
    
    /// <summary>
    /// Настраивает вертикальную ориентацию для WebGL билда
    /// </summary>
    void SetupWebGLPortrait()
    {
        // Убеждаемся что ориентация установлена
        Screen.orientation = ScreenOrientation.Portrait;
        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToPortrait = true;
        Screen.autorotateToPortraitUpsideDown = false;
        
        // Добавляем компонент WebGLPortraitEnforcer если его нет
        GameObject enforcerObj = GameObject.Find("WebGLPortraitEnforcer");
        if (enforcerObj == null)
        {
            enforcerObj = new GameObject("WebGLPortraitEnforcer");
            enforcerObj.AddComponent<WebGLPortraitEnforcer>();
        }
        else if (enforcerObj.GetComponent<WebGLPortraitEnforcer>() == null)
        {
            enforcerObj.AddComponent<WebGLPortraitEnforcer>();
        }
        
        Debug.Log("✓ Вертикальная ориентация настроена для WebGL");
    }
    
    /// <summary>
    /// Проверяет что все элементы правильно настроены и видимы
    /// </summary>
    void VerifySetup(Canvas canvas, Image background)
    {
        Debug.Log("=== Проверка настройки ===");
        
        if (canvas == null)
        {
            Debug.LogError("❌ Canvas отсутствует!");
            return;
        }
        
        Debug.Log($"✓ Canvas: активен={canvas.gameObject.activeSelf}, включен={canvas.enabled}, режим={canvas.renderMode}");
        
        if (background == null)
        {
            Debug.LogError("❌ Фон отсутствует!");
        }
        else
        {
            Debug.Log($"✓ Фон: активен={background.gameObject.activeSelf}, включен={background.enabled}, спрайт={background.sprite != null}");
        }
        
        // Проверяем что Canvas находится в корне сцены
        if (canvas.transform.parent != null)
        {
            Debug.LogWarning($"⚠ Canvas находится не в корне сцены (родитель: {canvas.transform.parent.name})");
        }
        
        // Проверяем что Canvas имеет правильный порядок сортировки
        Debug.Log($"✓ Canvas sortingOrder: {canvas.sortingOrder}");
        
        // Проверяем количество дочерних элементов
        int childCount = canvas.transform.childCount;
        Debug.Log($"✓ Дочерних элементов на Canvas: {childCount}");
        
        if (childCount == 0)
        {
            Debug.LogError("❌ На Canvas нет дочерних элементов! Сцена пустая!");
        }
        
        // Проверяем камеру
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Debug.Log($"✓ Камера: активна={mainCam.gameObject.activeSelf}, включена={mainCam.enabled}");
            Debug.Log($"  - clearFlags: {mainCam.clearFlags}");
            Debug.Log($"  - backgroundColor: {mainCam.backgroundColor}");
        }
        else
        {
            Debug.LogWarning("⚠ Main Camera не найдена (не критично для ScreenSpaceOverlay)");
        }
    }
    
    Canvas SetupCanvas()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // Вертикальное разрешение
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Устанавливаем правильный размер Canvas
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                canvasRect = canvasObj.AddComponent<RectTransform>();
            }
            canvasRect.sizeDelta = new Vector2(1080, 1920);
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            
            // Убеждаемся что Canvas видим и активен
            canvasObj.SetActive(true);
            canvas.enabled = true;
        }
        else
        {
            // Проверяем настройки существующего Canvas
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Debug.Log("Canvas renderMode изменен на ScreenSpaceOverlay");
            }
            
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
            }
            
            if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
            
            canvas.gameObject.SetActive(true);
            canvas.enabled = true;
        }
        
        // Создаем EventSystem если его нет
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();
        }
        
        // Убеждаемся что камера существует (для совместимости)
        if (Camera.main == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.3f, 0.5f, 0.7f); // Синий фон
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            camObj.AddComponent<AudioListener>();
        }
        
        Debug.Log("✓ Canvas создан/проверен");
        Debug.Log($"  - RenderMode: {canvas.renderMode}");
        Debug.Log($"  - Enabled: {canvas.enabled}");
        Debug.Log($"  - Active: {canvas.gameObject.activeSelf}");
        return canvas;
    }
    
    Image SetupBackground(Canvas canvas)
    {
        Transform bgTransform = canvas.transform.Find("Background");
        GameObject bgObj;
        Image bgImage;
        
        if (bgTransform == null)
        {
            bgObj = new GameObject("Background");
            bgObj.transform.SetParent(canvas.transform);
            bgObj.transform.SetAsFirstSibling();
            
            RectTransform rect = bgObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = new Vector3(0.015f, 0.015f, 1f);
            
            bgImage = bgObj.AddComponent<Image>();
            bgImage.color = Color.white;
            bgImage.sprite = CreateBackgroundSprite();
            bgImage.type = Image.Type.Simple;
            
            // Убеждаемся что фон видим
            bgImage.enabled = true;
            bgObj.SetActive(true);
        }
        else
        {
            bgObj = bgTransform.gameObject;
            bgImage = bgObj.GetComponent<Image>();
            if (bgImage == null)
            {
                bgImage = bgObj.AddComponent<Image>();
            }
            
            // Обновляем настройки существующего фона
            if (bgImage.sprite == null)
            {
                bgImage.sprite = CreateBackgroundSprite();
            }
            bgImage.type = Image.Type.Simple;
            bgImage.color = Color.white;
            bgImage.enabled = true;
            bgObj.SetActive(true);
            
            // Устанавливаем scale
            RectTransform existingRect = bgObj.GetComponent<RectTransform>();
            if (existingRect != null)
            {
                existingRect.localScale = new Vector3(0.015f, 0.015f, 1f);
            }
        }
        
        Debug.Log($"✓ Фоновое изображение создано/проверено");
        Debug.Log($"  - Sprite: {(bgImage.sprite != null ? "есть" : "ОТСУТСТВУЕТ")}");
        Debug.Log($"  - Enabled: {bgImage.enabled}");
        Debug.Log($"  - Active: {bgObj.activeSelf}");
        return bgImage;
    }
    
    Image SetupCastle(Canvas canvas)
    {
        Transform castleTransform = canvas.transform.Find("Castle");
        GameObject castleObj;
        Image castleImage;
        
        if (castleTransform == null)
        {
            castleObj = new GameObject("Castle");
            castleObj.transform.SetParent(canvas.transform);
            
            RectTransform rect = castleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f); // По центру снизу
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            // Замок занимает часть экрана - от низа до примерно 60% высоты
            rect.sizeDelta = new Vector2(200, 1200); // Ширина 200px, высота 1200px
            rect.anchoredPosition = new Vector2(0, 0); // Низ на дне экрана
            
            castleImage = castleObj.AddComponent<Image>();
            castleImage.color = Color.white;
            castleImage.sprite = CreateCastleSprite();
            castleImage.type = Image.Type.Simple;
        }
        else
        {
            castleObj = castleTransform.gameObject;
            castleImage = castleObj.GetComponent<Image>();
            if (castleImage == null)
            {
                castleImage = castleObj.AddComponent<Image>();
            }
        }
        
        Debug.Log("✓ Замок создан как UI элемент");
        return castleImage;
    }
    
    CastlePlayerUI SetupPlayer(Canvas canvas, Image castle)
    {
        Transform playerTransform = canvas.transform.Find("Player");
        GameObject playerObj;
        CastlePlayerUI player;
        
        if (playerTransform == null)
        {
            // Используем префаб если назначен
            if (playerPrefab != null)
            {
                playerObj = Instantiate(playerPrefab, canvas.transform);
                playerObj.name = "Player";
                player = playerObj.GetComponent<CastlePlayerUI>();
                if (player == null)
                {
                    player = playerObj.AddComponent<CastlePlayerUI>();
                }
            }
            else
            {
                playerObj = new GameObject("Player");
                playerObj.transform.SetParent(canvas.transform);
                player = playerObj.AddComponent<CastlePlayerUI>();
            }
        }
        else
        {
            playerObj = playerTransform.gameObject;
            player = playerObj.GetComponent<CastlePlayerUI>();
            if (player == null)
            {
                player = playerObj.AddComponent<CastlePlayerUI>();
            }
        }
        
        // Автоматически настраиваем компоненты
        player.SetupPlayerComponents();
        
        player.castlePosition = Vector2.zero;
        RectTransform playerRect = playerObj.GetComponent<RectTransform>();
        if (playerRect != null)
        {
            playerRect.anchoredPosition = new Vector2(0, 1100); // На вершине замка
        }
        
        Debug.Log("✓ Игрок создан на Canvas");
        return player;
    }
    
    HookUI SetupHook(Canvas canvas, CastlePlayerUI player)
    {
        Transform hookTransform = canvas.transform.Find("Hook");
        GameObject hookObj;
        HookUI hook;
        
        if (hookTransform == null)
        {
            // Используем префаб если назначен
            if (hookPrefab != null)
            {
                hookObj = Instantiate(hookPrefab, canvas.transform);
                hookObj.name = "Hook";
                hook = hookObj.GetComponent<HookUI>();
                if (hook == null)
                {
                    hook = hookObj.AddComponent<HookUI>();
                }
            }
            else
            {
                hookObj = new GameObject("Hook");
                hookObj.transform.SetParent(canvas.transform);
                hook = hookObj.AddComponent<HookUI>();
            }
        }
        else
        {
            hookObj = hookTransform.gameObject;
            hook = hookObj.GetComponent<HookUI>();
            if (hook == null)
            {
                hook = hookObj.AddComponent<HookUI>();
            }
        }
        
        // Автоматически настраиваем компоненты
        hook.SetupHookComponents();
        
        RectTransform hookRect = hookObj.GetComponent<RectTransform>();
        if (hookRect != null)
        {
            hookRect.anchorMin = new Vector2(0.5f, 0f);
            hookRect.anchorMax = new Vector2(0.5f, 0f);
            hookRect.pivot = new Vector2(0.5f, 0.5f);
            hookRect.sizeDelta = new Vector2(32, 32);
        }
        
        // Связываем крюк с игроком
        RectTransform playerRect = player.GetComponent<RectTransform>();
        if (playerRect != null)
        {
            hook.playerRectTransform = playerRect;
        }
        
        // Настройка параметров крюка
        hook.hookSpeed = 10f;
        hook.retractSpeed = 15f;
        hook.hookRadius = 60f;
        hook.lineThickness = 15f;
        hook.lineColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        
        Debug.Log("✓ Крюк создан и настроен на Canvas");
        Debug.Log($"  - Скорость полета: {hook.hookSpeed}");
        Debug.Log($"  - Скорость возврата: {hook.retractSpeed}");
        Debug.Log($"  - Радиус попадания: {hook.hookRadius}");
        Debug.Log($"  - Толщина линии: {hook.lineThickness}");
        if (hookPrefab != null)
        {
            Debug.Log($"  - Используется префаб крюка: {hookPrefab.name}");
        }
        return hook;
    }
    
    ArrowIndicatorUI SetupArrowUI(Canvas canvas)
    {
        GameObject arrowObj = GameObject.Find("ArrowIndicator");
        ArrowIndicatorUI arrow;
        
        if (arrowObj == null)
        {
            arrowObj = new GameObject("ArrowIndicator");
            if (canvas != null)
            {
                arrowObj.transform.SetParent(canvas.transform);
            }
            arrow = arrowObj.AddComponent<ArrowIndicatorUI>();
        }
        else
        {
            arrow = arrowObj.GetComponent<ArrowIndicatorUI>();
            if (arrow == null)
            {
                // Удаляем старый компонент если есть
                ArrowIndicator oldArrow = arrowObj.GetComponent<ArrowIndicator>();
                if (oldArrow != null) DestroyImmediate(oldArrow);
                
                arrow = arrowObj.AddComponent<ArrowIndicatorUI>();
            }
        }
        
        Debug.Log("✓ Индикатор стрелки (UI) создан");
        return arrow;
    }
    
    CastleGameTouchControllerUI SetupTouchController(Canvas canvas, HookUI hook, ArrowIndicatorUI arrow, CastlePlayerUI player)
    {
        GameObject touchObj = GameObject.Find("TouchController");
        CastleGameTouchControllerUI touchController;
        
        if (touchObj == null)
        {
            // Создаем на Canvas чтобы был в одной иерархии с другими UI элементами
            touchObj = new GameObject("TouchController");
            touchObj.transform.SetParent(canvas.transform);
            touchController = touchObj.AddComponent<CastleGameTouchControllerUI>();
        }
        else
        {
            touchController = touchObj.GetComponent<CastleGameTouchControllerUI>();
            if (touchController == null)
            {
                touchController = touchObj.AddComponent<CastleGameTouchControllerUI>();
            }
        }
        
        touchController.hookController = hook;
        touchController.arrowIndicatorUI = arrow;
        touchController.playerRectTransform = player.GetComponent<RectTransform>();
        touchController.playerController = player;
        touchController.canvas = canvas;
        
        Debug.Log("✓ Контроллер тапа создан на Canvas");
        Debug.Log($"  hookController назначен: {hook != null}");
        Debug.Log($"  playerRectTransform назначен: {player.GetComponent<RectTransform>() != null}");
        Debug.Log($"  playerController назначен: {player != null}");
        Debug.Log($"  canvas назначен: {canvas != null}");
        
        return touchController;
    }
    
    MonsterSpawnerUI SetupMonsterSpawner(Canvas canvas, Image castle)
    {
        GameObject spawnerObj = GameObject.Find("MonsterSpawner");
        MonsterSpawnerUI spawner;
        
        if (spawnerObj == null)
        {
            spawnerObj = new GameObject("MonsterSpawner");
            spawnerObj.transform.SetParent(canvas.transform);
            spawner = spawnerObj.AddComponent<MonsterSpawnerUI>();
            
            // Добавляем helper для быстрой настройки
            MonsterPoolSetupHelper h = spawnerObj.AddComponent<MonsterPoolSetupHelper>();
        }
        else
        {
            spawner = spawnerObj.GetComponent<MonsterSpawnerUI>();
            if (spawner == null)
            {
                spawner = spawnerObj.AddComponent<MonsterSpawnerUI>();
            }
            
            // Убеждаемся что helper есть
            if (spawnerObj.GetComponent<MonsterPoolSetupHelper>() == null)
            {
                spawnerObj.AddComponent<MonsterPoolSetupHelper>();
            }
        }
        
        spawner.castleCenter = Vector2.zero;
        spawner.monsterCount = 5;
        spawner.spawnRadius = 6f;
        
        // Назначаем префаб монстра если он указан в setup (для обратной совместимости)
        if (monsterPrefab != null && spawner.monsterPrefab == null)
        {
            spawner.monsterPrefab = monsterPrefab;
            
            // Если массив типов пуст, создаем один тип из старого префаба
            if (spawner.monsterTypes == null || spawner.monsterTypes.Length == 0)
            {
                spawner.monsterTypes = new MonsterPoolData[1];
                spawner.monsterTypes[0] = new MonsterPoolData
                {
                    monsterPrefab = monsterPrefab,
                    monsterTypeName = "Default Monster",
                    spawnWeight = 1,
                    poolMinSize = 2
                };
            }
        }
        
        Debug.Log("✓ Спавнер монстров создан на Canvas");
        
        // Проверяем настройку пула
        MonsterPoolSetupHelper helper = spawnerObj.GetComponent<MonsterPoolSetupHelper>();
        if (helper != null)
        {
            Debug.Log("  Доступен MonsterPoolSetupHelper для быстрой настройки пулов");
        }
        
        if (spawner.monsterTypes != null && spawner.monsterTypes.Length > 0)
        {
            Debug.Log($"  Используется система пула: {spawner.monsterTypes.Length} типов монстров");
            foreach (var type in spawner.monsterTypes)
            {
                if (type != null && type.monsterPrefab != null)
                {
                    Debug.Log($"    - {type.monsterTypeName}: вес={type.spawnWeight}, мин.пул={type.poolMinSize}");
                }
            }
        }
        else if (spawner.monsterPrefab != null)
        {
            Debug.Log($"  Используется одиночный префаб монстра: {spawner.monsterPrefab.name}");
        }
        else
        {
            Debug.Log("  ⚠ Пул монстров не настроен!");
            Debug.Log("  Для быстрой настройки:");
            Debug.Log("    1. Перетащите префабы монстров в MonsterPoolSetupHelper.monsterPrefabs");
            Debug.Log("    2. Правый клик → 'Автоматическая настройка из префабов'");
        }
        return spawner;
    }
    
    CastleUIManager SetupUI(Canvas canvas)
    {
        try
        {
            GameObject uiManagerObj = GameObject.Find("UIManager");
            CastleUIManager uiManager;
            
            if (uiManagerObj == null)
            {
                uiManagerObj = new GameObject("UIManager");
                uiManager = uiManagerObj.AddComponent<CastleUIManager>();
            }
            else
            {
                uiManager = uiManagerObj.GetComponent<CastleUIManager>();
                if (uiManager == null)
                {
                    uiManager = uiManagerObj.AddComponent<CastleUIManager>();
                }
            }
            
            Image background = canvas.transform.Find("Background")?.GetComponent<Image>();
            if (background != null) uiManager.backgroundImage = background;
            
            uiManager.canvas = canvas;
            
            Debug.Log("✓ UI Manager создан");
            return uiManager;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при создании UI: {e.Message}");
            return null;
        }
    }
    
    Text SetupScoreText(Canvas canvas)
    {
        Transform scoreTransform = canvas.transform.Find("ScoreText");
        GameObject scoreObj;
        Text scoreText;
        
        if (scoreTransform == null)
        {
            scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(canvas.transform);
            
            RectTransform rect = scoreObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f); // Верхний левый угол
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(400, 80);
            rect.anchoredPosition = new Vector2(20, -20); // 20px от левого края, 20px от верха
            
            scoreText = scoreObj.AddComponent<Text>();
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont != null) scoreText.font = defaultFont;
            scoreText.fontSize = 48;
            scoreText.color = Color.white;
            scoreText.text = "Score: 0";
            scoreText.alignment = TextAnchor.UpperLeft;
        }
        else
        {
            scoreObj = scoreTransform.gameObject;
            scoreText = scoreObj.GetComponent<Text>();
            if (scoreText == null) scoreText = scoreObj.AddComponent<Text>();
        }
        
        return scoreText;
    }
    
    Text SetupMonstersText(Canvas canvas)
    {
        Transform monstersTransform = canvas.transform.Find("MonstersText");
        GameObject monstersObj;
        Text monstersText;
        
        if (monstersTransform == null)
        {
            monstersObj = new GameObject("MonstersText");
            monstersObj.transform.SetParent(canvas.transform);
            
            RectTransform rect = monstersObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f); // Верхний левый угол
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(400, 80);
            rect.anchoredPosition = new Vector2(20, -110); // Ниже счета
            
            monstersText = monstersObj.AddComponent<Text>();
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont != null) monstersText.font = defaultFont;
            monstersText.fontSize = 36;
            monstersText.color = Color.white;
            monstersText.text = "Monsters: 0";
            monstersText.alignment = TextAnchor.UpperLeft;
        }
        else
        {
            monstersObj = monstersTransform.gameObject;
            monstersText = monstersObj.GetComponent<Text>();
            if (monstersText == null) monstersText = monstersObj.AddComponent<Text>();
        }
        
        return monstersText;
    }
    
    CastleGameManagerUI SetupGameManager(Canvas canvas, CastlePlayerUI player, HookUI hook, 
        CastleGameTouchControllerUI touchController, MonsterSpawnerUI spawner, ArrowIndicatorUI arrow)
    {
        GameObject gmObj = GameObject.Find("GameManager");
        CastleGameManagerUI gm;
        
        if (gmObj == null)
        {
            gmObj = new GameObject("GameManager");
            gm = gmObj.AddComponent<CastleGameManagerUI>();
        }
        else
        {
            gm = gmObj.GetComponent<CastleGameManagerUI>();
            if (gm == null)
            {
                gm = gmObj.AddComponent<CastleGameManagerUI>();
            }
        }
        
        gm.player = player;
        gm.hook = hook;
        gm.touchController = touchController;
        gm.monsterSpawner = spawner;
        gm.arrowIndicator = arrow;
        gm.canvas = canvas;
        
        Debug.Log("✓ GameManager создан");
        return gm;
    }
    
    Sprite CreateCastleSprite()
    {
        int width = 200;
        int height = 1100;
        Texture2D texture = new Texture2D(width, height);
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
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 100f);
    }
    
    Sprite CreateBackgroundSprite()
    {
        int width = 512;
        int height = 1024;
        Texture2D texture = new Texture2D(width, height);
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
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}

