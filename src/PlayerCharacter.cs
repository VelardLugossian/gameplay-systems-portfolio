using System.Collections;
using UnityEngine;
using TMPro;

public class PlayerCharacter : Character
{
    protected CharacterController characterController;
    protected Animator animator;

    [Header("Mostrar Controles")]
    public GameObject ayudaImagen;
    private bool ayudaVisible = false;

    [Header("Inventario")]
    public float currentKeys = 0;
    public float currentRPotion = 0;
    public float currentGPotion = 0;
    public float currentBPotion = 0;

    [Header("Pociones")]
    public int healPerPotion = 20;
    public AudioClip drinkPotionSound;
    public TextMeshProUGUI potionRText, potionGText, potionBText, keyText;

    [Header("Atributos dinámicos")]
    [SerializeField] protected float movementSpeed = 7f;
    [SerializeField] protected float jumpSpeed = 10f;
    protected Vector2 input;
    protected Vector3 direction;
    protected float yVelocity;
    public float gravity = -20f;
    protected float rotationSpeed = 720f;

    // Edge rotate (desde CameraController)
    protected CameraController camController;
    [SerializeField] protected float edgeTurnSpeed = 240f; // deg/s al tocar borde

    [Header("Turn In Place")]
    [SerializeField] private float turnSideStepDeg = 30f;
    [SerializeField] private float turnSideStepSpeed = 1.0f;
    [SerializeField] private float turnMinInputMag = 0.1f;
    private float lastYaw;

    // Reorientación rápida con click derecho
    private bool reorientHeld = false;

    public float crouchHeight = 2.6f;
    private float? lastGroundedTime;
    private float? jumpButtonPressedTime;
    private bool isJumping;
    private bool isGrounded;
    private bool isShiftActive = false;
    private bool isCtrlActive = false;
    private bool isCrouching = false;

    private IInteractable currentInteractable;

    private CapsuleCollider capsuleCollider;
    private float originalHeight;
    private Vector3 originalCenter;

