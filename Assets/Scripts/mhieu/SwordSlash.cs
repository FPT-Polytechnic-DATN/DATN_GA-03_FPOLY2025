using UnityEngine;

/// <summary>Cơ quan/công tắc nào muốn bị kiếm khí kích hoạt thì implement interface này.</summary>
public interface IActivatable
{
    void Activate();
}

[RequireComponent(typeof(Collider2D))]
public class SwordSlash : MonoBehaviour
{
    [SerializeField] private float speed = 14f;
    [SerializeField] private int damage = 15;
    [SerializeField] private float knockback = 6f;
    [SerializeField] private float lifetime = 1.2f;
    [SerializeField] private bool piercing = false;   // xuyên qua nhiều quái
    [SerializeField] private LayerMask damageLayers;  // Enemy
    [SerializeField] private LayerMask groundLayers;  // Ground (đụng là biến mất)

    private int direction = 1;

    /// <summary>Gọi ngay sau khi Instantiate.</summary>
    public void Launch(int dir)
    {
        direction = dir >= 0 ? 1 : -1;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * direction; // lật sprite theo hướng bay
        transform.localScale = s;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.Translate(Vector2.right * (direction * speed * Time.deltaTime), Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        int layerBit = 1 << other.gameObject.layer;

        // Kích hoạt cơ quan ở xa (câu đố)
        IActivatable activatable = other.GetComponentInParent<IActivatable>();
        if (activatable != null)
        {
            activatable.Activate();
            if (!piercing) Destroy(gameObject);
            return;
        }

        if ((damageLayers.value & layerBit) != 0)
        {
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead)
            {
                target.TakeDamage(damage, new Vector2(direction, 0f), knockback);
                if (!piercing) Destroy(gameObject);
            }
        }
        else if ((groundLayers.value & layerBit) != 0)
        {
            Destroy(gameObject);
        }
    }
}
