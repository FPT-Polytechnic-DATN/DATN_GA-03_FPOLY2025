using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class PlayerCombat : MonoBehaviour
{
    [Serializable]
    public class ComboStep
    {
        public int damage = 10;
        public float range = 0.8f;       // bán kính vùng đánh
        public float knockback = 5f;
        public float hitDelay = 0.1f;    // thời điểm gây sát thương (khớp animation)
        public float duration = 0.35f;   // tổng thời gian của đòn
        public string animTrigger = "Attack1";
    }

#if ENABLE_INPUT_SYSTEM
    [Header("Phím (Input System)")]
    [SerializeField] private Key meleeKey = Key.J;
    [SerializeField] private Key rangedKey = Key.K;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
    [Header("Phím (Input Manager cũ)")]
    [SerializeField] private string meleeButton = "Fire1";
    [SerializeField] private string rangedButton = "Fire2";
    [SerializeField] private string verticalAxis = "Vertical";
#endif

    [Header("Combo mặt đất (3 đòn)")]
    [SerializeField] private ComboStep[] comboSteps =
    {
        new ComboStep { damage = 10, animTrigger = "Attack1" },
        new ComboStep { damage = 12, animTrigger = "Attack2" },
        new ComboStep { damage = 20, range = 1.0f, knockback = 9f, duration = 0.5f, animTrigger = "Attack3" },
    };
    [SerializeField] private float comboWindow = 0.4f;     // thời gian nối đòn sau khi đánh xong
    [SerializeField] private float inputBuffer = 0.2f;     // nhớ phím bấm sớm

    [Header("Đòn đặc biệt")]
    [SerializeField] private ComboStep upAttack = new ComboStep { damage = 12, animTrigger = "AttackUp" };
    [SerializeField] private ComboStep downAttack = new ComboStep { damage = 15, animTrigger = "AttackDown" };
    [SerializeField] private float pogoForce = 12f;        // nảy lên khi chém xuống trúng

    [Header("Điểm đánh (đặt các Empty con của Player)")]
    [SerializeField] private Transform attackPoint;        // phía trước
    [SerializeField] private Transform upPoint;            // phía trên đầu
    [SerializeField] private Transform downPoint;          // phía dưới chân
    [SerializeField] private LayerMask enemyLayer;

    [Header("Kiếm khí tầm xa")]
    [SerializeField] private SwordSlash slashPrefab;
    [SerializeField] private Transform slashSpawn;
    [SerializeField] private int slashEnergyCost = 25;
    [SerializeField] private float slashCooldown = 0.5f;

    [Header("Năng lượng")]
    [SerializeField] private int maxEnergy = 100;
    [SerializeField] private int energyPerHit = 8;         // hồi khi đánh trúng

    [Header("Kiểm tra đất")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Cảm giác đánh (juice)")]
    [SerializeField] private float hitStopTime = 0.05f;

    public int Energy { get; private set; }
    public int MaxEnergy => maxEnergy;
    public bool IsAttacking { get; private set; }
    public event Action<int, int> EnergyChanged;

    private Rigidbody2D rb;
    private Health health;
    private Animator anim; // tùy chọn
    private int comboIndex;
    private float attackBufferedUntil;
    private float lastAttackEnd;
    private float nextSlashTime;
    private Coroutine hitStopRoutine;

    private int Facing => transform.localScale.x >= 0 ? 1 : -1;
    private bool IsGrounded =>
        groundCheck != null && Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        anim = GetComponentInChildren<Animator>();
        Energy = maxEnergy / 2;
    }

    private void Update()
    {
        if (health.IsDead) return;

        if (WasMeleePressedThisFrame())
            attackBufferedUntil = Time.time + inputBuffer;

        if (!IsAttacking && Time.time <= attackBufferedUntil)
        {
            attackBufferedUntil = 0f;
            StartMelee();
        }

        if (WasRangedPressedThisFrame())
            TryRangedAttack();

        // Hết thời gian nối combo -> về đòn đầu
        if (!IsAttacking && comboIndex > 0 && Time.time > lastAttackEnd + comboWindow)
            comboIndex = 0;
    }

    private bool WasMeleePressedThisFrame()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (meleeKey != Key.None)
                pressed |= keyboard[meleeKey].wasPressedThisFrame;
            pressed |= keyboard.leftCtrlKey.wasPressedThisFrame;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
            pressed |= mouse.leftButton.wasPressedThisFrame;

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
            pressed |= gamepad.buttonSouth.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetButtonDown(meleeButton);
#endif
        return pressed;
    }

    private bool WasRangedPressedThisFrame()
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (rangedKey != Key.None)
                pressed |= keyboard[rangedKey].wasPressedThisFrame;
            pressed |= keyboard.leftAltKey.wasPressedThisFrame;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
            pressed |= mouse.rightButton.wasPressedThisFrame;

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
            pressed |= gamepad.buttonEast.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetButtonDown(rangedButton);
