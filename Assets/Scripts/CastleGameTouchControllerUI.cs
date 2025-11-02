using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Контроллер тапов для игры с UI элементами на Canvas
/// </summary>
public class CastleGameTouchControllerUI : MonoBehaviour
{
    public static CastleGameTouchControllerUI Instance { get; private set; }
    
    [Header("References")]
    public HookUI hookController;
    public ArrowIndicatorUI arrowIndicatorUI;
    public Canvas canvas;
    public CastlePlayerUI playerController;
    public Transform playerTransform; // Transform игрока для получения позиции
    
    [Header("Player Prefab")]
    [Tooltip("Префаб игрока с анимациями (если назначен, будет использован вместо поиска существующего)")]
    public GameObject playerPrefab;
    [Tooltip("Автоматически создавать игрока из префаба при старте")]
    public bool autoSpawnPlayerFromPrefab = false;
    
    private bool isHoldingTouch = false;
    
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
        // Автоматически находим Canvas если не назначен
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
        }
        
        // Создаем игрока из префаба если нужно
        if (autoSpawnPlayerFromPrefab && playerPrefab != null && playerController == null)
        {
            SpawnPlayerFromPrefab();
        }
        
        // Автоматически находим компоненты если не назначены
        if (hookController == null)
        {
            hookController = FindObjectOfType<HookUI>();
        }
        
        if (playerTransform == null)
        {
            if (playerController != null)
            {
                playerTransform = playerController.transform;
            }
            else
            {
                CastlePlayerUI player = FindObjectOfType<CastlePlayerUI>();
                if (player != null)
                {
                    playerTransform = player.transform;
                }
            }
        }
        
        if (playerController == null)
        {
            playerController = FindObjectOfType<CastlePlayerUI>();
            // Обновляем playerTransform после поиска
            if (playerController != null && playerTransform == null)
            {
                playerTransform = playerController.transform;
            }
        }
        
        if (arrowIndicatorUI == null)
        {
            arrowIndicatorUI = FindObjectOfType<ArrowIndicatorUI>();
        }
        
        Debug.Log($"TouchController инициализирован:");
        Debug.Log($"  - canvas: {canvas != null}");
        Debug.Log($"  - hookController: {hookController != null}");
        Debug.Log($"  - playerTransform: {playerTransform != null}");
        Debug.Log($"  - playerController: {playerController != null}");
        Debug.Log($"  - arrowIndicatorUI: {arrowIndicatorUI != null}");
        Debug.Log($"  - playerPrefab: {playerPrefab != null}");
    }
    
    /// <summary>
    /// Создает игрока из префаба
    /// </summary>
    public void SpawnPlayerFromPrefab()
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("PlayerPrefab не назначен! Не могу создать игрока.");
            return;
        }
        
        // Удаляем старого игрока если есть
        if (playerController != null)
        {
            Debug.Log($"Удаляю старого игрока: {playerController.gameObject.name}");
            Destroy(playerController.gameObject);
            playerController = null;
            playerTransform = null;
        }
        else
        {
            // Ищем существующего игрока и удаляем
            CastlePlayerUI existingPlayer = FindObjectOfType<CastlePlayerUI>();
            if (existingPlayer != null)
            {
                Debug.Log($"Удаляю существующего игрока: {existingPlayer.gameObject.name}");
                Destroy(existingPlayer.gameObject);
            }
        }
        
        // Создаем нового игрока из префаба (всегда вне Canvas, так как используется SpriteRenderer)
        GameObject playerObj = Instantiate(playerPrefab);
        playerObj.name = "Player";
        
        // Получаем или добавляем компонент CastlePlayerUI
        playerController = playerObj.GetComponent<CastlePlayerUI>();
        if (playerController == null)
        {
            playerController = playerObj.AddComponent<CastlePlayerUI>();
            Debug.Log("Компонент CastlePlayerUI добавлен к префабу игрока");
        }
        
        // Настраиваем компоненты игрока
        playerController.SetupPlayerComponents();
        
        // Сбрасываем масштаб префаба перед применением настроек
        // Это нужно чтобы избежать умножения масштабов
        playerObj.transform.localScale = Vector3.one;
        
        // Применяем размер игрока если usePlayerScale включен
        if (playerController.usePlayerScale)
        {
            playerController.ApplyPlayerScale();
        }
        
        // Вызываем SetupPlayer() чтобы правильно установить позицию
        // SetupPlayer() учитывает настройки useCustomPosition и playerPosition
        playerController.SetupPlayer();
        
        // Если используется явная позиция, применяем её
        if (playerController.useCustomPosition)
        {
            playerObj.transform.position = playerController.playerPosition;
        }
        else
        {
            // Если не используется явная позиция, рассчитываем позицию
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Вычисляем позицию на вершине замка (примерно в середине экрана по вертикали)
                float castleTopY = mainCam.transform.position.y + mainCam.orthographicSize * 0.8f;
                playerObj.transform.position = new Vector3(0, castleTopY, 0);
                // Обновляем playerPosition для сохранения позиции
                playerController.playerPosition = playerObj.transform.position;
            }
        }
        
        // Сохраняем Transform для получения позиции
        playerTransform = playerObj.transform;
        
        Debug.Log($"Игрок создан из префаба: {playerPrefab.name}");
        Debug.Log($"  - Позиция (мировая): {playerObj.transform.position}");
        Debug.Log($"  - Использует явную позицию: {playerController.useCustomPosition}");
        Debug.Log($"  - Размер (scale): {playerController.playerScale}");
        Debug.Log($"  - Использует playerScale: {playerController.usePlayerScale}");
        Debug.Log($"  - Компонент CastlePlayerUI: {playerController != null}");
    }
    
    /// <summary>
    /// Подменяет текущего игрока на игрока из префаба
    /// </summary>
    [ContextMenu("Spawn Player From Prefab")]
    public void SpawnPlayerFromPrefabContextMenu()
    {
        SpawnPlayerFromPrefab();
    }
    
    void Update()
    {
        HandleInput();
    }
    
    void HandleInput()
    {
        // Обработка мыши
        if (Input.GetMouseButtonDown(0))
        {
            // Проверяем, не кликнули ли по UI тексту (HUD элементам)
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                // Игнорируем клики только по конкретным UI элементам (кнопки, интерактивные)
                // Но разрешаем клики по фону и игровым объектам
                GameObject selected = eventSystem.currentSelectedGameObject;
                if (selected != null && (selected.CompareTag("Button") || selected.GetComponent<Button>() != null))
                {
                    return; // Игнорируем клики по кнопкам
                }
            }
            
            OnTouchStart(Input.mousePosition);
        }
        
        if (Input.GetMouseButton(0) && isHoldingTouch)
        {
            OnTouchHold(Input.mousePosition);
        }
        
        if (Input.GetMouseButtonUp(0) && isHoldingTouch)
        {
            OnTouchEnd(Input.mousePosition);
        }
        
        // Обработка тача
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            if (touch.phase == TouchPhase.Began)
            {
                // Для тача проверяем только интерактивные элементы
                EventSystem eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    PointerEventData pointerData = new PointerEventData(eventSystem);
                    pointerData.position = touch.position;
                    
                    var results = new System.Collections.Generic.List<RaycastResult>();
                    eventSystem.RaycastAll(pointerData, results);
                    
                    // Пропускаем клик только если попали в Button или другой интерактивный элемент
                    bool hasInteractiveUI = false;
                    foreach (var result in results)
                    {
                        if (result.gameObject.GetComponent<Button>() != null || 
                            result.gameObject.GetComponent<InputField>() != null ||
                            result.gameObject.CompareTag("Button"))
                        {
                            hasInteractiveUI = true;
                            break;
                        }
                    }
                    
                    if (!hasInteractiveUI)
                    {
                        OnTouchStart(touch.position);
                    }
                }
                else
                {
                    OnTouchStart(touch.position);
                }
            }
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                if (isHoldingTouch)
                {
                    OnTouchHold(touch.position);
                }
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (isHoldingTouch)
                {
                    OnTouchEnd(touch.position);
                }
            }
        }
    }
    
    void OnTouchStart(Vector2 screenPosition)
    {
        Debug.Log($"OnTouchStart: screenPosition={screenPosition}, hookController.IsActive={hookController?.IsActive ?? false}");
        
        if (hookController != null && hookController.IsActive)
        {
            Debug.Log("Крюк уже активен, игнорируем новый тап");
            return;
        }
        
        isHoldingTouch = true;
        Debug.Log("isHoldingTouch = true");
        
        // Начинаем замах (как рыбак замахивается удочкой)
        if (playerController != null)
        {
            playerController.StartWindup();
            Debug.Log("Замах игрока начат");
        }
        else
        {
            Debug.LogWarning("playerController == null! Замах не начат");
        }
        
        if (arrowIndicatorUI != null && playerTransform != null && canvas != null)
        {
            Vector2 canvasTargetPos = ScreenToCanvasPosition(screenPosition);
            Vector2 playerCanvasPos = WorldToCanvasPosition(playerTransform.position);
            arrowIndicatorUI.Show(playerCanvasPos, canvasTargetPos);
        }
    }
    
    void OnTouchHold(Vector2 screenPosition)
    {
        if (!isHoldingTouch) return;
        
        if (arrowIndicatorUI != null && playerTransform != null && canvas != null)
        {
            Vector2 canvasTargetPos = ScreenToCanvasPosition(screenPosition);
            Vector2 playerCanvasPos = WorldToCanvasPosition(playerTransform.position);
            arrowIndicatorUI.Show(playerCanvasPos, canvasTargetPos);
        }
    }
    
    void OnTouchEnd(Vector2 screenPosition)
    {
        if (!isHoldingTouch)
        {
            Debug.Log("OnTouchEnd: isHoldingTouch = false, выходим");
            return;
        }
        
        Debug.Log($"OnTouchEnd: screenPosition={screenPosition}, hookController={hookController != null}, canvas={canvas != null}");
        
        isHoldingTouch = false;
        
        if (arrowIndicatorUI != null)
        {
            arrowIndicatorUI.Hide();
        }
        
        // Делаем бросок (анимация игрока)
        Vector2 canvasPos = ScreenToCanvasPosition(screenPosition);
        Debug.Log($"Конвертированная позиция в Canvas: {canvasPos}");
        
        float castPower = 1f;
        
        // Вычисляем "силу" броска на основе времени удержания или расстояния
        if (playerTransform != null)
        {
            Vector2 playerCanvasPos = WorldToCanvasPosition(playerTransform.position);
            float distance = Vector2.Distance(playerCanvasPos, canvasPos);
            castPower = Mathf.Clamp01(distance / 500f); // Нормализуем по максимальному расстоянию
            Debug.Log($"Расстояние до цели: {distance}, сила броска: {castPower}");
        }
        
        if (playerController != null)
        {
            playerController.Cast(castPower);
        }
        else
        {
            Debug.LogWarning("playerController == null! Анимация броска не будет выполнена");
        }
        
        // Небольшая задержка перед вылетом крюка для синхронизации с анимацией
        StartCoroutine(CastHookDelayed(canvasPos, 0.1f));
    }
    
    /// <summary>
    /// Запускает крюк с небольшой задержкой после анимации броска
    /// </summary>
    System.Collections.IEnumerator CastHookDelayed(Vector2 targetPos, float delay)
    {
        Debug.Log($"CastHookDelayed: ждем {delay} секунд, targetPos={targetPos}");
        yield return new WaitForSeconds(delay);
        
        Debug.Log($"CastHookDelayed: задержка прошла, hookController={hookController != null}, canvas={canvas != null}");
        
        if (hookController == null)
        {
            Debug.LogError("hookController == null! Не могу бросить крюк");
            yield break;
        }
        
        if (canvas == null)
        {
            Debug.LogWarning("canvas == null, но продолжаю...");
        }
        
        Debug.Log($"Вызываю hookController.CastHook({targetPos})");
        hookController.CastHook(targetPos);
        Debug.Log($"CastHook вызван, isActive={hookController.IsActive}");
    }
    
    Vector2 ScreenToCanvasPosition(Vector2 screenPos)
    {
        if (canvas == null) return screenPos;
        
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return screenPos;
        
        // Для ScreenSpaceOverlay камера не нужна
        Camera cam = canvas.worldCamera;
        if (cam == null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            cam = null; // Для overlay камера не используется
        }
        
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            cam,
            out Vector2 localPoint))
        {
            return localPoint;
        }
        
        // Fallback: простая конвертация для ScreenSpaceOverlay
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector2 canvasSize = canvasRect.sizeDelta;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            
            float x = (screenPos.x / screenSize.x - 0.5f) * canvasSize.x;
            float y = (screenPos.y / screenSize.y - 0.5f) * canvasSize.y;
            
            return new Vector2(x, y);
        }
        
        return screenPos;
    }
    
    Vector2 CanvasToScreenPosition(Vector2 canvasPos)
    {
        if (canvas == null) return canvasPos;
        
        return RectTransformUtility.WorldToScreenPoint(
            canvas.worldCamera ?? Camera.main,
            canvas.GetComponent<RectTransform>().TransformPoint(canvasPos));
    }
    
    /// <summary>
    /// Конвертирует мировую позицию в позицию Canvas
    /// </summary>
    Vector2 WorldToCanvasPosition(Vector3 worldPos)
    {
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Для ScreenSpaceOverlay конвертируем через камеру
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector2 screenPos = mainCam.WorldToScreenPoint(worldPos);
                return ScreenToCanvasPosition(screenPos);
            }
        }
        
        // Для других режимов используем камеру Canvas
        Camera canvasCam = canvas.worldCamera ?? Camera.main;
        if (canvasCam != null && canvas != null)
        {
            Vector2 screenPos = canvasCam.WorldToScreenPoint(worldPos);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                canvasCam,
                out Vector2 localPoint))
            {
                return localPoint;
            }
        }
        
        return Vector2.zero;
    }
}

