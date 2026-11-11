using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, Hurt, Dead }

    [Header("Tham chiếu")]
    [SerializeField] private Transform player; // để trống -> tự tìm theo tag "Player"
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform attackPoint;

    [Header("Di chuyển")]
    [SerializeField] private float patrolSpeed = 1.5f;
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float patrolWaitTime = 1f;   // đứng chờ khi quay đầu

    [Header("Phát hiện")]
    [SerializeField] private float detectRange = 6f;
    [SerializeField] private float loseRange = 9f;        // xa hơn mức này thì bỏ đuổi
    [SerializeField] private float verticalTolerance = 2f;
    [SerializeField] private bool needLineOfSight = true;
    [SerializeField] private Vector2 eyeOffset = new Vector2(0f, 0.5f);

    [Header("Kiểm tra địa hình")]
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float ledgeCheckForward = 0.5f;
    [SerializeField] private float ledgeCheckDown = 1f;

    [Header("Tấn công")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRange = 1.2f;    // khoảng cách bắt đầu đánh
    [SerializeField] private float hitRadius = 0.7f;      // vùng gây sát thương thực tế
    [SerializeField] private float windupTime = 0.4f;     // báo hiệu đòn -> người chơi kịp né
    [SerializeField] private float recoveryTime = 0.5f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float attackKnockback = 7f;
    [SerializeField] private Color windupColor = new Color(1f, 0.4f, 0.4f);

    [Header("Bị đánh")]
    [SerializeField] private float hurtStunTime = 0.35f;
    [Tooltip("Bật cho boss/quái giáp: không bị ngắt đòn khi trúng")]
    [SerializeField] private bool superArmor = false;

    [Header("Chết")]
    [SerializeField] private float destroyDelay = 1.5f;

    public State Current { get; private set; } = State.Patrol;

    private Rigidbody2D rb;
    private Health health;
    private Animator anim; // tùy chọn
    private SpriteRenderer sprite;
    private Color baseColor = Color.white;

    private int facing = 1;
    private float stateUntil;     // dùng cho Hurt / chờ khi patrol
    private float nextAttackTime;
    private Coroutine attackRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        anim = GetComponentInChildren<Animator>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite) baseColor = sprite.color;
        facing = transform.localScale.x >= 0 ? 1 : -1;
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    private void FixedUpdate()
    {
        if (Current == State.Dead || player == null) return;

        switch (Current)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase:  UpdateChase();  break;
            case State.Attack: /* coroutine xử lý */ break;
            case State.Hurt:
                if (Time.time >= stateUntil) SetState(State.Chase);
                break;
        }

        if (anim) anim.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
    }

    // ---------------- PATROL ----------------
    private void UpdatePatrol()
    {
        if (CanSeePlayer())
        {
            SetState(State.Chase);
            return;
        }

        // Đang đứng chờ sau khi quay đầu
        if (Time.time < stateUntil)
        {
            Stop();
            return;
        }

        if (HitWall() || !HasGroundAhead())
        {
            Flip();
            stateUntil = Time.time + patrolWaitTime;
            Stop();
            return;
        }

        MoveX(facing * patrolSpeed);
    }

    // ---------------- CHASE ----------------
    private void UpdateChase()
    {
        float dx = player.position.x - transform.position.x;
        float dist = Vector2.Distance(transform.position, player.position);

        if (dist > loseRange)
        {
            SetState(State.Patrol);
            return;
        }

        FaceTowards(dx);

        // Đủ gần + hết hồi chiêu -> tấn công
        if (Mathf.Abs(dx) <= attackRange && Mathf.Abs(player.position.y - transform.position.y) <= verticalTolerance)
        {
            if (Time.time >= nextAttackTime)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
            else
            {
                Stop(); // chờ hồi chiêu, đứng sát người chơi
            }
            return;
        }

        // Không rơi khỏi mép vực khi đang đuổi
        if (HitWall() || !HasGroundAhead())
        {
            Stop();
            return;
        }

        MoveX(facing * chaseSpeed);
    }

    // ---------------- ATTACK ----------------
    private IEnumerator AttackRoutine()
    {
        SetState(State.Attack);
        Stop();
        if (anim) anim.SetTrigger("Attack");

        // Báo hiệu đòn (đổi màu) để người chơi có thời gian phản ứng
        if (sprite) sprite.color = windupColor;
        yield return new WaitForSeconds(windupTime);
        if (sprite) sprite.color = baseColor;

        // Gây sát thương nếu người chơi vẫn trong vùng đánh
        Vector2 center = attackPoint != null ? (Vector2)attackPoint.position
                                             : (Vector2)transform.position + Vector2.right * facing * 0.8f;
        Collider2D hit = Physics2D.OverlapCircle(center, hitRadius, playerLayer);
        if (hit != null)
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead)
                target.TakeDamage(attackDamage, new Vector2(facing, 0f), attackKnockback);
        }

        yield return new WaitForSeconds(recoveryTime);

        nextAttackTime = Time.time + attackCooldown;
        attackRoutine = null;
        SetState(State.Chase);
    }

    // ---------------- SỰ KIỆN MÁU ----------------
    private void OnDamaged(int amount, Vector2 hitDir)
    {
        if (Current == State.Dead) return;

        // Bị đánh thì tự động quay sang phía người chơi và truy đuổi
        if (player != null) FaceTowards(player.position.x - transform.position.x);

        if (superArmor)
        {
            if (Current == State.Patrol) SetState(State.Chase);
            return;
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
            if (sprite) sprite.color = baseColor;
        }

        stateUntil = Time.time + hurtStunTime;
        SetState(State.Hurt);
        if (anim) anim.SetTrigger("Hurt");
    }

    private void OnDied()
    {
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        if (sprite) sprite.color = baseColor;
        SetState(State.Dead);
        if (anim) anim.SetTrigger("Die");

        Stop();
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        rb.simulated = false;
        Destroy(gameObject, destroyDelay);
    }

    // ---------------- HÀM PHỤ ----------------
    private void SetState(State s) => Current = s;

    private bool CanSeePlayer()
    {
        Vector2 toPlayer = player.position - transform.position;
        if (toPlayer.magnitude > detectRange) return false;
        if (Mathf.Abs(toPlayer.y) > verticalTolerance) return false;

        // Chỉ thấy người chơi ở phía trước mặt (hoặc rất gần phía sau)
        bool inFront = Mathf.Sign(toPlayer.x) == facing || toPlayer.magnitude < detectRange * 0.3f;
        if (!inFront) return false;

        if (needLineOfSight)
        {
            Vector2 eye = (Vector2)transform.position + eyeOffset;
            Vector2 dir = (Vector2)player.position - eye;
            if (Physics2D.Raycast(eye, dir.normalized, dir.magnitude, groundLayer)) return false;
        }
        return true;
    }

    private bool HitWall()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.3f;
        return Physics2D.Raycast(origin, Vector2.right * facing, wallCheckDistance, groundLayer);
    }

    private bool HasGroundAhead()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.right * facing * ledgeCheckForward + Vector2.up * 0.2f;
        return Physics2D.Raycast(origin, Vector2.down, ledgeCheckDown, groundLayer);
    }

    private void MoveX(float vx) => rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    private void Stop() => rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

    private void FaceTowards(float dx)
    {
        if (Mathf.Abs(dx) < 0.05f) return;
        int want = dx > 0 ? 1 : -1;
        if (want != facing) Flip();
    }

    private void Flip()
    {
        facing = -facing;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * facing;
        transform.localScale = s;
    }

    private void OnDrawGizmosSelected()
    {
        int f = Application.isPlaying ? facing : (transform.localScale.x >= 0 ? 1 : -1);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, loseRange);
        Gizmos.color = Color.red;
        Vector3 c = attackPoint != null ? attackPoint.position : transform.position + Vector3.right * f * 0.8f;
        Gizmos.DrawWireSphere(c, hitRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.right * f * ledgeCheckForward + Vector3.up * 0.2f, Vector3.down * ledgeCheckDown);
    }
}
