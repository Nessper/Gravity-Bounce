using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BallDestructionEffectsPrefabSetup
{
    private const string PrefabPath = "Assets/Project/Prefabs/BallNode.prefab";
    private const string ParticleMaterialPath =
        "Assets/Project/Materials/BallDestructionParticle.mat";
    private const int CurrentPresetVersion = 3;

    static BallDestructionEffectsPrefabSetup()
    {
        EditorApplication.delayCall += EnsurePrefabSetup;
    }

    [MenuItem("Tools/404/Verifier les effets de destruction des billes")]
    private static void EnsurePrefabSetup()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            BallState ballState = root.GetComponent<BallState>();
            if (ballState == null)
                return;

            if (IsSetupComplete(root.transform, ballState))
                return;

            Transform destructionRoot = root.transform.Find("Destruction");
            if (destructionRoot == null)
            {
                GameObject destructionObject = new GameObject("Destruction");
                destructionRoot = destructionObject.transform;
                destructionRoot.SetParent(root.transform, false);
            }

            BallDestructionEffects effects =
                destructionRoot.GetComponent<BallDestructionEffects>();
            if (effects == null)
                effects = destructionRoot.gameObject.AddComponent<BallDestructionEffects>();

            ParticleSystem white = EnsureEffect(
                destructionRoot,
                "White",
                new Color(0.9f, 0.98f, 1f, 1f)
            );
            ParticleSystem blue = EnsureEffect(
                destructionRoot,
                "Blue",
                new Color(0.1f, 0.65f, 1f, 1f)
            );
            ParticleSystem red = EnsureEffect(
                destructionRoot,
                "Red",
                new Color(1f, 0.12f, 0.08f, 1f)
            );
            ParticleSystem black = EnsureEffect(
                destructionRoot,
                "Black",
                new Color(0.55f, 0.12f, 0.75f, 1f)
            );

            Material particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                ParticleMaterialPath
            );
            if (particleMaterial == null)
            {
                EditorApplication.delayCall += EnsurePrefabSetup;
                return;
            }

            ConfigurePlanetExplosion(
                white,
                new Color(1.8f, 1.35f, 0.65f, 1f),
                new Color(0.55f, 1.55f, 2.2f, 1f),
                particleMaterial
            );
            ConfigurePlanetExplosion(
                blue,
                new Color(0.35f, 1.65f, 2.4f, 1f),
                new Color(0.12f, 0.42f, 2f, 1f),
                particleMaterial
            );
            ConfigurePlanetExplosion(
                red,
                new Color(2.3f, 0.85f, 0.08f, 1f),
                new Color(2f, 0.06f, 0.35f, 1f),
                particleMaterial
            );
            ConfigurePlanetExplosion(
                black,
                new Color(1.7f, 0.32f, 2.5f, 1f),
                new Color(0.48f, 0.08f, 1.5f, 1f),
                particleMaterial
            );

            SerializedObject effectsSerialized = new SerializedObject(effects);
            effectsSerialized.FindProperty("whiteEffect").objectReferenceValue = white;
            effectsSerialized.FindProperty("blueEffect").objectReferenceValue = blue;
            effectsSerialized.FindProperty("redEffect").objectReferenceValue = red;
            effectsSerialized.FindProperty("blackEffect").objectReferenceValue = black;
            effectsSerialized.FindProperty("editorPresetVersion").intValue =
                CurrentPresetVersion;
            effectsSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject ballSerialized = new SerializedObject(ballState);
            ballSerialized.FindProperty("destructionEffects").objectReferenceValue = effects;
            ballSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool IsSetupComplete(
        Transform root,
        BallState ballState)
    {
        Transform destructionRoot = root.Find("Destruction");
        if (destructionRoot == null)
            return false;

        BallDestructionEffects effects =
            destructionRoot.GetComponent<BallDestructionEffects>();
        if (effects == null)
            return false;

        string[] childNames = { "White", "Blue", "Red", "Black" };
        foreach (string childName in childNames)
        {
            Transform child = destructionRoot.Find(childName);
            if (child == null || child.GetComponent<ParticleSystem>() == null)
                return false;
        }

        SerializedObject effectsSerialized = new SerializedObject(effects);
        if (effectsSerialized.FindProperty("editorPresetVersion").intValue <
            CurrentPresetVersion)
        {
            return false;
        }

        if (effectsSerialized.FindProperty("whiteEffect").objectReferenceValue == null ||
            effectsSerialized.FindProperty("blueEffect").objectReferenceValue == null ||
            effectsSerialized.FindProperty("redEffect").objectReferenceValue == null ||
            effectsSerialized.FindProperty("blackEffect").objectReferenceValue == null)
        {
            return false;
        }

        SerializedObject ballSerialized = new SerializedObject(ballState);
        return ballSerialized.FindProperty("destructionEffects")
            .objectReferenceValue == effects;
    }

    private static ParticleSystem EnsureEffect(
        Transform parent,
        string objectName,
        Color color)
    {
        Transform child = parent.Find(objectName);
        GameObject effectObject;

        if (child == null)
        {
            effectObject = new GameObject(objectName);
            effectObject.transform.SetParent(parent, false);
        }
        else
        {
            effectObject = child.gameObject;
        }

        ParticleSystem particles = effectObject.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = effectObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.65f;
            main.startSpeed = 2.4f;
            main.startSize = 0.1f;
            main.startColor = color;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 18)
            });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            ParticleSystemRenderer renderer =
                effectObject.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerName = "CleanGameplay";
            renderer.sortingOrder = 120;
        }

        particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
        effectObject.SetActive(true);
        return particles;
    }

    private static void ConfigurePlanetExplosion(
        ParticleSystem particles,
        Color firstColor,
        Color secondColor,
        Material particleMaterial)
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.12f);
        ParticleSystem.MinMaxGradient luminousColors =
            new ParticleSystem.MinMaxGradient(
            firstColor,
            secondColor
        );
        main.startColor = luminousColors;
        main.maxParticles = 96;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 80)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.04f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.35f),
                new GradientAlphaKey(0.35f, 0.78f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = fadeGradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.12f, 1f),
                new Keyframe(0.72f, 0.55f),
                new Keyframe(1f, 0f)
            )
        );

        ParticleSystemRenderer renderer =
            particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = "CleanGameplay";
        renderer.sortingOrder = 120;

        if (particleMaterial != null)
            renderer.sharedMaterial = particleMaterial;

        particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }
}