    [Header("Detección auxiliar de suelo")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.3f;
    public LayerMask groundMask;

    public AudioClip castSound;

    [Header("Animación de casteo")]
    public bool isCasting1 = false;

    [Header("Cámara")]
    [SerializeField] protected Transform cameraTransform;
    public float MovementSpeed { get => movementSpeed; set => movementSpeed = value; }
    public float JumpSpeed { get => jumpSpeed; set => jumpSpeed = value; }

    // Candado de orientación mientras se apunta (lo activa Elf)
    [HideInInspector] public bool aimMoveLock = false;

    protected override void Awake()
    {
        base.Awake();

        camController = Camera.main ? Camera.main.GetComponent<CameraController>() : null;
        lastYaw = transform.eulerAngles.y;

        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        // Buscar automáticamente el capsule collider del personaje.
        capsuleCollider = GetComponentInChildren<CapsuleCollider>();
        if (capsuleCollider == null)
        {
            Debug.LogError("⚠ No se encontró ningún CapsuleCollider en los hijos del personaje.");
        }
        else
        {
            originalHeight = capsuleCollider.height;
            originalCenter = capsuleCollider.center;
        }

        // Buscar automáticamente la cámara activa del juego
        cameraTransform = Camera.main?.transform;
        if (cameraTransform == null)
        {
            Debug.LogWarning("⚠ No se encontró la cámara principal. La rotación basada en cámara no funcionará.");
        }
    }

    protected virtual void Update()
    {
        // 1) Primero dejamos que Character haga su trabajo (regenerar stamina)
        base.Update();

        // 2) Luego todo lo demás
        HandleInput();
        HandleMovement();
        HandleCombat();

        if (Input.GetKeyDown(KeyCode.Alpha1)) TryDrinkRPotion();
        if (Input.GetKeyDown(KeyCode.Alpha2)) TryDrinkGPotion();
        if (Input.GetKeyDown(KeyCode.Alpha3)) TryDrinkBPotion();

        // Acción (F)
        if (Input.GetKeyDown(KeyCode.F) && currentInteractable != null)
        {
            currentInteractable.Interact(this);
        }

        // Ayuda (F1)
        if (Input.GetKeyDown(KeyCode.F1))
        {
            ayudaVisible = !ayudaVisible;
            ayudaImagen.SetActive(ayudaVisible);
        }

        // Reset automático tras Cast1
        if (isCasting1)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsName("Cast1") && state.normalizedTime >= 1f)
            {
                isCasting1 = false;
                animator.SetBool("IsCasting1", false);
            }
        }
    }

    public virtual void HandleInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        input = new Vector2(h, v).normalized;

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            isShiftActive = !isShiftActive;

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
        {
            isCtrlActive = !isCtrlActive;
            ToggleStealthMode();
        }

        if (Input.GetButtonDown("Jump"))
            jumpButtonPressedTime = Time.time;

        // RMB = reorientación rápida
        reorientHeld = Input.GetMouseButton(1);
    }

    public virtual void HandleMovement()
    {
        if (characterController == null || cameraTransform == null) return;

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        // Reorientación explícita (RMB): alinea yaw a la cámara
        if (reorientHeld)
        {
            Vector3 flatCamFwd = cameraTransform.forward; flatCamFwd.y = 0f;
            if (flatCamFwd.sqrMagnitude > 0.0001f)
            {
                Quaternion lookToCam = Quaternion.LookRotation(flatCamFwd.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, lookToCam, rotationSpeed * Time.deltaTime);
            }
        }

        Vector3 movementDirection = camForward * input.y + camRight * input.x;

        float inputMag = Mathf.Clamp01(movementDirection.magnitude);
        if (isShiftActive) inputMag /= 2f;

        if (characterController.isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        yVelocity += gravity * Time.deltaTime;

        // Ventanas robustas
        bool recentGrounded = lastGroundedTime.HasValue && (Time.time - lastGroundedTime.Value <= 0.2f);
        bool groundedNow = characterController.isGrounded || IsReallyGrounded();
        bool jumpBuffered = jumpButtonPressedTime.HasValue && (Time.time - jumpButtonPressedTime.Value <= 0.2f);

        if (recentGrounded || groundedNow)
        {
            characterController.stepOffset = 0.3f;
            yVelocity = -0.5f;
            animator.SetBool("IsGrounded", true);
            isGrounded = true;
            animator.SetBool("IsJumping", false);
            isJumping = false;
            animator.SetBool("IsFalling", false);

            if (jumpBuffered)
            {
                yVelocity = JumpSpeed;
                animator.SetBool("IsJumping", true);
                isJumping = true;
                jumpButtonPressedTime = null;
                lastGroundedTime = null;
                animator.SetBool("IsCrouching", false);
                isCrouching = false;
            }
        }
        else
        {
            characterController.stepOffset = 0;
            animator.SetBool("IsGrounded", false);
            isGrounded = false;

            if ((isJumping && yVelocity < 0) || yVelocity < -2)
            {
                if (!isGrounded)
                    StartCoroutine(ConfirmGroundedDelay());

                animator.SetBool("IsFalling", true);
            }
        }

        // EDGE ROTATE: el personaje gira con la señal de los bordes (no la cámara)
        if (camController != null && camController.edgeRotateEnabled)
        {
            float edge = camController.edgeYawInput; // –1..1
            if (Mathf.Abs(edge) > 0f)
            {
                float deltaYaw = edge * edgeTurnSpeed * Time.deltaTime;
                transform.Rotate(0f, deltaYaw, 0f);
            }
        }

        if (movementDirection != Vector3.zero)
        {
            animator.SetBool("IsMoving", true);

            // No auto-rotar hacia movementDirection si edge-rotate o aimMoveLock están activos
            bool blockingEdge = (camController != null && camController.edgeRotateEnabled);
            if (!reorientHeld && !aimMoveLock && !blockingEdge)
            {
                Quaternion toRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
            }
        }
        else
        {
            animator.SetBool("IsMoving", false);
        }

        Vector3 horizontal = movementDirection * inputMag * movementSpeed;
        Vector3 velocity = new Vector3(horizontal.x, yVelocity, horizontal.z);

        // TURN-IN-PLACE SIDESTEP (una sola vez)
        float currYaw = transform.eulerAngles.y;
        float deltaYawFrame = Mathf.DeltaAngle(lastYaw, currYaw);
        lastYaw = currYaw;

        if (isGrounded && input.magnitude < turnMinInputMag && Mathf.Abs(deltaYawFrame) > turnSideStepDeg)
        {
            float sgn = Mathf.Sign(deltaYawFrame);
            Vector3 side = transform.right * sgn * turnSideStepSpeed;
            velocity += new Vector3(side.x, 0f, side.z);
        }

        var flags = characterController.Move(velocity * Time.deltaTime);

        // Snap-to-ground si toca abajo
        if ((flags & CollisionFlags.Below) != 0)
        {
            isGrounded = true;
            yVelocity = -0.5f;
        }

        animator.SetFloat("Input Magnitude", inputMag, 0.05f, Time.deltaTime);
    }

    private bool IsReallyGrounded()
    {
        if (groundCheck == null)
            return characterController != null && characterController.isGrounded;

        return Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private IEnumerator ConfirmGroundedDelay()
    {
        yield return new WaitForSeconds(0.1f);

        if (characterController.isGrounded)
        {
            isGrounded = true;
            animator.SetBool("IsGrounded", true);
            animator.SetBool("IsJumping", false);
            animator.SetBool("IsFalling", false);
            isJumping = false;

            Debug.Log("✅ Confirmado: personaje en el suelo tras la caída.");
        }
    }

    public virtual void HandleCombat() { }

    private void ToggleStealthMode()
    {
        if (capsuleCollider == null) return;

        if (isCtrlActive)
        {
            animator.SetBool("IsCrouching", true);
            isCrouching = true;
            capsuleCollider.height = crouchHeight;
            capsuleCollider.center = new Vector3(originalCenter.x, crouchHeight / 2, originalCenter.z);
        }
        else
        {
            animator.SetBool("IsCrouching", false);
            isCrouching = false;
            capsuleCollider.height = originalHeight;
            capsuleCollider.center = originalCenter;
        }
    }

    // Llamado por cualquier hechizo cuando se castee correctamente
    public void PlayCastAnimation()
    {
        if (animator != null)
        {
            isCasting1 = true;
            animator.SetBool("IsCasting1", true);
        }

        if (castSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(castSound);
        }
    }

    #region Sistema de detección de Interactuables

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out IInteractable interactable))
        {
            currentInteractable = interactable;
            Debug.Log("Interactuable detectado: " + interactable.GetGameObject().name);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out IInteractable interactable) && interactable == currentInteractable)
        {
            currentInteractable = null;
        }
    }
    #endregion

    public void SetInteractable(IInteractable interactable) => currentInteractable = interactable;

    public void ClearInteractable(IInteractable interactable)
    {
        if (currentInteractable == interactable) currentInteractable = null;
    }

    #region Pociones Rojas
    public void AddRPotion(int amount = 1) { currentRPotion += amount; UpdateRPotionUI(); }
    public bool SpendRPotion(int amount = 1)
    {
        if (currentRPotion >= amount) { currentRPotion -= amount; UpdateRPotionUI(); return true; }
        return false;
    }
    private void UpdateRPotionUI() { if (potionRText != null) potionRText.text = "x " + currentRPotion.ToString("0"); }
    public void TryDrinkRPotion()
    {
        if (healthNow <= 0 || currentRPotion <= 0 || healthNow >= maximumHealth || !SpendRPotion()) return;
        healthNow += healPerPotion; if (healthNow > maximumHealth) healthNow = maximumHealth;
        if (drinkPotionSound != null) audioSource.PlayOneShot(drinkPotionSound);
    }
    #endregion

    #region Pociones Verdes
    public void AddGPotion(int amount = 1) { currentGPotion += amount; UpdateGPotionUI(); }
    public bool SpendGPotion(int amount = 1)
    {
        if (currentGPotion >= amount) { currentGPotion -= amount; UpdateGPotionUI(); return true; }
        return false;
    }
    private void UpdateGPotionUI() { if (potionGText != null) potionGText.text = "x " + currentGPotion.ToString("0"); }
    public void TryDrinkGPotion()
    {
        if (healthNow <= 0 || currentGPotion <= 0 || staminaNow >= maximumStamina || !SpendGPotion()) return;
        staminaNow += healPerPotion; if (staminaNow > maximumStamina) staminaNow = maximumStamina;
        if (drinkPotionSound != null) audioSource.PlayOneShot(drinkPotionSound);
    }
    #endregion

    #region Pociones Azules
    public void AddBPotion(int amount = 1) { currentBPotion += amount; UpdateBPotionUI(); }
    public bool SpendBPotion(int amount = 1)
    {
        if (currentBPotion >= amount) { currentBPotion -= amount; UpdateBPotionUI(); return true; }
        return false;
    }
    private void UpdateBPotionUI() { if (potionBText != null) potionBText.text = "x " + currentBPotion.ToString("0"); }
    public void TryDrinkBPotion()
    {
        if (healthNow <= 0 || currentBPotion <= 0 || magicNow >= maximumMagic || !SpendBPotion()) return;
        magicNow += healPerPotion; if (magicNow > maximumMagic) magicNow = maximumMagic;
        if (drinkPotionSound != null) audioSource.PlayOneShot(drinkPotionSound);
    }
    #endregion

    #region Llaves
    public void AddKey(int amount = 1) { currentKeys += amount; UpdateKeyUI(); }
    public bool SpendKey(int amount = 1)
    {
        if (currentKeys >= amount) { currentKeys -= amount; UpdateKeyUI(); return true; }
        return false;
    }
    private void UpdateKeyUI() { if (keyText != null) keyText.text = "x " + currentKeys.ToString("0"); }
    #endregion
}
