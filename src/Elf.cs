using System.Collections;
using UnityEngine;

public class Elf : PlayerCharacter
{
    [Header("Asignación en escena")]
    [SerializeField] private Transform elfCamera;

    [Header("Combat Settings")]
    public WeaponType currentWeapon = WeaponType.Melee;

    public GameObject meleeWeapon;
    public GameObject rangedWeapon;

    public GameObject WeaponOneHandedShetered;
    public GameObject WeaponTwoHandedShetered;

    public Transform weaponMeleeSocket;
    private BoxCollider swordCollider;

    public float meleeAttackRange = 200f;
    public int meleeDamage = 20;

    [Header("Melee Combo (Light Attacks)")]
    [SerializeField]
    private string[] lightComboStateNames = new string[]
{
    "MeleeAttack 1",
    "MeleeAttack 2",
    "MeleeAttack 3"
};

    [SerializeField] private float comboResetDelay = 0.85f;   // si tardas más, vuelve al golpe 1
    [SerializeField] private float comboCrossFade = 0.06f;    // transición suave (ajústalo)
    [SerializeField] private float[] hitColliderTimes = new float[] { 0.45f, 0.45f, 0.55f };

    // Potenciación del 3er golpe:
    [SerializeField] private float thirdHitDamageMultiplier = 1.75f;
    [SerializeField] private float thirdHitStaminaMultiplier = 1.2f;
    [SerializeField] private float thirdHitCooldownMultiplier = 1.35f;

    private int comboIndex = 0;
    private float lastLightAttackTime = -999f;


    [SerializeField] private float aimTurnSpeed = 720f;

    [Header("Aiming (Raycast)")]
    [SerializeField] private LayerMask aimMask = ~0;  // por defecto, todo
    [SerializeField] private float maxAimDistance = 100f;
    // Cache puntual (por si quisieras usar Animation Events)
    private Vector3 cachedAimDir;



    // 🔹 Coste de stamina para ataque melee
    [Tooltip("Stamina que cuesta hacer un ataque cuerpo a cuerpo.")]
    public int meleeStaminaCost = 30;

    // --- Tap vs Hold (config)
    [Header("Primary Input (Tap vs Hold)")]
    [SerializeField] private float tapThreshold = 0.18f;  // <= esto es TAP (segundos)
    [SerializeField] private float maxChargeTime = 1.25f; // carga completa (segundos)

    // --- Estado interno de la pulsación
    private float primaryDownTime = -1f;
    private bool isCharging = false;
    private bool firedThisPress = false;
    private bool holdAimWithRMB => Input.GetMouseButton(1); //comprobación en aim para seguir manteniendo el modo apuntado si el RMB sigue pulsado
    private bool keepAimUntilRmbRelease = false;


    [Header("Ranged Attack")]
    [SerializeField] private GameObject arrow;
    [SerializeField] public Transform arrowSpawnStanding;
    [SerializeField] public Transform arrowSpawnCrouching;
    [SerializeField] public Transform arrowSpawnJumping;

    public float attackCooldown = 0.5f;
    private float cooldownTimer = 1.7f;

    [Header("Ranged  Aim Power Attack")]
    // Modo apuntado activo (solo arco) — control interno
    private bool aimModeActive = false;

    private CameraController cam;                 // se auto-detecta en Awake
    [SerializeField] private GameObject aimReticle; // opcional; arrástralo o déjalo null
    [SerializeField] private int chargedShotStaminaCost = 30; // coste alto del disparo cargado
    private Transform cachedChargedSpawn;//registro del punto de spawn del proyectil desde el modo cargado (necesario solo sin aiming fijo)
    private Vector3 cachedChargedAimDir;//registro de la dirección exacta del spawn del proyectil desde el modo cargado ( necesario solo sin aiming fijo)


    // --- Bow charge pose freeze ---
    [SerializeField] private string bowDrawStateName = "Draw Arrow"; // nombre EXACTO del estado de tensar
    [SerializeField] private string bowShootTrigger = "Shoot";       // trigger para soltar
    [SerializeField] private float bowHoldFreezeTime = 0.95f;        // 0..1: dónde congelar el clip de tensar
    private bool bowPoseFrozen = false;


