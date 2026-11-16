using System.Collections;
using UnityEngine;

/// <summary>
/// Gắn vào Player. Khi chết: khóa điều khiển, chờ một nhịp, rồi nạp lại từ checkpoint gần nhất.
/// Khi scene vừa nạp xong: áp dụng dữ liệu lưu (vị trí, rune, máu) lên người chơi.
/// </summary>
[RequireComponent(typeof(Health))]
public class PlayerRespawn : MonoBehaviour
{
    [SerializeField] private float respawnDelay = 1.5f;

    private Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
    }

    private void OnEnable() => health.Died += OnDied;
    private void OnDisable() => health.Died -= OnDied;

    private void Start()
    {
        // Chạy sau mọi Awake nên RuneInventory, Health đã sẵn sàng
        if (SaveManager.Instance.PendingApply)
            SaveManager.Instance.ApplyTo(gameObject);
    }

    private void OnDied()
    {
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // Khóa điều khiển trong lúc chờ (chỗ này sau này gắn animation chết + fade màn hình)
        if (TryGetComponent(out PlayerController controller)) controller.enabled = false;
        if (TryGetComponent(out PlayerCombat combat)) combat.enabled = false;

        yield return new WaitForSecondsRealtime(respawnDelay);

        SaveManager.Instance.RequestLoad();
    }
}
