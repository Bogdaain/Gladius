using System;
using UnityEngine;

/// <summary> Stamina pool for the player. Works by telling PlayerController the player state, like running and such (is Running) or events (onDash, onWallJump, onSlideStart), so the player controller works without knowing this script exists. </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerStamina : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private float maxStamina = 100f;

    [Header("Costs")]
    [SerializeField] private float runDrainPerSecond = 15f;
    [SerializeField] private float dashCost = 25f;
    [SerializeField] private float wallJumpCost = 10f;
    [Tooltip("0 = sliding is free and never blocked by stamina.")]
    [SerializeField] private float slideCost = 0f;

    [Header("Regeneration")]
    [SerializeField] private float regenPerSecond = 25f;
    [Tooltip("Seconds after spending before stamina starts coming back.")]
    [SerializeField] private float regenDelay = 0.8f;

    [Header("Exhaustion")]
    [Tooltip("After hitting 0, running stays locked until stamina recovers to this fraction of max.")]
    [SerializeField, Range(0f, 1f)] private float exhaustedRecoverFraction = 0.25f;

    /// <summary>Raised when the value changes: (current, max).</summary>
    public event Action<float, float> Changed;
    /// <summary>Raised when entering (true) or leaving (false) exhaustion.</summary>
    public event Action<bool> ExhaustedChanged;

    public float Current { get; private set; }
    public float Max => maxStamina;
    public float Normalized => maxStamina > 0f ? Current / maxStamina : 0f;
    public bool IsExhausted { get; private set; }

    private PlayerController player;
    private float regenResumeTime;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        Current = maxStamina;
    }

    private void OnEnable()
    {
        player.onDash.AddListener(HandleDash);
        player.onWallJump.AddListener(HandleWallJump);
        player.onSlideStart.AddListener(HandleSlideStart);
    }

    private void OnDisable()
    {
        player.onDash.RemoveListener(HandleDash);
        player.onWallJump.RemoveListener(HandleWallJump);
        player.onSlideStart.RemoveListener(HandleSlideStart);
    }

    private void Start()
    {
        ApplyPermissions();
        Changed?.Invoke(Current, maxStamina);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (player.IsRunning)
        {
            Spend(runDrainPerSecond * dt);
        }
        else if (Time.time >= regenResumeTime && Current < maxStamina)
        {
            Add(regenPerSecond * dt);
        }
    }

    // Public API

    public bool CanAfford(float amount) => Current >= amount;

    /// <summary>Spends the amount if available. Returns false (and spends nothing) if not.</summary>
    public bool TrySpend(float amount)
    {
        if (Current < amount) return false;
        Spend(amount);
        return true;
    }

    public void Refill() => Add(maxStamina);

    // Internals

    private void HandleDash() => Spend(dashCost);
    private void HandleWallJump() => Spend(wallJumpCost);
    private void HandleSlideStart() => Spend(slideCost);

    private void Spend(float amount)
    {
        if (amount <= 0f) return;

        // Spending stamina stops stamina regen, until it is empty
        regenResumeTime = Time.time + regenDelay;

        float before = Current;
        Current = Mathf.Max(0f, Current - amount);
        if (Current <= 0f && !IsExhausted) SetExhausted(true);

        if (!Mathf.Approximately(before, Current)) Changed?.Invoke(Current, maxStamina);
        ApplyPermissions();
    }

    private void Add(float amount)
    {
        float before = Current;
        Current = Mathf.Min(maxStamina, Current + amount);

        if (IsExhausted && Current >= maxStamina * exhaustedRecoverFraction) SetExhausted(false);

        if (!Mathf.Approximately(before, Current)) Changed?.Invoke(Current, maxStamina);
        ApplyPermissions();
    }

    private void SetExhausted(bool value)
    {
        IsExhausted = value;
        ExhaustedChanged?.Invoke(value);
    }

    private void ApplyPermissions()
    {
        player.AllowRun = !IsExhausted && Current > 0f;
        player.AllowDash = Current >= dashCost;
        player.AllowWallJump = wallJumpCost <= 0f || Current >= wallJumpCost;
        player.AllowSlide = slideCost <= 0f || Current >= slideCost;
    }
}