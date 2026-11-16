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

    [Header("Chỉ số rune (chỉ Player cần, quái để trống)")]
    [SerializeField] private PlayerStats stats;

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
    private int baseMaxHealth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        baseMaxHealth = maxHealth;
        Current = maxHealth;
        if (stats == null) stats = GetComponent<PlayerStats>();
    }

    private void OnEnable()
    {
        if (stats != null) stats.StatsChanged += RefreshMaxHealth;
    }

    private void OnDisable()
    {
        if (stats != null) stats.StatsChanged -= RefreshMaxHealth;
    }

    /// <summary>Rune đổi máu tối đa: tăng thì cộng luôn máu hiện tại, giảm thì cắt xuống mức mới.</summary>
    private void RefreshMaxHealth()
    {
        if (IsDead) return;
        int newMax = Mathf.Max(1, Mathf.RoundToInt(stats.Get(StatType.MaxHealth, baseMaxHealth)));
        if (newMax == maxHealth) return;

        int diff = newMax - maxHealth;
        maxHealth = newMax;
        Current = diff > 0 ? Current + diff : Mathf.Min(Current, newMax);
        HealthChanged?.Invoke(Current, maxHealth);
    }

    public void TakeDamage(int amount, Vector2 hitDirection, float knockback)
    {
        if (IsDead || IsInvincible || amount <= 0) return;

        if (stats != null)
        {
            float reduction = Mathf.Clamp(stats.Get(StatType.DamageReduction, 0f), -0.5f, 0.75f);
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - reduction)));
        }

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