    [Header("Weapon Icons & Audio")]
    public Transform iconSword;
    public Transform iconBow;
    public AudioClip drawnSound;
    public AudioClip shootSound;
    public AudioClip drawnSword;
    public AudioClip WeaponHit1;

    [Header("Elf Take Damage; animation and sound")]
    public Animator playerAnimator;       // igual que en GameManager
    public AudioClip elfScream;


    protected override void Awake()
    {
        base.Awake();

        //referencia a la cámara con CameraController
        cam = FindFirstObjectByType<CameraController>();

        ConfigureStats();
        SetupMeleeWeapon();
        audioSource = GetComponent<AudioSource>();
        cameraTransform = elfCamera; // 🔁 Aquí haces la asignación hacia la clase base


    }
    private IEnumerator DelayedCameraAlign()
    {
        yield return new WaitForEndOfFrame(); // Espera un frame para que todo se inicialice
        CameraController camController = Camera.main?.GetComponent<CameraController>();

        if (camController != null)
        {
            camController.AlignBehindTarget(transform);
            Debug.Log("📷 Cámara alineada tras el primer frame.");
        }
        else
        {
            Debug.LogWarning("⚠️ No se encontró CameraController en la cámara principal.");
        }
    }
    private void Start()
    {
        CameraController camController = Camera.main?.GetComponent<CameraController>();
        if (camController != null)
        {
            camController.AlignBehindTarget(transform);
        }
        else
        {
            Debug.LogWarning("⚠️ No se encontró CameraController en la cámara principal.");
        }

        StartCoroutine(DelayedCameraAlign()); // ← Aquí va la llamada
    }

    private void ConfigureStats()
    {
        MovementSpeed = 8.5f;
        JumpSpeed = 12f;

        maximumHealth = 80;
        maximumStamina = 120;
        maximumMagic = 150;

        healthNow = maximumHealth;
        staminaNow = maximumStamina;
        magicNow = maximumMagic;
    }

    private void SetupMeleeWeapon()
    {
        if (weaponMeleeSocket != null)
        {
            GameObject sword = weaponMeleeSocket.GetComponentInChildren<BoxCollider>()?.gameObject;
            if (sword != null)
            {
                swordCollider = sword.GetComponent<BoxCollider>();
                if (swordCollider != null)
                    swordCollider.enabled = false;
            }
        }
    }

