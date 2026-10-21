using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Volume))]
public class SpeedPostFX : MonoBehaviour
{
    [SerializeField] private PlayerController player;

    [Header("Speed Range")]
    [SerializeField] private float startSpeed = 8f;
    [SerializeField] private float maxSpeed = 22f;
    [SerializeField] private float smoothing = 8f;

    [Header("Vignette")]
    [SerializeField] private float vignetteIdle = 0.15f;
    [SerializeField] private float vignetteFast = 0.35f;

    [Header("Chromatic Aberration")]
    [SerializeField] private float chromaFast = 0.5f;

    [Header("Lens Distortion")]
    [SerializeField] private float lensFast = -0.2f;

    private Vignette vignette;
    private ChromaticAberration chroma;
    private LensDistortion lens;
    private float amount;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();

        VolumeProfile profile = GetComponent<Volume>().profile;
        profile.TryGet(out vignette);
        profile.TryGet(out chroma);
        profile.TryGet(out lens);
    }

    private void Update()
    {
        float target = Mathf.InverseLerp(startSpeed, maxSpeed, player.HorizontalSpeed);
        amount = Mathf.Lerp(amount, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

        if (vignette != null) vignette.intensity.Override(Mathf.Lerp(vignetteIdle, vignetteFast, amount));
        if (chroma != null) chroma.intensity.Override(chromaFast * amount);
        if (lens != null) lens.intensity.Override(lensFast * amount);
    }
}