using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the cell-collect sound with progressively rising pitch
/// as the player extends the selection chain, plus segment-complete,
/// undo, and level-complete sounds.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private AudioSource audioSource;
    private AudioClip collectClip;
    private AudioClip collectLongClip;
    private AudioClip undoClip;
    private AudioClip levelCompleteClip;

    private const float BasePitch = 1.0f;
    private const float PitchStep = 0.07f;
    private const float MaxPitch = 2.2f;

    private float currentPitch = BasePitch;
    private int   chainLength  = 0;

    private static AudioManager _instance;
    public static AudioManager Instance => _instance;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        collectClip      = Resources.Load<AudioClip>("cell_collect")   ?? GenerateCollectClip(false);
        collectLongClip  = Resources.Load<AudioClip>("collect_long")   ?? GenerateCollectClip(true);
        undoClip         = Resources.Load<AudioClip>("undo")           ?? GenerateUndoClip();
        levelCompleteClip = Resources.Load<AudioClip>("level_complete") ?? GenerateLevelCompleteClip();
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>Call when a new chain is started (pitch resets).</summary>
    public void OnChainStarted()
    {
        currentPitch = BasePitch;
        chainLength  = 0;
    }

    /// <summary>Call when a cell is added to the chain (pitch rises).</summary>
    public void OnCellCollected()
    {
        audioSource.pitch = Mathf.Min(currentPitch, MaxPitch);
        audioSource.PlayOneShot(collectClip);
        currentPitch = Mathf.Min(currentPitch + PitchStep, MaxPitch);
        chainLength++;
    }

    /// <summary>
    /// Plays the collect sound twice in quick succession — "pip-pip".
    /// </summary>
    public void OnSegmentComplete()
    {
        StartCoroutine(PlayDoublePip(currentPitch));
        currentPitch = BasePitch;
        chainLength  = 0;
    }

    private IEnumerator PlayDoublePip(float pitch)
    {
        float p = Mathf.Min(pitch, MaxPitch);
        audioSource.pitch = p;
        audioSource.PlayOneShot(collectClip, 0.9f);
        yield return new WaitForSeconds(0.07f);
        audioSource.pitch = Mathf.Min(p + 0.15f, MaxPitch);
        audioSource.PlayOneShot(collectClip, 1.0f);
    }

    /// <summary>Call when the player undoes a step.</summary>
    public void OnUndo()
    {
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(undoClip, 0.7f);
        if (currentPitch > BasePitch)
            currentPitch = Mathf.Max(BasePitch, currentPitch - PitchStep);
    }

    /// <summary>Call when the level is completed.</summary>
    public void OnLevelComplete()
    {
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(levelCompleteClip, 1.0f);
        currentPitch = BasePitch;
    }

    /// <summary>Call when chain is cancelled or level reloads.</summary>
    public void OnChainReset()
    {
        currentPitch = BasePitch;
        chainLength  = 0;
    }

    // ── Procedural fallback clip generation ────────────────────────────────

    private AudioClip GenerateCollectClip(bool longVersion)
    {
        int sr = 44100;
        float dur = longVersion ? 0.45f : 0.13f;
        int n = (int)(sr * dur);
        float[] d = new float[n];
        float f0 = 480f, f1 = 720f, k = (f1 - f0) / 0.13f;
        float decay = longVersion ? 7f : 28f;
        int atk = (int)(0.005f * sr);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float ts = Mathf.Min(t, 0.13f);
            float ph = 2f * Mathf.PI * (f0 * ts + .5f * k * ts * ts + 720f * Mathf.Max(0f, t - 0.13f));
            float env = Mathf.Exp(-t * decay) * (i < atk ? (float)i / atk : 1f);
            d[i] = (Mathf.Sin(ph) + .25f * Mathf.Sin(2f * ph)) * env * .9f;
        }
        return MakeClip(longVersion ? "collect_long" : "cell_collect", d, sr);
    }

    private AudioClip GenerateUndoClip()
    {
        int sr = 44100; float dur = 0.13f; int n = (int)(sr * dur);
        float[] d = new float[n];
        float f0 = 460f, f1 = 260f, k = (f1 - f0) / dur;
        int atk = (int)(0.004f * sr);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float ph = 2f * Mathf.PI * (f0 * t + .5f * k * t * t);
            float env = Mathf.Exp(-t * 16f) * (i < atk ? (float)i / atk : 1f);
            d[i] = Mathf.Sin(ph) * env * .85f;
        }
        return MakeClip("undo", d, sr);
    }

    private AudioClip GenerateLevelCompleteClip()
    {
        int sr = 44100; int total = (int)(0.65f * sr);
        float[] buf = new float[total];
        AddTone(buf, sr, 523.25f,  0,               0.18f);
        AddTone(buf, sr, 659.25f,  (int)(.12f * sr), 0.18f);
        AddTone(buf, sr, 783.99f,  (int)(.24f * sr), 0.18f);
        AddTone(buf, sr, 1046.50f, (int)(.36f * sr), 0.28f, 1.0f);
        AddTone(buf, sr,  659.25f, (int)(.36f * sr), 0.28f, 0.35f);
        AddTone(buf, sr,  783.99f, (int)(.36f * sr), 0.28f, 0.25f);
        return MakeClip("level_complete", NormBuf(buf, 0.92f), sr);
    }

    private static void AddTone(float[] buf, int sr, float freq, int startSample, float dur, float vol = 0.85f)
    {
        int n = (int)(dur * sr);
        int atk = (int)(0.005f * sr);
        for (int i = 0; i < n; i++)
        {
            int idx = startSample + i;
            if (idx >= buf.Length) break;
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * 18f) * (i < atk ? (float)i / atk : 1f);
            buf[idx] += (Mathf.Sin(2f * Mathf.PI * freq * t)
                        + 0.2f * Mathf.Sin(4f * Mathf.PI * freq * t)) * env * vol;
        }
    }

    private static float[] NormBuf(float[] buf, float target)
    {
        float peak = 0f;
        foreach (float v in buf) if (Mathf.Abs(v) > peak) peak = Mathf.Abs(v);
        if (peak < 0.001f) return buf;
        float scale = target / peak;
        for (int i = 0; i < buf.Length; i++) buf[i] *= scale;
        return buf;
    }

    private static AudioClip MakeClip(string name, float[] data, int sr)
    {
        var clip = AudioClip.Create(name, data.Length, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
