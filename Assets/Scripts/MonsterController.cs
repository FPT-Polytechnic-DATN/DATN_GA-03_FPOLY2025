using System.Collections;
using UnityEngine;

/// <summary>
/// Điều khiển hành vi Quái vật (Monster / Enemy AI) trong game 2D Platformer:
/// 1. Tuần tra (Patrol): Di chuyển qua lại trái - phải trong bán kính 4 unit từ vị trí xuất phát.
/// 2. Phát hiện & Truy đuổi (Chase): Khi khoảng cách tới Player <= 6 unit, quái truy đuổi
///    đến cùng (đuổi đến khi giết được player hoặc bị player tiêu diệt).
/// 3. Tấn công (Attack): Khi khoảng cách <= 1 unit:
///    - Debug: "tan cong"
///    - Trừ 10 HP của Player
///    - Debug: "HP cua Player hien tai: [máu hiện tại]"
/// 4. Hỗ trợ nhận sát thương và bị Player tiêu diệt ("hoặc bị player giết thì thôi").
/// </summary>
[SelectionBase]
public class MonsterController : MonoBehaviour
{
    [Header("=== MỤC TIÊU (TARGET) ===")]
    [Tooltip("Tham chiếu tới PlayerController (nếu để trống script sẽ tự tìm trong Scene)")]
    [SerializeField] private PlayerController player;

    [Header("=== 1. TUẦN TRA (PATROL) ===")]
    [Tooltip("Bán kính tuần tra qua lại từ vị trí xuất phát ban đầu (mặc định 4 unit)")]
    [SerializeField] private float patrolRadius = 4.0f;

    [Tooltip("Tốc độ di chuyển khi tuần tra")]
    [SerializeField] private float patrolSpeed = 2.0f;

    [Header("=== 2. TRUY ĐUỔI (CHASE) ===")]
    [Tooltip("Khoảng cách phát hiện Player để bắt đầu truy đuổi (mặc định 6 unit)")]
    [SerializeField] private float detectionRange = 6.0f;

    [Tooltip("Tốc độ di chuyển khi truy đuổi")]
    [SerializeField] private float chaseSpeed = 3.2f;

    [Header("=== 3. TẤN CÔNG & KHỰNG LẠI (ATTACK & RECOVERY) ===")]
    [Tooltip("Khoảng cách tấn công Player (mặc định 1 unit)")]
    [SerializeField] private float attackRange = 1.0f;

    [Tooltip("Lượng sát thương mỗi lần tấn công trừ vào máu Player (mặc định 10 HP)")]
    [SerializeField] private float attackDamage = 10.0f;

    [Tooltip("Thời gian hồi chiêu giữa các đòn đánh (giây)")]
    [SerializeField] private float attackCooldown = 2.0f;

    [Tooltip("Thời gian quái vật bị khựng lại sau mỗi lần tấn công trước khi tấn công tiếp đòn nữa (mặc định 2 giây)")]
    [SerializeField] private float attackRecoveryDuration = 2.0f;

    [Tooltip("Thời gian Player bị khựng lại khi trúng đòn (mặc định 0.5 giây)")]
    [SerializeField] private float playerStunDuration = 0.5f;

    [Header("=== 4. CHỈ SỐ QUÁI (MONSTER STATS) ===")]
    [Tooltip("Máu tối đa của quái (mặc định 200)")]
    [SerializeField] private float maxHealth = 200.0f;

    [Tooltip("Máu hiện tại của quái")]
    [SerializeField] private float currentHealth = 200.0f;

    // Component references
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;

    // Trạng thái tuần tra
    private Vector3 spawnPosition;
    private bool movingRight = true;
    private bool isFacingRight = true;

    // Trạng thái truy đuổi & chiến đấu
    private bool isChasing = false;
    private bool isDead = false;
    private float nextAttackTime = 0f;
    private bool isRecovering = false;
    private Coroutine recoveryCoroutine;

    // Public properties
    public bool IsChasing => isChasing;
    public bool IsDead => isDead;
    public bool IsRecovering => isRecovering;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        currentHealth = maxHealth;
        isDead = false;

