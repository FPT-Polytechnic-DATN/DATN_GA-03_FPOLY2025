using System;
using UnityEngine;

/// <summary>Mọi thứ có thể nhận sát thương (player, quái, boss, vật thể phá được).</summary>
public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(int amount, Vector2 hitDirection, float knockback);
}

[RequireComponent(typeof(Rigidbody2D))]
public class Health : MonoBehaviour, IDamageable
{
    [Header("Máu")]
    [SerializeField] private int maxHealth = 100;
    [Tooltip("Thời gian miễn thương sau mỗi lần trúng đòn")]
    [SerializeField] private float invincibleTime = 0.4f;

    [Header("Knockback")]
    [SerializeField] private bool receiveKnockback = true;
    [Tooltip("Tỉ lệ lực hất lên so với lực hất ngang")]
    [SerializeField] private float knockbackUpRatio = 0.4f;

    /// <summary>Bật từ bên ngoài, ví dụ lúc dash (i-frame).</summary>
    [NonSerialized] public bool externalInvincible;

    public int Current { get; private set; }
    public int Max => maxHealth;
    public bool IsDead { get; private set; }
    public bool IsInvincible => externalInvincible || Time.time < invincibleUntil;

    public event Action<int, Vector2> Damaged; // (sát thương, hướng đòn)
    public event Action Died;
    public event Action<int, int> HealthChanged; // (hiện tại, tối đa) cho HUD

    private Rigidbody2D rb;
    private float invincibleUntil;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        Current = maxHealth;
    }

    public void TakeDamage(int amount, Vector2 hitDirection, float knockback)
    {
        if (IsDead || IsInvincible || amount <= 0) return;

        Current = Mathf.Max(0, Current - amount);
        invincibleUntil = Time.time + invincibleTime;
        HealthChanged?.Invoke(Current, maxHealth);

        if (receiveKnockback && knockback > 0f)
        {
            Vector2 dir = hitDirection.normalized;
            rb.linearVelocity = new Vector2(dir.x * knockback, knockback * knockbackUpRatio);
        }

        Damaged?.Invoke(amount, hitDirection);

        if (Current <= 0)
        {
            IsDead = true;
            Died?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        HealthChanged?.Invoke(Current, maxHealth);
    }

    public void ResetHealth()
    {
        IsDead = false;
        Current = maxHealth;
        HealthChanged?.Invoke(Current, maxHealth);
    }
}