    protected override void Update()
    {
        base.Update();

        if (cooldownTimer > 0)
            cooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Q))
            SwitchWeapon();

        // Nuevo manejo único del botón izquierdo
        HandlePrimaryAttackInput();
        if (aimModeActive)
        {
            // Copia el yaw de la cámara (sin pitch)
            float targetYaw = cam.transform.eulerAngles.y;
            Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, aimTurnSpeed * Time.deltaTime);
        }

        // Salimos del aim SOLO si lo estamos manteniendo por RMB y lo soltamos
        if (aimModeActive && keepAimUntilRmbRelease && !Input.GetMouseButton(1))
        {
            keepAimUntilRmbRelease = false;

            cam?.ExitAim();
            cam?.SetSuppressFollowBehind(false);
            if (aimReticle) aimReticle.SetActive(false);
            aimModeActive = false;
        }


    }


    void SwitchWeapon()
    {
        // Si estamos cargando, limpiamos el estado para no arrastrarlo al arma nueva
        if (isCharging) ResetChargeState();
        
        if (currentWeapon == WeaponType.Melee)
        {
            currentWeapon = WeaponType.Ranged;
            meleeWeapon.SetActive(false);
            rangedWeapon.SetActive(true);
            iconBow.gameObject.SetActive(false);
            iconSword.gameObject.SetActive(true);
            WeaponOneHandedShetered.SetActive(true);
            WeaponTwoHandedShetered.SetActive(false);
            audioSource.PlayOneShot(drawnSound);
        }
        else
        {
            currentWeapon = WeaponType.Melee;
            meleeWeapon.SetActive(true);
            rangedWeapon.SetActive(false);
            iconSword.gameObject.SetActive(false);
            iconBow.gameObject.SetActive(true);
            WeaponOneHandedShetered.SetActive(false);
            WeaponTwoHandedShetered.SetActive(true);
            audioSource.PlayOneShot(drawnSword);
        }
    }

    void MeleeAttack_LightCombo()
    {
        // Si tardamos demasiado, reiniciamos combo
        if (Time.time - lastLightAttackTime > comboResetDelay)
            comboIndex = 0;

        lastLightAttackTime = Time.time;

        // Seguridad
        if (lightComboStateNames == null || lightComboStateNames.Length == 0)
            return;

        // Clamp por si faltan animaciones
        int idx = Mathf.Clamp(comboIndex, 0, lightComboStateNames.Length - 1);
        string state = lightComboStateNames[idx];

        // IMPORTANTE: NO hacer Rebind/Play("Idle") aquí, o rompes transiciones/combos
        animator.CrossFadeInFixedTime(state, comboCrossFade, 0);


        audioSource.PlayOneShot(WeaponHit1);

        // Daño escalado en el 3er golpe (índice 2)
        bool isThird = (idx == 2);
        float dmgMult = isThird ? thirdHitDamageMultiplier : 1f;

        // Ventana del collider por golpe (si hay array)
        float colliderTime = 0.5f;
        if (hitColliderTimes != null && hitColliderTimes.Length > idx)
            colliderTime = hitColliderTimes[idx];

        StartCoroutine(ActivateSwordColliderTemporarily_Combo(colliderTime, dmgMult));

        // Avanzamos el combo (0->1->2->0...)
        comboIndex++;
        if (comboIndex >= lightComboStateNames.Length)
            comboIndex = 0;
    }

    IEnumerator ActivateSwordColliderTemporarily_Combo(float activeTime, float damageMultiplier)
    {
        if (swordCollider != null)
        {
            // Si tu sistema de daño lee "meleeDamage" en el momento del impacto,
            // puedes ajustar temporalmente aquí.
            int originalDamage = meleeDamage;
            meleeDamage = Mathf.RoundToInt(meleeDamage * damageMultiplier);

            swordCollider.enabled = true;
            yield return new WaitForSeconds(activeTime);
            swordCollider.enabled = false;

            meleeDamage = originalDamage;
        }
    }

    IEnumerator ActivateSwordColliderTemporarily()
    {
        if (swordCollider != null)
        {
            swordCollider.enabled = true;
            yield return new WaitForSeconds(0.5f);
            swordCollider.enabled = false;
        }
    }

    void ShootArrow()
    {
        animator.Play("Idle");
        animator.Rebind();
        animator.Update(0);
        animator.CrossFade("Draw Arrow", 0f);
        animator.SetTrigger("Shoot");

        audioSource.PlayOneShot(drawnSound);

        // ⬅️ Capturamos YA la dirección de apuntado
        Transform spawn = GetArrowSpawnPoint();
        Vector3 aimDirNow = GetAimDirection(spawn);
        cachedAimDir = aimDirNow;

        // y la pasamos a la corrutina
        StartCoroutine(ShootArrowAfterDelay(aimDirNow));
    }


    IEnumerator ShootArrowAfterDelay(Vector3 aimDir)
    {
        yield return new WaitForSeconds(0.9f);
        animator.Update(0);
        audioSource.PlayOneShot(shootSound);

        Transform currentSpawnPoint = GetArrowSpawnPoint();

        // Dirección normal = hacia donde mira el personaje
        Vector3 dir = GetNormalShotDirection();

        // OJO: mantengo el -dir porque tu Arrow vuela en -forward
        Quaternion rot = Quaternion.LookRotation(-dir, Vector3.up);
        Instantiate(arrow, currentSpawnPoint.position, rot);

    }


    Transform GetArrowSpawnPoint()
    {
        if (animator.GetBool("IsCrouching"))
            return arrowSpawnCrouching;
        else if (!animator.GetBool("IsGrounded"))
            return arrowSpawnJumping;
        else
            return arrowSpawnStanding;
    }

    public override void TakeDamage(int amount)
    {
        // 1) Resta vida y muerte (flujo original de Character)
        base.TakeDamage(amount);

        // 2) Asegurar referencia al Animator (como hacía el GM al iniciar escena)
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();

        // 3) Disparar la animación de daño con el MISMO trigger de antes
        if (playerAnimator != null)
            playerAnimator.SetTrigger("TakeDamage");

        // 4) Reproducir el mismo grito que usaba el GameManager
        if (audioSource != null && elfScream != null)
            audioSource.PlayOneShot(elfScream);
    }

    public enum WeaponType { Melee, Ranged, Magic }

    // ========================= TAP vs HOLD INPUT =========================
    void HandlePrimaryAttackInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            primaryDownTime = Time.time;
            isCharging = true;
            firedThisPress = false;

            if (currentWeapon == WeaponType.Ranged)
            {
                // 🔹 Limpieza total del estado anterior
                animator.speed = 1f;
                bowPoseFrozen = false;

                animator.ResetTrigger(bowShootTrigger);
                animator.SetBool("ShootBow", false);

                animator.Play("Idle", 0, 0f);
                animator.Update(0f);

                // 🔹 INICIO DE NUEVA CARGA (SIEMPRE)
                animator.CrossFade(bowDrawStateName, 0f);
                animator.SetBool("ShootBow", true);

                // 🔊 SONIDO DE TENSAR → AQUÍ SIEMPRE
                if (drawnSound && audioSource)
                    audioSource.PlayOneShot(drawnSound);
            }

            OnChargeStart();
        }



        // Mantener (carga)
        if (isCharging && Input.GetMouseButton(0))
        {
            float held = Time.time - primaryDownTime;
            float charge01 = Mathf.Clamp01(held / maxChargeTime);

            if (currentWeapon == WeaponType.Ranged && !aimModeActive && held > tapThreshold)
            {
                cam?.EnterAim();
                cam?.SetSuppressFollowBehind(true);
                if (aimReticle) aimReticle.SetActive(true);
                aimModeActive = true;

                //activamos cameralock
                aimMoveLock = true;

                // ⬇️ AÑADE/DEJA ASÍ (tensar y preparar freeze)
                bowPoseFrozen = false;
                animator.speed = 1f; // asegurarnos de estar "vivos"
                animator.Play("Idle");
                animator.Rebind();
                animator.Update(0);
                animator.CrossFade(bowDrawStateName, 0f);
                animator.SetBool("ShootBow", true);
                if (drawnSound) audioSource.PlayOneShot(drawnSound);
            }

            OnChargeProgress(charge01); // UI opcional

            // (Opcional) auto-disparo al llegar a carga completa:
            // if (charge01 >= 1f && !firedThisPress) { DoPrimaryCharged(1f); firedThisPress = true; ResetChargeState(); }

            // Congelar la pose de tensado cuando el clip llega al final
            if (currentWeapon == WeaponType.Ranged && aimModeActive && !bowPoseFrozen)
            {
                var st = animator.GetCurrentAnimatorStateInfo(0);
                if (st.IsName(bowDrawStateName) && st.normalizedTime >= bowHoldFreezeTime)
                {
                    animator.speed = 0f;    // congelamos la anim (mantiene la cuerda tensada)
                    bowPoseFrozen = true;
                }
            }
        }

        // Soltar (decidir tap vs hold)
        if (Input.GetMouseButtonUp(0))
        {
            if (firedThisPress) { ResetChargeState(); return; }

            float held = Time.time - primaryDownTime;
            bool isTap = held <= tapThreshold;

            if (isTap)
                DoPrimaryTap();
            else
            {
                float charge01 = Mathf.Clamp01(held / maxChargeTime);
                DoPrimaryCharged(charge01);
            }

            firedThisPress = true;
            ResetChargeState();
        }
    }

    void ResetChargeState()
    {
        isCharging = false;
        primaryDownTime = -1f;
        aimMoveLock = false; // ⬅️ añade

        if (!keepAimUntilRmbRelease)
            cam?.SetSuppressFollowBehind(false);




        // ← limpieza de cámara/retícula si el modo apuntado estaba activo
        if (aimModeActive && !keepAimUntilRmbRelease)
        {
            cam?.ExitAim();
            cam?.SetSuppressFollowBehind(false);
            if (aimReticle) aimReticle.SetActive(false);
            aimModeActive = false;
        }


        animator.speed = 1f;
        bowPoseFrozen = false;


        OnChargeEnd(); // apaga flags/animaciones de carga si los usas
    }


    // --- Feedback opcional (UI/Anim/SFX de “carga”)
    void OnChargeStart()
    {
        if (currentWeapon == WeaponType.Ranged)
        {
           
        }
        else
        {
            // Espada: si quieres, puedes marcar "ChargingHeavy"
            // animator.SetBool("ChargingHeavy", true);
        }
    }

    void OnChargeProgress(float t)
    {
        // UI barra de carga, SFX pitch, etc. (0..1 en t)
    }
    void OnChargeEnd()
    {
        if (currentWeapon == WeaponType.Ranged)
        {
            
        }

        // Apaga flags genéricos por si quedaron activos
        if (animator != null)
        {
            animator.SetBool("Melee", false);
            animator.SetBool("ShootBow", false);
            // animator.SetBool("ChargingHeavy", false);
        }
    }


    // ========================= ACCIONES =========================
    void DoPrimaryTap()
    {
        if (cooldownTimer > 0) return;

        if (currentWeapon == WeaponType.Melee)
        {
            // Ataque ligero (tu flujo actual)
            if (staminaNow >= meleeStaminaCost)
            {
                SpendStamina(meleeStaminaCost);

                bool isThird = (comboIndex == 2); // OJO: comboIndex aún no se incrementó dentro del ataque

                // Coste stamina: el 3er golpe cuesta un poco más (opcional)
                int cost = meleeStaminaCost;
                if (isThird) cost = Mathf.CeilToInt(meleeStaminaCost * thirdHitStaminaMultiplier);

                if (staminaNow >= cost)
                {
                    SpendStamina(cost);

                    MeleeAttack_LightCombo();

                    // Cooldown: 3er golpe un poco más lento (opcional)
                    cooldownTimer = attackCooldown * (isThird ? thirdHitCooldownMultiplier : 1f);
                }
                else
                {
                    Debug.Log("No hay stamina suficiente para ataque ligero.");
                }

            }
            else
            {
                Debug.Log("No hay stamina suficiente para ataque ligero.");
            }
        }
        else if (currentWeapon == WeaponType.Ranged)
        {
            // Tiro rápido (tu flujo actual)
            animator.SetBool("ShootBow", true);
            ShootArrow(); // usa tu implementación actual
            cooldownTimer = attackCooldown;

            // ⬇️ Cierra apuntado si quedó activo por error
            if (aimModeActive)
            {
                cam?.ExitAim();
                cam?.SetSuppressFollowBehind(false);
                if (aimReticle) aimReticle.SetActive(false);
                aimModeActive = false;
            }
            aimMoveLock = false; // ⬅️ añade
        }
    }

    public void MeleeHitStart()
    {
        if (swordCollider != null)
            swordCollider.enabled = true;
    }

    public void MeleeHitEnd()
    {
        if (swordCollider != null)
            swordCollider.enabled = false;
    }


    void DoPrimaryCharged(float charge01)
    {
        if (cooldownTimer > 0) return;

        if (currentWeapon == WeaponType.Melee)
        {
            // Ataque pesado: coste un poco mayor según carga
            int heavyCost = Mathf.CeilToInt(meleeStaminaCost * Mathf.Lerp(1.2f, 2.0f, charge01));
            if (staminaNow >= heavyCost)
            {
                SpendStamina(heavyCost);
                MeleeHeavyAttack(charge01);   // ver abajo
                cooldownTimer = attackCooldown * Mathf.Lerp(1.2f, 1.6f, charge01); // opcional
            }
            else
            {
                Debug.Log("No hay stamina suficiente para ataque pesado.");
            }
        }
        else if (currentWeapon == WeaponType.Ranged)
        {
            // Coste alto al soltar (si no hay, cancelar sin disparar)
            if (staminaNow < chargedShotStaminaCost)
            {
                Debug.Log("No hay stamina suficiente para el disparo cargado.");
                OnChargeEnd(); // salir de apuntado/retícula si estaba activo
                return;
            }

            SpendStamina(chargedShotStaminaCost);

            // ✅ SNAPSHOT del apuntado en el instante EXACTO de soltar LMB
            cachedChargedSpawn = GetArrowSpawnPoint();
            cachedChargedAimDir = GetAimDirection(cachedChargedSpawn);

            // Disparo cargado (usa el aim snapshot)
            ShootArrowCharged(charge01);

            // Si RMB NO está pulsado, podemos salir del aim sin afectar a la dirección del disparo
            if (!holdAimWithRMB)
            {
                cam?.ExitAim();
                cam?.SetSuppressFollowBehind(false);
                if (aimReticle) aimReticle.SetActive(false);
                aimModeActive = false;
            }
            else
            {
                keepAimUntilRmbRelease = true;
            }

            // ⬇️ SOLO salimos del aim si NO estamos manteniendo RMB
            if (!holdAimWithRMB)
            {
                cam?.ExitAim();
                cam?.SetSuppressFollowBehind(false);
                if (aimReticle) aimReticle.SetActive(false);
                aimModeActive = false;
            }



            // desactivamos lock de camara
            aimMoveLock = false;

            // (Opcional) un poco más de cooldown para el cargado
            cooldownTimer = attackCooldown * Mathf.Lerp(1.1f, 1.5f, charge01);
        }

    }

    // ========================= VARIANTES (no invasivas) =========================
    void MeleeHeavyAttack(float charge01)
    {
        // Si tienes anim específica heavy, úsala:
        // animator.CrossFade("MeleeAttackHeavy", 0f);
        // animator.SetFloat("Charge01", charge01);

        // Fallback: usa tu anim actual para no romper nada
        animator.Play("Idle");
        animator.Rebind();
        animator.Update(0);
        animator.CrossFade("MeleeAttack 1", 0f);      // reutilizamos la existente
        animator.SetTrigger("MeleeWeapon");

        // Puedes escalar daño/ventana de hit aquí si tu collider lo permite
        audioSource.PlayOneShot(WeaponHit1);
        StartCoroutine(ActivateSwordColliderTemporarily()); // mismo collider
    }

    void ShootArrowCharged(float charge01)
    {
        // venimos tensando; descongela para permitir la anim de "Shoot"
        animator.speed = 1f;
        bowPoseFrozen = false;

        animator.SetFloat("Charge01", charge01);

        // 🔥 DISPARO: SOLO UN TRIGGER
        animator.ResetTrigger(bowShootTrigger);
        animator.SetTrigger(bowShootTrigger);

        // 🔥 Flecha + rearme se hacen DESPUÉS, en la corrutina
        StartCoroutine(FireChargedArrowNextFrame());


    }

    IEnumerator FireChargedArrowNextFrame()
    {
        yield return null; // ⏱️ esperar 1 frame → Animator ya evaluó el trigger

        if (shootSound)
            audioSource.PlayOneShot(shootSound);

        Transform spawn = cachedChargedSpawn != null ? cachedChargedSpawn : GetArrowSpawnPoint();
        Vector3 aimDir = cachedChargedAimDir != Vector3.zero ? cachedChargedAimDir : GetAimDirection(spawn);

        Quaternion rot = Quaternion.LookRotation(-aimDir, Vector3.up);
        Instantiate(arrow, spawn.position, rot);

        // Rearmar animación si seguimos en aiming
        if (aimModeActive)
        {
            // Rearme SILENCIOSO: dejamos el animator listo, pero SIN iniciar anim visible
            animator.speed = 1f;
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);

            // Importante: NO entramos en Draw Arrow aquí
            animator.SetBool("ShootBow", false);
            bowPoseFrozen = false;
        }

        cachedChargedSpawn = null;
        cachedChargedAimDir = Vector3.zero;

    }


    // ===== Aiming helpers (pegar al final de la clase Elf, antes de la última }) =====
    /// <summary>
    /// Calcula la dirección de puntería desde la cámara.
    /// Si el raycast golpea algo, apunta a ese punto; si no, usa el forward de cámara.
    /// </summary>
    /// <param name="spawn">Transform desde donde nace la flecha (tu spawn del arco).</param>
    Vector3 GetAimDirection(Transform spawn)
    {
        if (cameraTransform == null)
            return transform.forward; // fallback seguro

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = (hit.point - spawn.position);
            return dir.normalized;
        }
        return (cameraTransform.forward).normalized;
    }

    Vector3 GetNormalShotDirection()
    {
        // Dirección basada SOLO en hacia dónde mira el personaje
        Vector3 dir = transform.forward;
        dir.y = 0f; // evitamos pitch raro al correr/saltar
        return dir.normalized;
    }

    // ===== fin helpers =====

}
