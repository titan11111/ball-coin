using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CoinBallGame : MonoBehaviour
{
    enum Phase { Title, Play, Pause, Clear }

    [SerializeField] Rigidbody ball;
    [SerializeField] CoinPickup[] coins;

    Phase phase = Phase.Title;
    Camera cam;
    AudioSource source;
    AudioClip blip;
    AudioClip fanfare;
    AudioClip whoClip;
    AudioClip weatherClip;
    bool moved;
    int remaining;
    float elapsed;
    bool muted;
    bool dragging;
    Vector2 origin;
    Vector2 pointer;
    Vector2 stick;
    Rect[] blocks = new Rect[6];
    int blockCount;
    Texture2D pixel;
    Texture2D titleTex, handTex, creditTex, collectTex, startTex, remainTex, pauseTex, pauseHeadTex;
    Texture2D soundOnTex, soundOffTex, resumeTex, fromStartTex, clearTex, againTex, colonTex;
    Texture2D[] digits = new Texture2D[10];

    const float MaxSpeed = 6.5f;
    const float Accel = 38f;
    const string MuteKey = "ballcoin.mute";

    void Start()
    {
        cam = Camera.main;
        remaining = coins == null ? 0 : coins.Length;
        muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        blip = MakeTone(880f, 0.12f, 10f);
        fanfare = MakeChord();
        pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        titleTex = Resources.Load<Texture2D>("Ui/title");
        handTex = Resources.Load<Texture2D>("Ui/hand");
        creditTex = Resources.Load<Texture2D>("Ui/credit");
        collectTex = Resources.Load<Texture2D>("Ui/collect");
        startTex = Resources.Load<Texture2D>("Ui/start");
        remainTex = Resources.Load<Texture2D>("Ui/remain");
        pauseTex = Resources.Load<Texture2D>("Ui/pause");
        pauseHeadTex = Resources.Load<Texture2D>("Ui/pause_head");
        soundOnTex = Resources.Load<Texture2D>("Ui/sound_on");
        soundOffTex = Resources.Load<Texture2D>("Ui/sound_off");
        resumeTex = Resources.Load<Texture2D>("Ui/resume");
        fromStartTex = Resources.Load<Texture2D>("Ui/from_start");
        clearTex = Resources.Load<Texture2D>("Ui/clear");
        againTex = Resources.Load<Texture2D>("Ui/again");
        colonTex = Resources.Load<Texture2D>("Ui/colon");
        whoClip = Resources.Load<AudioClip>("Voice/who");
        weatherClip = Resources.Load<AudioClip>("Voice/weather");
        for (int i = 0; i < 10; i++) digits[i] = Resources.Load<Texture2D>("Ui/d" + i);
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;
    }

    void Update()
    {
        bool held = false;
        bool began = false;
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            held = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
            began = t.phase == TouchPhase.Began;
            pointer = t.position;
        }
        else
        {
            held = Input.GetMouseButton(0);
            began = Input.GetMouseButtonDown(0);
            pointer = Input.mousePosition;
        }

        if (!held)
        {
            dragging = false;
        }
        else if (began)
        {
            origin = pointer;
            dragging = phase == Phase.Play && !Hits(pointer);
            if (dragging) moved = true;
        }

        if (dragging)
        {
            Vector2 delta = (pointer - origin) / Px(90f);
            stick = delta.sqrMagnitude > 1f ? delta.normalized : delta;
            if (stick.magnitude < 0.12f) stick = Vector2.zero;
        }
        else
        {
            stick = Vector2.zero;
        }

        if (phase == Phase.Title && began && !Hits(pointer)) StartPlay();
        if (phase == Phase.Play) elapsed += Time.deltaTime;
    }

    void FixedUpdate()
    {
        if (phase != Phase.Play || ball == null || cam == null) return;
        KeepInside();
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude < 0.01f) input = stick;
        if (input.sqrMagnitude < 0.01f) return;
        if (input.sqrMagnitude > 1f) input.Normalize();

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = cam.transform.right;
        right.y = 0f;
        right.Normalize();
        ball.AddForce((right * input.x + forward * input.y) * Accel, ForceMode.Acceleration);

        Vector3 v = ball.linearVelocity;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (flat.magnitude > MaxSpeed)
            ball.linearVelocity = new Vector3(flat.normalized.x * MaxSpeed, 0f, flat.normalized.z * MaxSpeed);
    }

    void KeepInside()
    {
        Vector3 p = ball.position;
        Vector3 v = ball.linearVelocity;
        bool hit = false;
        if (p.x < -1.62f) { p.x = -1.62f; v.x = Mathf.Abs(v.x) * 0.7f; hit = true; }
        else if (p.x > 1.62f) { p.x = 1.62f; v.x = -Mathf.Abs(v.x) * 0.7f; hit = true; }
        if (p.z < -1.78f) { p.z = -1.78f; v.z = Mathf.Abs(v.z) * 0.7f; hit = true; }
        else if (p.z > 4.62f) { p.z = 4.62f; v.z = -Mathf.Abs(v.z) * 0.7f; hit = true; }
        if (p.y < 0.2f || p.y > 1.2f) { p.y = 0.45f; v.y = 0f; hit = true; }
        if (!hit) return;
        ball.position = p;
        ball.linearVelocity = v;
    }

    public void Collect(CoinPickup coin)
    {
        if (phase != Phase.Play || coin == null || coin.taken) return;
        coin.taken = true;
        Collider col = coin.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        remaining = Mathf.Max(0, remaining - 1);
        if (!muted && blip != null) source.PlayOneShot(blip);
        StartCoroutine(Shrink(coin.transform));
        if (remaining <= 0)
        {
            phase = Phase.Clear;
            if (ball != null)
            {
                ball.linearVelocity = Vector3.zero;
                ball.angularVelocity = Vector3.zero;
            }
            if (!muted && fanfare != null) source.PlayOneShot(fanfare);
        }
    }

    IEnumerator Shrink(Transform target)
    {
        Vector3 from = target.localScale;
        float t = 0f;
        while (t < 1f && target != null)
        {
            t += Time.deltaTime * 4f;
            target.localScale = Vector3.Lerp(from, Vector3.zero, t);
            yield return null;
        }
        if (target != null) Destroy(target.gameObject);
    }

    void OnGUI()
    {
        blockCount = 0;
        float top = Screen.height - Screen.safeArea.yMax;
        float bottom = Screen.safeArea.yMin;
        float left = Screen.safeArea.xMin;
        float right = Screen.width - Screen.safeArea.xMax;
        float bw = Px(230);
        float bh = Px(76);
        float y = Screen.height - bottom - bh - Px(18);

        if (phase == Phase.Title) DrawTitle();
        else if (phase == Phase.Pause) DrawPause();
        else if (phase == Phase.Clear) DrawClear();

        if (phase == Phase.Play || phase == Phase.Pause)
        {
            float hx = left + Px(20);
            float hy = top + Px(14);
            hx += Blit(remainTex, hx, hy, Px(46)) + Px(10);
            BlitDigits(remaining.ToString(), hx, hy, Px(46));
            DrawClock(Screen.width - right - Px(20), hy, Px(46));
            Button(new Rect(left + Px(16), y, bw, bh), pauseTex, phase == Phase.Pause ? Resume : Pause);
        }
        Button(new Rect(Screen.width - right - Px(16) - bw, y, bw, bh), muted ? soundOffTex : soundOnTex, ToggleMute);
        if (phase == Phase.Play && !moved) DrawHand(Screen.width * 0.5f - Px(100), Screen.height * 0.58f, Px(200));
        if (phase == Phase.Play && dragging) DrawStick();
        DrawCredit(top);
    }

    void DrawTitle()
    {
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.05f, 0.08f, 0.12f, 0.72f));
        float w = Mathf.Min(Screen.width - Px(48), Px(680));
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.16f;
        BlitCentered(titleTex, x, y, w, Px(84));
        DrawHand(x, y + Px(96), w);
        BlitCentered(collectTex, x, y + Px(220), w, Px(48));
        Button(new Rect(x + (w - Px(460)) * 0.5f, y + Px(290), Px(460), Px(92)), startTex, StartPlay);
    }

    void DrawPause()
    {
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.5f));
        float w = Px(440);
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.26f;
        BlitCentered(pauseHeadTex, x, y, w, Px(72));
        Button(new Rect(x, y + Px(110), w, Px(84)), resumeTex, Resume);
        Button(new Rect(x, y + Px(210), w, Px(84)), fromStartTex, Restart);
    }

    void DrawClear()
    {
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.05f, 0.07f, 0.1f, 0.7f));
        float w = Px(560);
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.22f;
        BlitCentered(clearTex, x, y, w, Px(80));
        DrawClock(x + w * 0.5f, y + Px(96), Px(52), true);
        Button(new Rect(x + (w - Px(440)) * 0.5f, y + Px(180), Px(440), Px(88)), againTex, Restart);
    }

    void DrawStick()
    {
        Vector2 a = ToGui(origin);
        Vector2 b = ToGui(pointer);
        Fill(new Rect(a.x - Px(34), a.y - Px(34), Px(68), Px(68)), new Color(1f, 1f, 1f, 0.16f));
        Fill(new Rect(b.x - Px(18), b.y - Px(18), Px(36), Px(36)), new Color(1f, 0.86f, 0.35f, 0.85f));
    }

    void Button(Rect rect, Texture2D icon, System.Action onClick)
    {
        if (blockCount < blocks.Length) blocks[blockCount++] = rect;
        Fill(rect, new Color(0.55f, 0.38f, 0.08f, 1f));
        Fill(new Rect(rect.x + Px(3), rect.y + Px(3), rect.width - Px(6), rect.height - Px(6)), new Color(0.98f, 0.82f, 0.32f, 1f));
        BlitCentered(icon, rect.x, rect.y, rect.width, rect.height * 0.62f);
        if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) onClick();
    }

    float Blit(Texture2D tex, float x, float y, float h)
    {
        if (tex == null || h <= 1f) return 0f;
        float w = h * tex.width / tex.height;
        GUI.DrawTexture(new Rect(x, y + (h - h) * 0.5f, w, h), tex, ScaleMode.ScaleToFit, true);
        return w;
    }

    void BlitCentered(Texture2D tex, float x, float y, float w, float h)
    {
        if (tex == null) return;
        float aspect = tex.width / (float)tex.height;
        float dw = h * aspect;
        float dh = h;
        if (dw > w)
        {
            dw = w;
            dh = w / aspect;
        }
        GUI.DrawTexture(new Rect(x + (w - dw) * 0.5f, y + (h - dh) * 0.5f, dw, dh), tex, ScaleMode.ScaleToFit, true);
    }

    void BlitDigits(string text, float x, float y, float h)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            Texture2D tex = c == ':' ? colonTex : (c >= '0' && c <= '9' ? digits[c - '0'] : null);
            x += Blit(tex, x, y, h);
        }
    }

    void DrawClock(float rightOrCenter, float y, float h, bool center = false)
    {
        string text = Clock(elapsed);
        float width = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            Texture2D tex = c == ':' ? colonTex : digits[c - '0'];
            if (tex != null) width += h * tex.width / tex.height;
        }
        float x = center ? rightOrCenter - width * 0.5f : rightOrCenter - width;
        BlitDigits(text, x, y, h);
    }

    void StartPlay()
    {
        if (phase != Phase.Title) return;
        phase = Phase.Play;
        elapsed = 0f;
        Unlock();
        StartCoroutine(SpeakIntro());
    }

    IEnumerator SpeakIntro()
    {
        yield return Speak(whoClip);
        if (phase != Phase.Play) yield break;
        yield return Speak(weatherClip);
    }

    IEnumerator Speak(AudioClip clip)
    {
        if (clip == null || muted || source == null) yield break;
        source.clip = clip;
        source.Play();
        yield return new WaitForSeconds(clip.length);
    }

    void DrawHand(float x, float y, float w)
    {
        float slide = Mathf.Sin(Time.time * 2.6f) * Px(34);
        BlitCentered(handTex, x + slide, y, w, Px(120));
    }

    void DrawCredit(float top)
    {
        BlitCentered(creditTex, Screen.width - Px(360), top + Px(62), Px(340), Px(30));
    }

    void Pause()
    {
        if (phase != Phase.Play) return;
        phase = Phase.Pause;
        Time.timeScale = 0f;
        dragging = false;
        stick = Vector2.zero;
    }

    void Resume()
    {
        if (phase != Phase.Pause) return;
        phase = Phase.Play;
        Time.timeScale = 1f;
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ToggleMute()
    {
        muted = !muted;
        if (muted && source != null) source.Stop();
        PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
    }

    void Unlock()
    {
        if (source == null || blip == null) return;
        float volume = source.volume;
        source.volume = 0f;
        source.PlayOneShot(blip);
        source.volume = volume;
    }

    bool Hits(Vector2 screenPos)
    {
        Vector2 g = ToGui(screenPos);
        for (int i = 0; i < blockCount; i++)
            if (blocks[i].Contains(g)) return true;
        return false;
    }

    Vector2 ToGui(Vector2 screenPos)
    {
        return new Vector2(screenPos.x, Screen.height - screenPos.y);
    }

    int Px(float design)
    {
        return Mathf.RoundToInt(design * Screen.height / 800f);
    }

    void Fill(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, pixel);
        GUI.color = prev;
    }

    static string Clock(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    static AudioClip MakeTone(float freq, float seconds, float decay)
    {
        int rate = 22050;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-decay * t) * 0.28f;
        }
        AudioClip clip = AudioClip.Create("tone", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeChord()
    {
        int rate = 22050;
        int n = Mathf.CeilToInt(rate * 0.45f);
        float[] data = new float[n];
        float[] freqs = { 523.25f, 659.25f, 783.99f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-3.5f * t);
            float s = 0f;
            for (int f = 0; f < freqs.Length; f++) s += Mathf.Sin(2f * Mathf.PI * freqs[f] * t);
            data[i] = s * env * 0.12f;
        }
        AudioClip clip = AudioClip.Create("clear", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

public class BallTag : MonoBehaviour { }

public class CoinPickup : MonoBehaviour
{
    public CoinBallGame game;
    public bool taken;
    float baseY;
    float phase;

    void Start()
    {
        baseY = transform.position.y;
        phase = transform.position.x * 1.7f + transform.position.z;
    }

    void Update()
    {
        if (taken) return;
        transform.Rotate(0f, 110f * Time.deltaTime, 0f, Space.World);
        Vector3 p = transform.position;
        p.y = baseY + Mathf.Sin(Time.time * 3f + phase) * 0.06f;
        transform.position = p;
    }

    void OnTriggerEnter(Collider other)
    {
        TryTake(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryTake(other);
    }

    void TryTake(Collider other)
    {
        if (taken || game == null || !other.GetComponent<BallTag>()) return;
        game.Collect(this);
    }
}
