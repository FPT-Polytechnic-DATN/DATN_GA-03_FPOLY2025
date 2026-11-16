using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Checkpoint kiêm bàn thờ rune.
/// - Bước vào vùng: lưu game, mở quyền đổi rune.
/// - Bấm E: nghỉ (hồi đầy máu, lưu lại). Gắn UnityEvent onRest để mở UI rune.
/// Đặt trước đoạn khó và trước phòng boss như tài liệu thiết kế.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Vị trí hồi sinh. Để trống = vị trí của chính checkpoint")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private bool autoSaveOnEnter = true;
    [SerializeField] private UnityEvent onActivated;
    [SerializeField] private UnityEvent onRest; // mở UI bàn thờ rune, phát âm thanh...

    private Health playerHealth;
    private RuneInventory playerRunes;
    private bool playerInside;

    private Vector2 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerHealth = other.GetComponentInParent<Health>();
        playerRunes = other.GetComponentInParent<RuneInventory>();
        playerInside = true;

        if (playerRunes != null) playerRunes.AtAltar = true;
        if (autoSaveOnEnter) SaveGame();
        onActivated?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInside = false;
        if (playerRunes != null) playerRunes.AtAltar = false;
    }

    private void Update()
    {
        if (playerInside && InteractPressed()) Rest();
    }

    private void Rest()
    {
        if (playerHealth != null && !playerHealth.IsDead) playerHealth.Heal(playerHealth.Max);
        SaveGame();
        onRest?.Invoke();
    }

    private void SaveGame() => SaveManager.Instance.Save(RespawnPosition, playerRunes);

    private static bool InteractPressed()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null) pressed |= Keyboard.current.eKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.E);
#endif
        return pressed;
    }
}
