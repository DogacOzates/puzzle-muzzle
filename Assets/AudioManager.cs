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
    private AudioClip undoClip;
    private AudioClip levelCompleteClip;

    private const float BasePitch = 1.0f;
    private const float PitchStep = 0.07f;
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
        audioSource.spatialBlend = 0f;

        collectClip      = Resources.Load<AudioClip>("cell_collect") ?? GenerateCollectClip();
        undoClip         = Resources.Load<AudioClip>("undo")         ?? GenerateUndoClip();
        levelCompleteClip = Resources.Load<AudioClip>("level_complete") ?? GenerateLevelCompleteClip();
    }

    // ── Public API ──────────────────────────────────────────────────────────

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

    /// <summary>
    /// Call when a full segment is completed.
    /// Plays the collect clip 3 times rapidly — pip · pip · PIP! —
    /// so the completion sound is the same family as the collect sounds.
    /// </summary>
    public void OnSegmentComplete()
    {
        StartCoroutine(PlayCompletionBurst(currentPitch));
        currentPitch = BasePitch;
    }

    private IEnumerator PlayCompletionBurst(float fromPitch)
    {
        float p = Mathf.Min(fromPitch, MaxPitch);
        audioSource.pitch = p;
        audioSource.PlayOneShot(collectClip, 0.90f);

        yield return new WaitForSeconds(0.055f);
        audioSource.pitch = Mathf.Min(p + 0.12f, MaxPitch);
        audioSource.PlayOneShot(collectClip, 0.95f);

        yield return new WaitForSeconds(0.055f);
        audioSource.pitch = Mathf.Min(p + 0.28f, MaxPitch);
        audioSource.PlayOneShot(collectClip, 1.00f);
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
    }

    // ── Procedural fallback clip generation ────────────────────────────────

    private AudioClip GenerateCollectClip()
    {
        int sr = 44100; float dur = 0.13f; int n = (int)(sr * dur);
        float[] d = new float[n];
        float f0 = 480f, f1 = 720f, k = (f1 - f0) / dur;
        int atk = (int)(0.005f * sr);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float ph = 2f * Mathf.PI * (f0 * t + .5f * k * t * t);
            float env = Mathf.Exp(-t * 28f) * (i < atk ? (float)i / atk : 1f);
            d[i] = (Mathf.Sin(ph) + .25f * Mathf.Sin(2f * ph)) * env * .9f;
        }
        return MakeClip("cell_collect", d, sr);
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


/// <summary>
/// Plays the cell-collect sound with progressively rising pitch
/// as the player extends the selection chain, plus segment-complete,
/// undo, and level-complete sounds.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private AudioSource audioSource;
    private AudioClip collectClip;
    private AudioClip segmentCompleteClip;
    private AudioClip undoClip;
    private AudioClip levelCompleteClip;

    private const float BasePitch = 1.0f;
    private const float PitchStep = 0.07f;
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
        audioSource.spatialBlend = 0f;

        collectClip         = Resources.Load<AudioClip>("cell_collect")      ?? GenerateCollectClip();
        segmentCompleteClip = Resources.Load<AudioClip>("segment_complete")  ?? GenerateSegmentCompleteClip();
        undoClip            = Resources.Load<AudioClip>("undo")              ?? GenerateUndoClip();
        levelCompleteClip   = Resources.Load<AudioClip>("level_complete")    ?? GenerateLevelCompleteClip();
    }

    // ── Public API ──────────────────────────────────────────────────────────

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

    /// <summary>Call when a full segment is successfully completed.</summary>
    public void OnSegmentComplete()
    {
        // Play at the pitch we left off at so the sound continues naturally
        // from the rising collect sequence, then the descending sweep resolves it.
        audioSource.pitch = Mathf.Min(currentPitch, MaxPitch);
        audioSource.PlayOneShot(segmentCompleteClip, 0.85f);
        currentPitch = BasePitch;
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
    }

    // ── Procedural fallback clip generation ────────────────────────────────

    private AudioClip GenerateCollectClip()
    {
        int sr = 44100; float dur = 0.13f; int n = (int)(sr * dur);
        float[] d = new float[n];
        float f0 = 480f, f1 = 720f, k = (f1 - f0) / dur;
        int atk = (int)(0.005f * sr);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float ph = 2f * Mathf.PI * (f0 * t + .5f * k * t * t);
            float env = Mathf.Exp(-t * 28f) * (i < atk ? (float)i / atk : 1f);
            d[i] = (Mathf.Sin(ph) + .25f * Mathf.Sin(2f * ph)) * env * .9f;
        }
        return MakeClip("cell_collect", d, sr);
    }

    private AudioClip GenerateSegmentCompleteClip()
    {
        // Fast downward sweep (landing) + overlapping bell ding (resolution).
        // Played at currentPitch so it continues naturally from the collect sequence.
        int sr = 44100;
        int total = (int)(0.30f * sr);
        float[] buf = new float[total];

        // Part 1: downward sweep 800→480 Hz, 70ms
        int n1 = (int)(0.07f * sr);
        float f0s = 800f, f1s = 480f, ks = (f1s - f0s) / (n1 / (float)sr);
        int atk1 = (int)(0.003f * sr);
        for (int i = 0; i < n1; i++)
        {
            float t = i / (float)sr;
            float ph = 2f * Mathf.PI * (f0s * t + .5f * ks * t * t);
            float s = Mathf.Sin(ph) + 0.15f * Mathf.Sin(2f * ph);
            float env = Mathf.Exp(-t * 8f) * (i < atk1 ? (float)i / atk1 : 1f);
            buf[i] += s * env * 0.7f;
        }

        // Part 2: bell tone at 520 Hz starting at 40ms
        int n2 = (int)(0.25f * sr), start2 = (int)(0.04f * sr);
        int atk2 = (int)(0.003f * sr);
        for (int i = 0; i < n2; i++)
        {
            int idx = start2 + i;
            if (idx >= total) break;
            float t = i / (float)sr;
            float ph = 2f * Mathf.PI * 520f * t;
            float s = Mathf.Sin(ph) + 0.4f * Mathf.Sin(2f * ph) + 0.1f * Mathf.Sin(3f * ph);
            float env = Mathf.Exp(-t * 9f) * (i < atk2 ? (float)i / atk2 : 1f);
            buf[idx] += s * env * 0.85f;
        }

        return MakeClip("segment_complete", NormBuf(buf, 0.90f), sr);
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

    private static void AddChirp(float[] buf, int sr, float f0, float f1, float startSec, float dur,
                                   float decay, float vol, bool extraHarmonics = false)
    {
        int n = (int)(dur * sr);
        int start = (int)(startSec * sr);
        float k = (f1 - f0) / dur;
        int atk = (int)(0.005f * sr);
        for (int i = 0; i < n; i++)
        {
            int idx = start + i;
            if (idx >= buf.Length) break;
            float t = i / (float)sr;
            float ph = 2f * Mathf.PI * (f0 * t + .5f * k * t * t);
            float s = Mathf.Sin(ph) + 0.2f * Mathf.Sin(2f * ph);
            if (extraHarmonics) s += 0.08f * Mathf.Sin(3f * ph);
            float env = Mathf.Exp(-t * decay) * (i < atk ? (float)i / atk : 1f);
            buf[idx] += s * env * vol;
        }
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
