using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerAudio : MonoBehaviour
{
    [Header("One-shots (a random clip is picked each time)")]
    [SerializeField] private AudioClip[] footsteps;
    [SerializeField] private AudioClip[] jumps;
    [SerializeField] private AudioClip[] wallJumps;
    [SerializeField] private AudioClip[] landings;
    [SerializeField] private AudioClip[] dashes;
    [SerializeField] private AudioClip slideStart;
    [SerializeField] private AudioClip slideEnd;

    [Header("Loops")]
    [SerializeField] private AudioClip slideLoop;
    [SerializeField] private AudioClip windLoop;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float slideLoopVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float windMaxVolume = 0.5f;
    [SerializeField] private float pitchVariation = 0.08f;

    [Header("Wind")]
    [SerializeField] private float windStartSpeed = 8f;
    [SerializeField] private float windMaxSpeed = 22f;
    [SerializeField] private Vector2 windPitchRange = new Vector2(0.8f, 1.3f);
    [SerializeField] private float loopSmoothing = 6f;

    private const int PoolSize = 6;

    private PlayerController player;
    private AudioSource[] pool;
    private AudioSource slideSource;
    private AudioSource windSource;
    private int poolIndex;
    private int lastFootstep = -1;
    private float slideTarget;

    private void Awake()
    {
        player = GetComponent<PlayerController>();

        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++) pool[i] = CreateSource(null);

        slideSource = CreateSource(slideLoop);
        windSource = CreateSource(windLoop);
    }

    private void Start()
    {
        if (windLoop != null) windSource.Play();
    }

    private void OnEnable()
    {
        player.onFootstep.AddListener(OnFootstep);
        player.onJump.AddListener(OnJump);
        player.onWallJump.AddListener(OnWallJump);
        player.onLand.AddListener(OnLand);
        player.onDash.AddListener(OnDash);
        player.onSlideStart.AddListener(OnSlideStart);
        player.onSlideEnd.AddListener(OnSlideEnd);
    }

    private void OnDisable()
    {
        player.onFootstep.RemoveListener(OnFootstep);
        player.onJump.RemoveListener(OnJump);
        player.onWallJump.RemoveListener(OnWallJump);
        player.onLand.RemoveListener(OnLand);
        player.onDash.RemoveListener(OnDash);
        player.onSlideStart.RemoveListener(OnSlideStart);
        player.onSlideEnd.RemoveListener(OnSlideEnd);
    }

    private void Update()
    {
        float speed = player.HorizontalSpeed;
        float k = 1f - Mathf.Exp(-loopSmoothing * Time.deltaTime);

        if (windLoop != null)
        {
            float t = Mathf.InverseLerp(windStartSpeed, windMaxSpeed, speed);
            windSource.volume = Mathf.Lerp(windSource.volume, t * windMaxVolume, k);
            windSource.pitch = Mathf.Lerp(windPitchRange.x, windPitchRange.y, t);
        }

        if (slideLoop != null)
        {
            float t = Mathf.InverseLerp(3f, 14f, speed);
            slideSource.volume = Mathf.Lerp(slideSource.volume, slideTarget * Mathf.Lerp(0.4f, 1f, t), k);
            slideSource.pitch = Mathf.Lerp(0.85f, 1.15f, t);

            if (slideTarget <= 0f && slideSource.isPlaying && slideSource.volume < 0.01f)
                slideSource.Stop();
        }
    }

    private void OnFootstep()
    {
        if (footsteps == null || footsteps.Length == 0) return;

        int i = Random.Range(0, footsteps.Length);
        if (footsteps.Length > 1 && i == lastFootstep) i = (i + 1) % footsteps.Length;
        lastFootstep = i;

        bool running = player.IsRunning;
        Play(footsteps[i], footstepVolume * (running ? 1.25f : 1f), running ? 1.1f : 1f);
    }

    private void OnJump() => PlayRandom(jumps, sfxVolume * 0.7f);

    private void OnWallJump()
    {
        bool hasWallClips = wallJumps != null && wallJumps.Length > 0;
        PlayRandom(hasWallClips ? wallJumps : jumps, sfxVolume * 0.8f);
    }

    private void OnLand()
    {
        float t = Mathf.InverseLerp(3f, 18f, player.LastLandSpeed);
        PlayRandom(landings, sfxVolume * Mathf.Lerp(0.3f, 1f, t));
    }

    private void OnDash() => PlayRandom(dashes, sfxVolume);

    private void OnSlideStart()
    {
        Play(slideStart, sfxVolume);
        slideTarget = slideLoopVolume;
        if (slideLoop != null && !slideSource.isPlaying) slideSource.Play();
    }

    private void OnSlideEnd()
    {
        Play(slideEnd, sfxVolume * 0.7f);
        slideTarget = 0f;
    }

    private void PlayRandom(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0) return;
        Play(clips[Random.Range(0, clips.Length)], volume);
    }

    // Round-robin over a few sources so each sound can have its own pitch
    private void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null) return;

        AudioSource source = pool[poolIndex];
        poolIndex = (poolIndex + 1) % PoolSize;

        source.pitch = pitch + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, volume);
    }

    private AudioSource CreateSource(AudioClip loopClip)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // first-person: our own sounds are 2D

        if (loopClip != null)
        {
            source.clip = loopClip;
            source.loop = true;
            source.volume = 0f;
        }

        return source;
    }
}