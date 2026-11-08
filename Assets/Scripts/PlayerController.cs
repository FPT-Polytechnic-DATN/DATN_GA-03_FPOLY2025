using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Điều khiển nhân vật 2D/Platformer:
/// 1. Di chuyển Trái / Phải (A/D) tốc độ 3.5 m/s, tự động xoay hướng nhân vật.
/// 2. Lướt (Shift) theo hướng nhân vật đang nhìn + i-frame (bất tử) 1 giây với hiệu ứng nhấp nháy.
/// 3. Nhảy (Space) và Double Jump: chỉ nhảy tối đa 2 lần. Khi đã double jump trên không,
///    phải đáp đất thì mới được bấm Space để nhảy tiếp (chống spam vô hạn).
/// Hỗ trợ cả New Input System và Legacy Input Manager.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("=== DI CHUYỂN (A / D) ===")]
    [Tooltip("Tốc độ di chuyển theo phương ngang (m/s)")]
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("=== NHẢY & DOUBLE JUMP (SPACE) ===")]
    [Tooltip("Lực nhảy lần 1 từ mặt đất")]
    [SerializeField] private float jumpForce = 8.5f;

    [Tooltip("Lực nhảy lần 2 (Double Jump) trên không")]
    [SerializeField] private float doubleJumpForce = 8.0f;

    [Header("=== KIỂM TRA MẶT ĐẤT (GROUND CHECK) ===")]
    [Tooltip("Transform vị trí chân nhân vật để kiểm tra mặt đất (nếu để trống sẽ tự dùng Collider)")]
    [SerializeField] private Transform groundCheck;

    [Tooltip("Bán kính vùng kiểm tra mặt đất quanh groundCheck")]
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Tooltip("Layer được tính là mặt đất")]
    [SerializeField] private LayerMask groundLayer;

    [Header("=== LƯỚT & I-FRAME (SHIFT) ===")]
    [Tooltip("Tốc độ khi lướt")]
    [SerializeField] private float dashSpeed = 12f;

    [Tooltip("Thời gian thực hiện cú lướt (giây)")]
    [SerializeField] private float dashDuration = 0.2f;

    [Tooltip("Thời gian bất tử (i-frame) sau khi bấm Shift (giây)")]
    [SerializeField] private float iFrameDuration = 1.0f;

    [Tooltip("Thời gian hồi chiêu lướt (giây)")]
    [SerializeField] private float dashCooldown = 1.0f;

    [Header("=== TRỌNG LỰC ===")]
    [Tooltip("Hệ số trọng lực khi rơi để nhảy có cảm giác tự nhiên hơn")]
    [SerializeField] private float gravityScale = 2.5f;

    // Component references
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;

    // Trạng thái di chuyển
    private float horizontalInput;
    private bool isFacingRight = true;

    // Trạng thái nhảy & giới hạn Double Jump
    private bool isGrounded;
    private bool canDoubleJump;
    private float lastJumpTime;
    private const float JUMP_COOLDOWN = 0.15f; // Khoảng thời gian chống nhận nhầm đất khi vừa cất cánh
    private bool jumpRequest;

    // Bộ lọc va chạm mặt đất (tránh va vào chính player)
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    private ContactFilter2D groundFilter;

    // Trạng thái lướt & i-frame
    private bool isDashing;
    private bool canDash = true;
    private bool isInvincible;
    private float originalGravity;
    private Coroutine dashCoroutine;
    private Coroutine iFrameCoroutine;

    // Properties công khai để các hệ thống khác (Combat, Animation, UI) có thể truy cập
    public bool IsGrounded => isGrounded;
    public bool CanDoubleJump => canDoubleJump;
    public int JumpsRemaining => isGrounded ? 2 : (canDoubleJump ? 1 : 0);
    public bool IsDashing => isDashing;
    public bool IsInvincible => isInvincible;
    public bool CanDash => canDash;
    public int FacingDirection => isFacingRight ? 1 : -1;
    public float MoveSpeed => moveSpeed;

    // Events thông báo hành động (dành cho âm thanh, hiệu ứng hạt, camera shake...)
    public System.Action OnJump;
    public System.Action OnDoubleJump;
    public System.Action OnDash;
    public System.Action<bool> OnInvincibilityChanged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Thiết lập chuẩn cho Rigidbody2D để di chuyển mượt mà và không bị xoay tròn
        rb.freezeRotation = true;
        rb.gravityScale = gravityScale;
        originalGravity = gravityScale;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Thiết lập layer kiểm tra mặt đất
        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Default");
            if (groundLayer.value == 0)
            {
                groundLayer = ~(1 << gameObject.layer);
            }
        }

        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayer);
        groundFilter.useTriggers = false;

        canDoubleJump = true;
    }

    private void Update()
    {
        // 1. Đọc phím điều khiển
        ProcessInputs();

        // 2. Kiểm tra mặt đất chính xác
        CheckGrounded();

        // 3. Xử lý nhảy (Space)
        if (jumpRequest)
        {
            jumpRequest = false;
            HandleJump();
        }
    }

    private void FixedUpdate()
    {
        // Nếu đang trong trạng thái lướt thì không nhận lực di chuyển thông thường
        if (isDashing) return;

        // 1. Áp dụng vận tốc di chuyển trái phải (A và D) với tốc độ 3.5 m/s
        ApplyMovement();

        // 2. Quay mặt nhân vật theo hướng di chuyển
        UpdateFacingDirection();
    }

    #region Input Handling
    /// <summary>
    /// Đọc phím từ New Input System hoặc Legacy Input Manager
    /// </summary>
    private void ProcessInputs()
    {
        if (isDashing)
        {
            horizontalInput = 0f;
            return;
        }

        // --- Đọc phím A / D hoặc mũi tên trái / phải ---
        bool leftPressed = false;
        bool rightPressed = false;
        bool jumpPressed = false;
        bool dashPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            leftPressed |= Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed;
            rightPressed |= Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
            jumpPressed |= Keyboard.current.spaceKey.wasPressedThisFrame;
            dashPressed |= Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        leftPressed |= Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        rightPressed |= Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        jumpPressed |= Input.GetKeyDown(KeyCode.Space);
        dashPressed |= Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
#endif

        // Tính toán hướng di chuyển ngang (-1: Trái, 0: Đứng yên, 1: Phải)
        horizontalInput = 0f;
        if (leftPressed) horizontalInput -= 1f;
        if (rightPressed) horizontalInput += 1f;

        // Bắt sự kiện Nhảy (Space)
        if (jumpPressed)
        {
            jumpRequest = true;
        }

        // Bắt sự kiện Lướt (Shift)
        if (dashPressed && canDash && !isDashing)
        {
            StartDash();
        }
    }
    #endregion

    #region Movement (A / D)
    /// <summary>
    /// Thực hiện di chuyển ngang với tốc độ moveSpeed = 3.5 m/s
    /// </summary>
    private void ApplyMovement()
    {
        float targetVelocityX = horizontalInput * moveSpeed;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector2(targetVelocityX, rb.linearVelocity.y);
#else
        rb.velocity = new Vector2(targetVelocityX, rb.velocity.y);
#endif
    }

    /// <summary>
    /// Tự động xoay mặt nhân vật theo hướng di chuyển (trái / phải)
    /// </summary>
    private void UpdateFacingDirection()
    {
        if (horizontalInput > 0.01f && !isFacingRight)
        {
            Flip();
        }
        else if (horizontalInput < -0.01f && isFacingRight)
        {
            Flip();
        }
    }

    /// <summary>
    /// Đảo chiều Scale theo trục X để lật sprite và các con của nhân vật
    /// </summary>
    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 localScale = transform.localScale;
        localScale.x = Mathf.Abs(localScale.x) * (isFacingRight ? 1f : -1f);
        transform.localScale = localScale;
    }
    #endregion

    #region Jump & Double Jump (Space)
    /// <summary>
    /// Kiểm tra mặt đất chính xác, loại trừ triệt để Collider của chính Player để không bao giờ bị nhận nhầm đất trên không.
    /// </summary>
    private void CheckGrounded()
    {
        // 1. Nếu vừa mới nhảy và đang trong thời gian cất cánh thì chắc chắn chưa chạm đất
        if (Time.time < lastJumpTime + JUMP_COOLDOWN)
        {
            isGrounded = false;
            return;
        }

        bool groundDetected = false;

        // 2. Nếu có gán Transform groundCheck ở chân nhân vật
        if (groundCheck != null)
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(groundCheck.position, groundCheckRadius, groundLayer);
            foreach (var hit in colliders)
            {
                // Bỏ qua chính player, các đối tượng con và các Trigger
                if (hit != null && hit != col && !hit.transform.IsChildOf(transform) && !hit.isTrigger)
                {
                    groundDetected = true;
                    break;
                }
            }
        }
        // 3. Nếu không gán groundCheck: dùng col.Cast quét xuống dưới đáy Collider của Player
        else if (col != null)
        {
            groundFilter.SetLayerMask(groundLayer);
            int hitCount = col.Cast(Vector2.down, groundFilter, groundHits, 0.08f);
            for (int i = 0; i < hitCount; i++)
            {
                var hit = groundHits[i];
                if (hit.collider != null && hit.collider != col && !hit.collider.transform.IsChildOf(transform) && !hit.collider.isTrigger)
                {
                    // Chỉ tính là mặt đất nếu góc pháp tuyến hướng lên (mặt sàn, dốc đứng < 60 độ)
                    if (hit.normal.y > 0.5f)
                    {
                        groundDetected = true;
                        break;
                    }
                }
            }
        }
        // 4. Fallback bằng RaycastAll
        else
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, Vector2.down, 1.1f, groundLayer);
            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider.transform != transform && !hit.collider.transform.IsChildOf(transform) && !hit.collider.isTrigger)
                {
                    groundDetected = true;
                    break;
                }
            }
        }

        // 5. Nếu nhân vật đang bay vọt lên trên (vận tốc Y dương > 0.2) thì không tính là tiếp đất
