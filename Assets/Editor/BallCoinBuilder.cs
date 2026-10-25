using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BallCoinBuilder
{
    public static void BuildWebGL()
    {
        try
        {
            string scenePath = CreateScene();
            ConfigurePlayer();
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = "docs",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Build " + report.summary.result + " errors=" + report.summary.totalErrors);
            File.WriteAllText("build-result.txt", "OK size=" + report.summary.totalSize);
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            File.WriteAllText("build-result.txt", "FAIL\n" + e);
            Debug.LogError(e);
            EditorApplication.Exit(1);
        }
    }

    static string CreateScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        Texture2D checker = Checker();
        Material floorMat = MakeMat("Floor", new Color(0.75f, 0.8f, 0.84f), 0f, 0.08f, checker, new Vector2(8f, 8f), Color.black);
        Material wallMat = MakeMat("Wall", new Color(0.16f, 0.24f, 0.32f), 0.05f, 0.2f, null, Vector2.one, Color.black);
        Material ballMat = MakeMat("Ball", new Color(0.45f, 0.78f, 0.95f), 0.15f, 0.62f, null, Vector2.one, Color.black);
        Material coinMat = MakeMat("Coin", new Color(1f, 0.78f, 0.18f), 0.85f, 0.72f, null, Vector2.one, new Color(0.45f, 0.28f, 0.02f));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Camera cam = Camera.main;
        cam.transform.position = new Vector3(0f, 16f, -4.2f);
        cam.transform.rotation = Quaternion.Euler(72f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.12f, 0.18f);
        cam.fieldOfView = 36f;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.48f, 0.55f);

        Light light = Object.FindFirstObjectByType<Light>();
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(54f, -28f, 0f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, 0.97f, 0.9f);
        }

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        Wall(wallMat, new Vector3(0f, 1f, 7.5f), new Vector3(16f, 2f, 1f));
        Wall(wallMat, new Vector3(0f, 1f, -7.5f), new Vector3(16f, 2f, 1f));
        Wall(wallMat, new Vector3(7.5f, 1f, 0f), new Vector3(1f, 2f, 16f));
        Wall(wallMat, new Vector3(-7.5f, 1f, 0f), new Vector3(1f, 2f, 16f));

        GameObject ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballGo.name = "Ball";
        ballGo.transform.position = new Vector3(0f, 0.45f, 0f);
        ballGo.transform.localScale = Vector3.one * 0.9f;
        ballGo.GetComponent<Renderer>().sharedMaterial = ballMat;
        ballGo.AddComponent<BallTag>();
        Rigidbody rb = ballGo.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 1.35f;
        rb.angularDamping = 0.35f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezePositionY;
        PhysicsMaterial friction = new PhysicsMaterial("Ball");
        friction.dynamicFriction = 0.45f;
        friction.staticFriction = 0.45f;
        friction.bounciness = 0.12f;
        AssetDatabase.CreateAsset(friction, "Assets/Materials/Ball.physicMaterial");
        ballGo.GetComponent<SphereCollider>().material = friction;

        GameObject host = new GameObject("Game");
        CoinBallGame game = host.AddComponent<CoinBallGame>();

        GameObject holder = new GameObject("Coins");
        CoinPickup[] coins = new CoinPickup[12];
        for (int i = 0; i < coins.Length; i++)
        {
            float ang = i * Mathf.PI * 2f / coins.Length;
            float rad = (i % 2 == 0) ? 1.85f : 3.45f;
            GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coin.name = "Coin" + i;
            coin.transform.SetParent(holder.transform);
            coin.transform.position = new Vector3(Mathf.Cos(ang) * rad, 0.16f, Mathf.Sin(ang) * rad);
            coin.transform.localScale = new Vector3(0.72f, 0.07f, 0.72f);
            coin.GetComponent<Renderer>().sharedMaterial = coinMat;
            Object.DestroyImmediate(coin.GetComponent<Collider>());
            BoxCollider box = coin.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.4f, 18f, 2.4f);
            CoinPickup pickup = coin.AddComponent<CoinPickup>();
            pickup.game = game;
            coins[i] = pickup;
        }

        SerializedObject so = new SerializedObject(game);
        so.FindProperty("ball").objectReferenceValue = rb;
        SerializedProperty list = so.FindProperty("coins");
        list.arraySize = coins.Length;
        for (int i = 0; i < coins.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = coins[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        string scenePath = "Assets/Scenes/Main.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        AssetDatabase.SaveAssets();
        return scenePath;
    }

    static void Wall(Material mat, Vector3 pos, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material MakeMat(string name, Color color, float metallic, float gloss, Texture2D tex, Vector2 scale, Color emission)
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) throw new System.Exception("Standard shader missing");
        Material mat = new Material(shader);
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", gloss);
        if (tex != null)
        {
            mat.mainTexture = tex;
            mat.mainTextureScale = scale;
        }
        if (emission.maxColorComponent > 0.001f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
        }
        AssetDatabase.CreateAsset(mat, "Assets/Materials/" + name + ".mat");
        return mat;
    }

    static Texture2D Checker()
    {
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color a = new Color(0.20f, 0.33f, 0.42f);
        Color b = new Color(0.15f, 0.26f, 0.34f);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                tex.SetPixel(x, y, ((x + y) & 1) == 0 ? a : b);
        tex.Apply();
        string path = "Assets/Materials/Floor.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.companyName = "Titans";
        PlayerSettings.productName = "101-コイン集め";
        PlayerSettings.bundleVersion = "1.0";
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 600;
        PlayerSettings.runInBackground = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.template = "PROJECT:Phone";
        PlayerSettings.WebGL.initialMemorySize = 32;
        PlayerSettings.WebGL.maximumMemorySize = 256;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
    }
}
