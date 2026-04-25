using UnityEngine;

/// <summary>
/// Plays the cell-collect sound with progressively rising pitch
/// as the player extends the selection chain.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private AudioSource audioSource;
    private AudioClip collectClip;

    // Pitch resets to this when a new chain starts
    private const float BasePitch = 1.0f;
    // Each new cell added to the chain increases pitch by this amount
    private const float PitchStep = 0.07f;
    // Maximum pitch (prevents it from getting unbearably high)
    private const float MaxPitch = 2.2f;

    private float currentPitch = BasePitch;

    private static AudioManager _instance;
    public static AudioManager Instance => _instance;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D sound

        LoadClip();
    }

    private void LoadClip()
    {
        // Load from Resources folder if available, otherwise use procedural
        collectClip = Resources.Load<AudioClip>("cell_collect");
        if (collectClip == null)
            collectClip = GenerateCollectClip();
        audioSource.clip = collectClip;
    }

    /// <summary>Call when a new chain is started (pitch resets).</summary>
    public void OnChainStarted()
    {
        currentPitch = BasePitch;
    }

    /// <summary>Call when a cell is added to the chain (pitch rises).</summary>
    public void OnCellCollected()
    {
        audioSource.pitch = Mathf.Min(currentPitch, MaxPitch);
        audioSource.PlayOneShot(collectClip);
        currentPitch = Mathf.Min(currentPitch + PitchStep, MaxPitch);
    }

    /// <summary>Call when chain is cancelled or completed.</summary>
    public void OnChainReset()
    {
        currentPitch = BasePitch;
    }

    // Procedural fallback: generates a short upward chirp at runtime
    private AudioClip GenerateCollectClip()
    {
        int sampleRate = 44100;
        float duration = 0.13f;
        int n = (int)(sampleRate * duration);
        float[] data = new float[n];

        float f0 = 480f, f1 = 720f;
        float k = (f1 - f0) / duration;
        int attackN = (int)(0.005f * sampleRate);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sampleRate;
            float phase = 2f * Mathf.PI * (f0 * t + 0.5f * k * t * t);
            float sine = Mathf.Sin(phase) + 0.25f * Mathf.Sin(2f * phase);
            float env = Mathf.Exp(-t * 28f);
            if (i < attackN) env *= (float)i / attackN;
            data[i] = sine * env * 0.9f;
        }

        var clip = AudioClip.Create("cell_collect", n, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