        // Khóa xoay Rigidbody2D nếu có để quái không bị đổ khi va chạm
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
    }

    private void Start()
    {
        // Ghi nhận vị trí xuất phát ban đầu làm tâm của bán kính tuần tra 4 unit
        spawnPosition = transform.position;

        // Tự động tìm kiếm Player nếu chưa gán trong Inspector
        FindPlayerTarget();
    }

    private void Update()
    {
        // Nếu quái đã chết thì dừng mọi hành vi
        if (isDead) return;

        // Nếu quái vật đang bị khựng lại sau đòn tấn công: dừng di chuyển và chờ hết thời gian khựng 1s
        if (isRecovering)
        {
            SetHorizontalVelocity(0f);
            return;
        }

        // Đảm bảo luôn có tham chiếu Player
        if (player == null)
        {
            FindPlayerTarget();
            if (player == null)
            {
                // Nếu chưa có player trong Scene thì tiếp tục tuần tra bình thường
                HandlePatrol();
                return;
            }
        }

        // Tính khoảng cách giữa quái và Player
        float distanceToPlayer = Vector2.Distance(transform.position, player.transform.position);

        // --- BƯỚC 1: KIỂM TRA PHÁT HIỆN PLAYER ĐỂ BẮT ĐẦU TRUY ĐUỔI ---
        // Khi khoảng cách giữa player và quái là từ 6 unit trở xuống -> tiến hành truy đuổi
        if (!isChasing && !player.IsDead)
        {
            if (distanceToPlayer <= detectionRange)
            {
                isChasing = true;
                Debug.Log("[MonsterController] Phat hien Player trong khoang cach <= " + detectionRange + " unit! Bat dau truy duoi den cung.");
            }
        }

        // --- BƯỚC 2: TRUY ĐUỔI HOẶC TUẦN TRA ---
        if (isChasing)
        {
            // QUY TẮC: "đuổi đến khi nào giết được player hoặc bị player giết thì thôi"
            // Nếu Player đã bị giết (IsDead hoặc CurrentHealth <= 0): quái dừng đuổi
            if (player.IsDead || player.CurrentHealth <= 0f)
            {
                isChasing = false;
                SetHorizontalVelocity(0f);
                Debug.Log("[MonsterController] Player da bi tieu diet! Quai ngung truy duoi.");
                return;
            }

            // Xử lý truy đuổi & tấn công
            HandleChaseAndAttack(distanceToPlayer);
        }
        else
        {
            // Nếu chưa phát hiện player: di chuyển qua lại trái phải trong bán kính 4 unit
            HandlePatrol();
        }
    }

    #region 1. Patrol Logic (Tuần tra trong bán kính 4 unit)
    /// <summary>
    /// Quái di chuyển qua lại trái phải trong phạm vi bán kính 4 unit quanh vị trí xuất phát
    /// </summary>
    private void HandlePatrol()
    {
        float leftLimit = spawnPosition.x - patrolRadius;
        float rightLimit = spawnPosition.x + patrolRadius;

        if (movingRight)
        {
            SetHorizontalVelocity(patrolSpeed);
            Flip(true);

            // Khi chạm hoặc vượt biên phải -> quay đầu sang trái
            if (transform.position.x >= rightLimit)
            {
                movingRight = false;
                Flip(false);
            }
        }
        else
        {
            SetHorizontalVelocity(-patrolSpeed);
            Flip(false);

            // Khi chạm hoặc vượt biên trái -> quay đầu sang phải
            if (transform.position.x <= leftLimit)
            {
                movingRight = true;
                Flip(true);
            }
        }
    }
    #endregion

    #region 2 & 3. Chase & Attack Logic
    /// <summary>
    /// Xử lý hành vi truy đuổi áp sát và tấn công Player
    /// </summary>
    private void HandleChaseAndAttack(float distanceToPlayer)
    {
        // Nếu đang khựng lại sau đòn đánh thì dừng di chuyển và chờ
        if (isRecovering)
        {
            SetHorizontalVelocity(0f);
            return;
        }

        // Xác định hướng ngang về phía Player (+1: sang phải, -1: sang trái)
        float directionX = (player.transform.position.x > transform.position.x) ? 1.0f : -1.0f;
        Flip(directionX > 0);

        // 3. Khi khoảng cách <= 1 unit: Tấn công Player
        if (distanceToPlayer <= attackRange)
        {
            // Dừng di chuyển ngang để thực hiện đòn tấn công
            SetHorizontalVelocity(0f);

            // Kiểm tra thời gian hồi chiêu đòn đánh
            if (Time.time >= nextAttackTime && !player.IsDead)
            {
                nextAttackTime = Time.time + attackCooldown;
                ExecuteAttack();
            }
        }
        // 2. Nếu khoảng cách > 1 unit: Tiếp tục truy đuổi áp sát Player
        else
        {
            SetHorizontalVelocity(directionX * chaseSpeed);
        }
    }

    /// <summary>
    /// Thực hiện đòn tấn công theo đúng yêu cầu đề bài:
    /// - Debug: tan cong
    /// - Trừ 10 HP của player
    /// - Debug: HP cua Player hien tai: [HP của player]
    /// </summary>
    private void ExecuteAttack()
    {
        // 1. Khi tấn công thì debug: tan cong
        Debug.Log("tan cong");

        // 2. Sau đó -10 HP của player
        player.TakeDamage(attackDamage);

        // 3. Sau khi trừ máu thì debug: HP cua Player hien tai: HP của player tại thời điểm đó
        Debug.Log("HP cua Player hien tai: " + player.CurrentHealth);

        // 4. Player bị khựng lại khi trúng đòn
        if (player != null && !player.IsDead)
        {
            player.Stun(playerStunDuration);
        }

        // 5. Quái vật bị khựng lại 2s sau khi tấn công trước khi tấn công tiếp đòn nữa
        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
        }
        recoveryCoroutine = StartCoroutine(AttackRecoveryRoutine(attackRecoveryDuration));
    }

    /// <summary>
    /// Coroutine xử lý trạng thái quái vật bị khựng lại 2s sau khi tấn công trước khi tấn công tiếp đòn nữa
    /// </summary>
    private IEnumerator AttackRecoveryRoutine(float duration)
    {
        isRecovering = true;
        SetHorizontalVelocity(0f);
        Debug.Log("[MonsterController] Quai vat bi khung lai " + duration + "s truoc khi tan cong tiep don nua...");

        yield return new WaitForSeconds(duration);

        isRecovering = false;
        recoveryCoroutine = null;
        Debug.Log("[MonsterController] Quai vat da het khung " + duration + "s, san sang cho don tiep theo!");
    }
    #endregion

    #region 4. Monster Damage & Death ("hoặc bị player giết thì thôi")
    private float lastSwordHitTime = -1f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        CheckSwordHit(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckSwordHit(collision.gameObject);
    }

    /// <summary>
    /// Kiểm tra va chạm với GameObject Sword (Tag: PlayerSword / Name: Sword)
    /// Khi va chạm sẽ -20 HP và in debug HP hiện tại của quái
    /// </summary>
    private void CheckSwordHit(GameObject hitObj)
    {
        if (isDead || hitObj == null) return;

        bool isSword = hitObj.name == "Sword" || SafeCompareTag(hitObj, "PlayerSword");
        if (isSword)
        {
            // Tránh trừ máu trùng lặp trong cùng 1 lần vung kiếm (cách nhau tối thiểu 0.2s)
            if (Time.time < lastSwordHitTime + 0.2f) return;
            lastSwordHitTime = Time.time;

            TakeDamage(20.0f);
        }
    }

    private static bool SafeCompareTag(GameObject obj, string tag)
    {
        if (obj == null) return false;
        try
        {
            return obj.CompareTag(tag);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Hàm nhận sát thương: Trừ máu quái và in Debug HP quái hiện tại
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        // Debug đúng theo yêu cầu đề bài: "HP quái hiện tại: [HP của quái tại thời điểm đó]"
        Debug.Log("HP quái hiện tại: " + currentHealth);

        // Bị đánh trúng cũng tự động kích hoạt truy đuổi ngay lập tức
        if (!isChasing && player != null && !player.IsDead)
        {
            isChasing = true;
            Debug.Log("[MonsterController] Quai bi danh trung! Tien hanh truy duoi nguoi choi.");
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Xử lý khi Quái bị tiêu diệt
    /// </summary>
    private void Die()
    {
        if (isDead) return;

        isDead = true;
        isChasing = false;
        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
            recoveryCoroutine = null;
        }
        isRecovering = false;
        SetHorizontalVelocity(0f);

        Debug.Log("[MonsterController] Quai da bi tieu diet hoan toan!");

        // Vô hiệu hóa va chạm để không cản đường Player
        if (col != null)
        {
            col.enabled = false;
        }

        // Tự động hủy Game Object sau 1.2s (để kịp hiển thị hiệu ứng nếu có)
        Destroy(gameObject, 1.2f);
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Thiết lập vận tốc di chuyển theo phương ngang (hỗ trợ cả Rigidbody2D và Transform fallback)
    /// </summary>
    private void SetHorizontalVelocity(float velocityX)
    {
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = new Vector2(velocityX, rb.linearVelocity.y);
#else
            rb.velocity = new Vector2(velocityX, rb.velocity.y);
#endif
        }
        else
        {
            transform.Translate(Vector2.right * (velocityX * Time.deltaTime));
        }
    }

    /// <summary>
    /// Tự động lật mặt sprite theo hướng di chuyển
    /// </summary>
    private void Flip(bool faceRight)
    {
        if (isFacingRight != faceRight)
        {
            isFacingRight = faceRight;
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (isFacingRight ? 1f : -1f);
            transform.localScale = scale;
        }
    }

    /// <summary>
    /// Tìm đối tượng Player trong Scene
    /// </summary>
    private void FindPlayerTarget()
    {
        if (player != null) return;

#if UNITY_2023_1_OR_NEWER
        player = FindFirstObjectByType<PlayerController>();
#else
        player = FindObjectOfType<PlayerController>();
#endif
    }
    #endregion

    #region Scene Gizmos
    private void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? spawnPosition : transform.position;

        // 1. Vùng tuần tra (4 unit trái - phải): Màu xanh lơ (Cyan)
        Gizmos.color = Color.cyan;
        Vector3 leftBound = new Vector3(center.x - patrolRadius, center.y, center.z);
        Vector3 rightBound = new Vector3(center.x + patrolRadius, center.y, center.z);
        Gizmos.DrawLine(leftBound + Vector3.up * 0.5f, leftBound + Vector3.down * 0.5f);
        Gizmos.DrawLine(rightBound + Vector3.up * 0.5f, rightBound + Vector3.down * 0.5f);
        Gizmos.DrawLine(leftBound, rightBound);

        // 2. Phạm vi phát hiện Player (6 unit): Màu vàng (Yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 3. Phạm vi tấn công (1 unit): Màu đỏ (Red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    #endregion
}