#endif
        return pressed;
    }

    // ---------------- CẬN CHIẾN ----------------
    private void StartMelee()
    {
        float v = ReadVerticalInput();

        if (v > 0.5f)
            StartCoroutine(AttackRoutine(upAttack, upPoint, false, false));
        else if (v < -0.5f && !IsGrounded)
            StartCoroutine(AttackRoutine(downAttack, downPoint, true, false));
        else
            StartCoroutine(AttackRoutine(comboSteps[comboIndex], attackPoint, false, true));
    }

    private float ReadVerticalInput()
    {
        float value = 0f;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            bool upPressed = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
            bool downPressed = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
            value = (upPressed ? 1f : 0f) - (downPressed ? 1f : 0f);
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float gamepadValue = gamepad.leftStick.ReadValue().y;
            if (Mathf.Abs(gamepadValue) > Mathf.Abs(value))
                value = gamepadValue;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        float legacyValue = Input.GetAxisRaw(verticalAxis);
        if (Mathf.Abs(legacyValue) > Mathf.Abs(value))
            value = legacyValue;
#endif
        return value;
    }

    private IEnumerator AttackRoutine(ComboStep step, Transform point, bool isDownAttack, bool advancesCombo)
    {
        IsAttacking = true;
        if (anim) anim.SetTrigger(step.animTrigger);

        // Đòn trên mặt đất: khựng lại một chút để có trọng lượng
        if (IsGrounded) rb.linearVelocity = new Vector2(Facing * 1.5f, rb.linearVelocity.y);

        yield return new WaitForSeconds(step.hitDelay);
        bool hitSomething = DealDamage(step, point);

        if (hitSomething && isDownAttack)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, pogoForce); // nảy lên như Hollow Knight

        yield return new WaitForSeconds(Mathf.Max(0f, step.duration - step.hitDelay));

        IsAttacking = false;
        lastAttackEnd = Time.time;

        if (advancesCombo)
            comboIndex = (comboIndex + 1) % comboSteps.Length;
    }

    private bool DealDamage(ComboStep step, Transform point)
    {
        Collider2D[] cols = Physics2D.OverlapCircleAll(point.position, step.range, enemyLayer);
        var already = new HashSet<IDamageable>(); // tránh một quái bị trúng 2 lần do nhiều collider
        bool hit = false;

        foreach (Collider2D c in cols)
        {
            IDamageable target = c.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead || !already.Add(target)) continue;

            Vector2 dir = ((Vector2)c.transform.position - (Vector2)transform.position);
            if (Mathf.Abs(dir.x) < 0.01f) dir.x = Facing;

            target.TakeDamage(step.damage, dir, step.knockback);
            hit = true;
        }

        if (hit)
        {
            AddEnergy(energyPerHit);
            HitStop();
        }
        return hit;
    }

    // ---------------- TẦM XA ----------------
    private void TryRangedAttack()
    {
        if (slashPrefab == null || health.IsDead) return;
        if (Time.time < nextSlashTime || Energy < slashEnergyCost) return;

        nextSlashTime = Time.time + slashCooldown;
        AddEnergy(-slashEnergyCost);
        if (anim) anim.SetTrigger("Cast");

        Vector3 pos = slashSpawn != null ? slashSpawn.position : transform.position;
        SwordSlash slash = Instantiate(slashPrefab, pos, Quaternion.identity);
        slash.Launch(Facing);
    }

    // ---------------- TÀI NGUYÊN & JUICE ----------------
    public void AddEnergy(int amount)
    {
        Energy = Mathf.Clamp(Energy + amount, 0, maxEnergy);
        EnergyChanged?.Invoke(Energy, maxEnergy);
    }

    private void HitStop()
    {
        if (hitStopTime <= 0f) return;
        if (hitStopRoutine != null) StopCoroutine(hitStopRoutine);
        hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(hitStopTime);
        Time.timeScale = 1f;
    }

    private void OnDisable() => Time.timeScale = 1f;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (attackPoint) Gizmos.DrawWireSphere(attackPoint.position, comboSteps.Length > 0 ? comboSteps[0].range : 0.8f);
        if (upPoint) Gizmos.DrawWireSphere(upPoint.position, upAttack.range);
        if (downPoint) Gizmos.DrawWireSphere(downPoint.position, downAttack.range);
        Gizmos.color = Color.green;
        if (groundCheck) Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}