#if UNITY_6000_0_OR_NEWER
        float currentVy = rb.linearVelocity.y;
#else
        float currentVy = rb.velocity.y;
#endif
        if (currentVy > 0.2f)
        {
            groundDetected = false;
        }

        isGrounded = groundDetected;

        // Khi đã thực sự đáp đất: HỒI LẠI QUYỀN DOUBLE JUMP
        if (isGrounded)
        {
            canDoubleJump = true;
        }
    }

    /// <summary>
    /// Xử lý logic Nhảy từ đất và Double Jump trên không.
    /// Khi đã double jump thì KHÓA HOÀN TOÀN cho tới khi đáp đất.
    /// </summary>
    private void HandleJump()
    {
        if (isGrounded)
        {
            // Nhảy lần 1 từ mặt đất
            ExecuteJump(jumpForce);
            isGrounded = false;
            canDoubleJump = true; // Cho phép thực hiện 1 lần Double Jump khi đang trên không
            lastJumpTime = Time.time;
            OnJump?.Invoke();
        }
        else if (canDoubleJump)
        {
            // Nhảy lần 2 (Double Jump) trên không
            ExecuteJump(doubleJumpForce);
            canDoubleJump = false; // ĐÃ DOUBLE JUMP -> KHÓA CHẶT, SPAM SPACE SẼ KHÔNG NHẢY NỮA CHO ĐẾN KHI CHẠM ĐẤT!
            lastJumpTime = Time.time;
            OnDoubleJump?.Invoke();
        }
        // Nếu không chạm đất VÀ canDoubleJump == false: Bỏ qua hoàn toàn, không nhảy được!
    }

    private void ExecuteJump(float force)
    {
#if UNITY_6000_0_OR_NEWER
        // Đặt lại vận tốc Y về 0 trước khi thêm lực nhảy để độ cao nhảy luôn đồng đều
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, force);
#else
        rb.velocity = new Vector2(rb.velocity.x, force);
