using UnityEngine;
using UnityEngine.Events;

/// <summary>Rune nằm trong màn chơi (trên bệ đá, sau câu đố, trong phòng bí mật...). Chạm vào là nhặt.</summary>
[RequireComponent(typeof(Collider2D))]
public class RunePickup : MonoBehaviour
{
    [SerializeField] private RuneData rune;
    [Tooltip("Gắn âm thanh, hiệu ứng hạt, popup lore...")]
    [SerializeField] private UnityEvent onCollected;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Start()
    {
        // Sau khi load game, rune đã nhặt thì không xuất hiện lại
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && player.TryGetComponent(out RuneInventory inv) && inv.Has(rune))
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        RuneInventory inv = other.GetComponentInParent<RuneInventory>();
        if (inv == null) return;

        if (inv.AddRune(rune))
        {
            onCollected?.Invoke();
            Destroy(gameObject);
        }
    }
}