#endif
    }
    #endregion

    #region Dash & I-Frame (Shift)
    /// <summary>
    /// Kích hoạt lướt theo hướng đang nhìn và kích hoạt i-frame 1 giây
    /// </summary>
    private void StartDash()
    {
        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
        }
        dashCoroutine = StartCoroutine(DashRoutine());

        // Kích hoạt trạng thái bất tử (i-frame) kéo dài 1 giây
        if (iFrameCoroutine != null)
        {
            StopCoroutine(iFrameCoroutine);
        }
        iFrameCoroutine = StartCoroutine(IFrameRoutine());

        OnDash?.Invoke();
    }

    /// <summary>
    /// Coroutine xử lý hành động lướt nhanh theo phương ngang
    /// </summary>
    private IEnumerator DashRoutine()
    {
        canDash = false;
        isDashing = true;

        // Tạm thời bỏ trọng lực để cú lướt bay ngang hoàn hảo ngay cả trên không
        rb.gravityScale = 0f;

        // Lướt về hướng nhân vật đang nhìn (+1 nếu nhìn phải, -1 nếu nhìn trái)
        float dashDirection = FacingDirection;
        float dashVelocityX = dashDirection * dashSpeed;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = new Vector2(dashVelocityX, 0f);
#else
        rb.velocity = new Vector2(dashVelocityX, 0f);
#endif

        yield return new WaitForSeconds(dashDuration);

        // Kết thúc lướt, hoàn trả trọng lực
        rb.gravityScale = originalGravity;
        isDashing = false;

        // Chờ hết thời gian hồi chiêu (dashCooldown) để được lướt tiếp
        float remainingCooldown = Mathf.Max(0f, dashCooldown - dashDuration);
        if (remainingCooldown > 0f)
        {
            yield return new WaitForSeconds(remainingCooldown);
        }

        canDash = true;
    }

    /// <summary>
    /// Coroutine xử lý thời gian bất tử (i-frame) 1 giây và hiệu ứng nhấp nháy thị giác
    /// </summary>
    private IEnumerator IFrameRoutine()
    {
        isInvincible = true;
        OnInvincibilityChanged?.Invoke(true);

        float elapsed = 0f;
        Color normalColor = Color.white;
        Color flashColor = new Color(1f, 1f, 1f, 0.4f); // Bán trong suốt khi bất tử

        if (spriteRenderer != null)
        {
            normalColor = spriteRenderer.color;
            flashColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.4f);
        }

        // Nhấp nháy trong suốt thời gian i-frame (1s)
        while (elapsed < iFrameDuration)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = flashColor;
            }
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = normalColor;
            }
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        // Đảm bảo hoàn trả màu sắc bình thường
        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }

        isInvincible = false;
        OnInvincibilityChanged?.Invoke(false);
    }
    #endregion

    #region Debug Gizmos
    private void OnDrawGizmosSelected()
    {
        // Màu sắc trực quan trong Scene view:
        // Xanh lá = Đang chạm đất
        // Vàng = Đang trên không và CÒN lượt Double Jump
        // Đỏ = Đang trên không và ĐÃ HẾT lượt nhảy (khóa nhảy cho đến khi chạm đất)
        if (isGrounded)
        {
            Gizmos.color = Color.green;
        }
        else if (canDoubleJump)
        {
            Gizmos.color = Color.yellow;
        }
        else
        {
            Gizmos.color = Color.red;
        }

        if (groundCheck != null)
        {
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        else
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c != null)
            {
                Vector3 center = new Vector3(c.bounds.center.x, c.bounds.min.y, 0f);
                Gizmos.DrawWireCube(center, new Vector3(c.bounds.size.x * 0.85f, 0.08f, 0f));
            }
        }

        // Vẽ mũi tên chỉ hướng đang quay mặt
        Gizmos.color = Color.cyan;
        Vector3 forwardPos = transform.position + Vector3.right * FacingDirection * 1.0f;
        Gizmos.DrawLine(transform.position, forwardPos);
    }
    #endregion
}