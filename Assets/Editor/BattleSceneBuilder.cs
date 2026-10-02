using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DragonBattle.EditorTools
{
    /// <summary>
    /// One-click builder: menu "Dragon Battle / Build Battle Scene".
    /// Generates textures, icons, sounds, materials and prefabs, then (re)builds SampleScene:
    /// arena, two dragons, camera, lighting, managers and the full UI. Safe to run repeatedly.
    /// </summary>
    public static class BattleSceneBuilder
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string MatDir = "Assets/Materials";
        private const string DragonPack = "Assets/FourEvilDragonsHP";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        private static Font font;
        private static Sprite whiteSprite, circleSprite, iconFire, iconTail, iconFly;
        private static Texture2D stoneTex, glowTex, ringTex;
        private static Material fireMat, glowMat;
        private static PhysicMaterial noFriction;

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        [MenuItem("Dragon Battle/Build Battle Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MatDir);

            BattleAssetGenerator.GenerateTextures();
            BattleAssetGenerator.GenerateAudio();
            LoadSharedAssets();

            // Dragons come from the "Four Evil Dragons HP" asset pack.
            var playerPrefab = BuildModelDragonPrefab("Dragon_Player", "You", true,
                DragonPack + "/Prefab/DragonTerrorBringer/Red.prefab", DragonPack + "/Animations/DragonTerrorBringer",
                new Color(0.82f, 0.16f, 0.1f), new Color(1f, 0.4f, 0.1f), new Color(1f, 0.45f, 0.15f));
            var enemyPrefab = BuildModelDragonPrefab("Dragon_Enemy", "AI", false,
                DragonPack + "/Prefab/DragonUsurper/Purple.prefab", DragonPack + "/Animations/DragonUsurper",
                new Color(0.45f, 0.2f, 0.8f), new Color(0.7f, 0.4f, 1f), new Color(0.5f, 0.3f, 0.9f));

            var hitSpark = MakeHitSparkPrefab();
            var shockwave = MakeShockwavePrefab();
            var popup = MakeDamagePopupPrefab();

            BuildScene(playerPrefab, enemyPrefab, hitSpark, shockwave, popup);

            AssetDatabase.SaveAssets();
            Debug.Log("[DragonBattle] Scene built. Press Play!");
        }

        // ================================================================ shared assets

        private static void LoadSharedAssets()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/White.png");
            circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Circle.png");
            iconFire = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icon_Fire.png");
            iconTail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icon_Tail.png");
            iconFly = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icon_Fly.png");
            stoneTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Stone.png");
            glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Glow.png");
            ringTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Ring.png");

            fireMat = Additive("FX_Fire", glowTex, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            glowMat = Additive("FX_Glow", glowTex, new Color(0.5f, 0.5f, 0.5f, 0.5f));

            string pmPath = MatDir + "/NoFriction.physicMaterial";
            AssetDatabase.DeleteAsset(pmPath);
            noFriction = new PhysicMaterial("NoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(noFriction, pmPath);

            // Make sure the new material assets are fully imported before prefabs reference them.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            fireMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/FX_Fire.mat");
            glowMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/FX_Glow.mat");
        }

        private static Material Lit(string name, Color color, float smoothness = 0.35f, float metallic = 0f,
                                    Texture tex = null, Vector2? tiling = null)
        {
            string path = $"{MatDir}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material Emissive(string name, Color color, float intensity)
        {
            var m = Lit(name, color, 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material Additive(string name, Texture tex, Color tint)
        {
            string path = $"{MatDir}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = new Material(sh) { name = name };
            m.SetTexture("_MainTex", tex);
            m.SetColor("_TintColor", tint);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ================================================================ primitives & particles

        private static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        private static Transform Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale,
                                      Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = euler;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static ParticleSystem NewParticles(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        private static Gradient MakeGradient(Color a, Color b, Color c, Color d)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.2f), new GradientColorKey(c, 0.6f), new GradientColorKey(d, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static ParticleSystem MakeFireBreath(Transform parent, Color hot, Color mid)
        {
            var ps = NewParticles("FireBreath", parent, fireMat);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            var em = ps.emission;
            em.rateOverTime = 150f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.12f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(Color.white, hot, mid, new Color(0.25f, 0.05f, 0.05f));

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.6f)));

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.8f;
            return ps;
        }

        private static ParticleSystem MakeTorchFlame(Transform parent, Color hot, Color mid)
        {
            var ps = NewParticles("TorchFlame", parent, fireMat);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 28f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.25f;
            shape.rotation = V(-90f, 0f, 0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(Color.white, hot, mid, new Color(0.2f, 0.04f, 0.02f));

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(1f, 0.1f)));
            ps.Play();
            return ps;
        }

        private static GameObject MakeHitSparkPrefab()
        {
            var go = new GameObject("HitSpark");
            var ps = NewParticles("Sparks", go.transform, glowMat);
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor = new Color(1f, 0.8f, 0.35f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            // The root object has no system itself; destroy it together with the child.
            var cleanup = go.AddComponent<DestroyAfter>();
            cleanup.seconds = 1f;
            return SavePrefab(go, "HitSpark");
        }

        private static GameObject MakeShockwavePrefab()
        {
            var go = new GameObject("Shockwave");
            var ps = NewParticles("Ring", go.transform, glowMat);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = 0.55f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startColor = new Color(1f, 0.7f, 0.4f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 44) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.radiusThickness = 0f;
            shape.rotation = V(90f, 0f, 0f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(Color.white, new Color(1f, 0.8f, 0.5f), new Color(0.8f, 0.4f, 0.2f), Color.black);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

            var cleanup = go.AddComponent<DestroyAfter>();
            cleanup.seconds = 1.2f;
            return SavePrefab(go, "Shockwave");
        }

        private static GameObject MakeDamagePopupPrefab()
        {
            var go = new GameObject("DamagePopup");
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = new Color(1f, 0.92f, 0.3f, 1f);
            tm.text = "0";

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            go.AddComponent<DamagePopup>().text = tm;
            return SavePrefab(go, "DamagePopup");
        }

        private static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ================================================================ asset-pack dragon prefab

        private static GameObject BuildModelDragonPrefab(string prefabName, string displayName, bool isPlayer,
            string sourcePrefabPath, string animFolder, Color theme, Color flameHot, Color flameMid)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (source == null)
            {
                Debug.LogError("[DragonBattle] Dragon prefab not found: " + sourcePrefabPath);
                return null;
            }

            var root = new GameObject(prefabName);
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 8f;
            rb.drag = 0f;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = root.AddComponent<SphereCollider>();
            col.radius = 1.1f;
            col.center = V(0f, 1.2f, 0f);
            col.sharedMaterial = noFriction;

            // ---- model: copy of the pack prefab, URP materials, scaled to arena size
            var model = Object.Instantiate(source);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int r = 0; r < renderers.Length; r++)
            {
                var old = renderers[r].sharedMaterials;
                var fresh = new Material[old.Length];
                for (int m = 0; m < old.Length; m++) fresh[m] = ToUrpMaterial(old[m], $"{prefabName}_Skin{r}_{m}");
                renderers[r].sharedMaterials = fresh;
                renderers[r].updateWhenOffscreen = true;
            }

            // Fit the dragon into the arena: longest horizontal side of the idle pose = TargetSize.
            const float TargetSize = 6f;
            Bounds b = WorldBounds(renderers);
            float longest = Mathf.Max(b.size.x, b.size.z);
            float scale = longest > 0.001f ? TargetSize / longest : 1f;
            model.transform.localScale = Vector3.one * scale;
            b = WorldBounds(renderers);
            model.transform.localPosition = V(0f, -b.min.y, 0f); // stand on the floor
            b = WorldBounds(renderers);

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = BuildAnimatorController(prefabName, animFolder);

            // ---- visual (only lifts the model while flying; the Animator does the rest)
            var visual = model.AddComponent<DragonVisual>();
            visual.modelRoot = model.transform;
            visual.renderers = renderers;

            // ---- mouth for the fire breath: at the head bone (bind pose), pushed slightly forward
            Transform headBone = FindDeep(model.transform, "Head");
            Vector3 mouthWorld = headBone != null ? headBone.position : V(0f, b.size.y * 0.7f, b.max.z * 0.6f);
            var mouth = Pivot("Mouth", root.transform, Vector3.zero);
            mouth.position = mouthWorld + root.transform.forward * (TargetSize * 0.08f);
            mouth.localRotation = Quaternion.identity;

            var fire = MakeFireBreath(mouth, flameHot, flameMid);
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(mouth, false);
            lightGo.transform.localPosition = V(0f, 0f, 1.5f);
            var fireLight = lightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = flameMid;
            fireLight.intensity = 4f;
            fireLight.range = 10f;
            fireLight.shadows = LightShadows.None;
            fireLight.enabled = false;

            // ---- gameplay components
            var health = root.AddComponent<Health>();
            var dragon = root.AddComponent<Dragon>();
            dragon.displayName = displayName;
            dragon.themeColor = Color.Lerp(theme, Color.white, 0.25f);
            dragon.visual = visual;
            dragon.animator = animator;
            dragon.fireParticles = fire;
            dragon.fireLight = fireLight;
            dragon.arenaHalfSize = new Vector2(9.8f, 6.3f);
            dragon.fire.color = new Color(1f, 0.5f, 0.15f);
            dragon.tail.color = new Color(0.4f, 0.85f, 0.4f);
            dragon.fly.color = new Color(0.45f, 0.7f, 1f);

            if (isPlayer) root.AddComponent<PlayerController>();
            else root.AddComponent<EnemyAI>();

            BuildWorldHealthBar(root.transform, health, dragon, b.max.y + 0.8f);
            return SavePrefab(root, prefabName);
        }

        private static Bounds WorldBounds(SkinnedMeshRenderer[] renderers)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>The pack ships Standard-shader materials (pink in URP); make a URP Lit copy.</summary>
        private static Material ToUrpMaterial(Material src, string name)
        {
            string path = $"{MatDir}/{name}.mat";
            AssetDatabase.DeleteAsset(path);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            Texture tex = null;
            if (src != null)
            {
                if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                if (src.HasProperty("_Color")) m.SetColor("_BaseColor", src.GetColor("_Color"));
            }
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", 0.25f);
            m.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// Animator for Dragon.cs: float Speed, triggers Fire / Tail / Fly / Hit / Die.
        /// Attack clips are time-scaled to match the length of each ability in Dragon.cs.
        /// </summary>
        private static RuntimeAnimatorController BuildAnimatorController(string name, string animFolder)
        {
            var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            foreach (string file in Directory.GetFiles(animFolder, "*.fbx"))
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(file.Replace('\\', '/')))
                    if (a is AnimationClip c && !c.name.StartsWith("__preview__")) clips[c.name] = c;

            AnimationClip Clip(string clipName)
            {
                if (!clips.TryGetValue(clipName, out var c)) Debug.LogWarning($"[DragonBattle] Missing clip '{clipName}' in {animFolder}");
                return c;
            }

            string path = $"{PrefabDir}/{name}_Animator.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            foreach (var t in new[] { "Fire", "Tail", "Fly", "Hit", "Die" })
                ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            // Locomotion blend tree: idle -> walk -> run
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Idle01"), 0f);
            tree.AddChild(Clip("Walk"), 0.35f);
            tree.AddChild(Clip("Run"), 1f);
            sm.defaultState = loco;

            AnimatorState Attack(string stateName, string clipName, string trigger, float duration)
            {
                var clip = Clip(clipName);
                var st = sm.AddState(stateName);
                st.motion = clip;
                if (clip != null && duration > 0f) st.speed = clip.length / duration;
                var any = sm.AddAnyStateTransition(st);
                any.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                any.hasExitTime = false;
                any.duration = 0.08f;
                any.canTransitionToSelf = false;
                return st;
            }

            void ExitTo(AnimatorState from, AnimatorState to, float exitTime, float blend)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = true;
                t.exitTime = exitTime;
                t.duration = blend;
            }

            // Fire breath ~1.7s, claw swipe ~0.95s (see Dragon.cs routines)
            var fire = Attack("Fire", "Flame Attack", "Fire", 1.7f);
            ExitTo(fire, loco, 0.95f, 0.1f);
            var claw = Attack("Tail", "Claw Attack", "Tail", 0.95f);
            ExitTo(claw, loco, 0.95f, 0.1f);

            // Sky strike: airborne ~1.7s, then land
            var flyClip = Clip("Fly Forward");
            var fly = Attack("Fly", "Fly Forward", "Fly", 0f);
            var land = sm.AddState("Land");
            var landClip = Clip("Land");
            land.motion = landClip;
            if (landClip != null) land.speed = landClip.length / 0.5f;
            ExitTo(fly, land, flyClip != null ? 1.65f / flyClip.length : 1f, 0.1f);
            ExitTo(land, loco, 0.95f, 0.1f);

            // Hit reaction (only from locomotion so it never cuts an attack short)
            var hit = sm.AddState("Hit");
            var hitClip = Clip("Get Hit");
            hit.motion = hitClip;
            if (hitClip != null) hit.speed = hitClip.length / 0.45f;
            var toHit = loco.AddTransition(hit);
            toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            toHit.hasExitTime = false;
            toHit.duration = 0.05f;
            ExitTo(hit, loco, 0.95f, 0.1f);

            // Death
            var die = sm.AddState("Die");
            die.motion = Clip("Die");
            var toDie = sm.AddAnyStateTransition(die);
            toDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");
            toDie.hasExitTime = false;
            toDie.duration = 0.1f;
            toDie.canTransitionToSelf = false;

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ================================================================ dragon prefab

        private static GameObject BuildDragonPrefab(string prefabName, string displayName, bool isPlayer,
            Color body, Color belly, Color accent, Color eye, Color flameHot, Color flameMid)
        {
            var mBody = Lit(prefabName + "_Body", body, 0.45f);
            var mBelly = Lit(prefabName + "_Belly", belly, 0.3f);
            var mAccent = Lit(prefabName + "_Accent", accent, 0.55f, 0.1f);
            var mEye = Emissive(prefabName + "_Eye", eye, 3f);

            var root = new GameObject(prefabName);
            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 8f;
            rb.drag = 0f;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var col = root.AddComponent<SphereCollider>();
            col.radius = 1.1f;
            col.center = V(0f, 1.2f, 0f);
            col.sharedMaterial = noFriction;

            // ---- model hierarchy: Model > Spin > Rig > parts
            var model = Pivot("Model", root.transform, Vector3.zero);
            var visual = model.gameObject.AddComponent<DragonVisual>();
            var spin = Pivot("Spin", model, Vector3.zero);
            var rig = Pivot("Rig", spin, Vector3.zero);

            Prim(PrimitiveType.Sphere, "Body", rig, V(0f, 1.35f, 0f), V(1.7f, 1.4f, 2.3f), mBody);
            Prim(PrimitiveType.Sphere, "Belly", rig, V(0f, 1.1f, 0.1f), V(1.4f, 1f, 2f), mBelly);
            Prim(PrimitiveType.Sphere, "Neck1", rig, V(0f, 1.75f, 1.05f), V(0.75f, 0.75f, 0.9f), mBody);
            Prim(PrimitiveType.Sphere, "Neck2", rig, V(0f, 2.05f, 1.4f), V(0.65f, 0.65f, 0.8f), mBody);
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cube, "Spike" + i, rig, V(0f, 2.05f - Mathf.Abs(i - 2) * 0.12f, -0.9f + i * 0.45f),
                     V(0.12f, 0.38f, 0.3f), mAccent, V(45f, 0f, 0f));

            // ---- head
            var head = Pivot("Head", rig, V(0f, 2.3f, 1.75f));
            Prim(PrimitiveType.Sphere, "Skull", head, V(0f, 0f, 0.1f), V(0.85f, 0.75f, 1f), mBody);
            Prim(PrimitiveType.Cube, "Snout", head, V(0f, -0.1f, 0.7f), V(0.5f, 0.32f, 0.65f), mBody);
            var jaw = Pivot("Jaw", head, V(0f, -0.2f, 0.35f));
            Prim(PrimitiveType.Cube, "JawMesh", jaw, V(0f, 0f, 0.38f), V(0.46f, 0.12f, 0.75f), mBelly);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, "Eye", head, V(0.32f * s, 0.15f, 0.4f), V(0.2f, 0.2f, 0.2f), mEye);
                Prim(PrimitiveType.Cube, "Horn", head, V(0.3f * s, 0.4f, -0.3f), V(0.12f, 0.12f, 0.75f), mAccent, V(35f, 12f * s, 0f));
            }
            var mouth = Pivot("Mouth", head, V(0f, -0.1f, 1.05f));

            // ---- wings (pivot at the shoulder, animated in DragonVisual)
            var wings = new Transform[2];
            for (int w = 0; w < 2; w++)
            {
                float s = w == 0 ? -1f : 1f;
                var pivot = Pivot(w == 0 ? "WingL" : "WingR", rig, V(0.75f * s, 1.9f, 0.25f));
                Prim(PrimitiveType.Cube, "Arm", pivot, V(0.95f * s, 0f, 0f), V(1.9f, 0.1f, 0.14f), mBody);
                Prim(PrimitiveType.Cube, "Membrane", pivot, V(1f * s, 0f, -0.6f), V(1.8f, 0.03f, 1.3f), mAccent);
                Prim(PrimitiveType.Cube, "Finger", pivot, V(1.75f * s, 0f, -0.5f), V(0.08f, 0.08f, 1.4f), mBody, V(0f, -12f * s, 0f));
                wings[w] = pivot;
            }

            // ---- legs
            var legs = new Transform[4];
            float[] lx = { -0.7f, 0.7f, -0.7f, 0.7f };
            float[] lz = { 0.7f, 0.7f, -0.7f, -0.7f };
            for (int i = 0; i < 4; i++)
            {
                var leg = Pivot("Leg" + i, rig, V(lx[i], 0.95f, lz[i]));
                Prim(PrimitiveType.Cylinder, "Thigh", leg, V(0f, -0.45f, 0f), V(0.4f, 0.45f, 0.4f), mBody);
                Prim(PrimitiveType.Cube, "Foot", leg, V(0f, -0.92f, 0.12f), V(0.5f, 0.16f, 0.7f), mAccent);
                legs[i] = leg;
            }

            // ---- tail chain
            float[] widths = { 0.85f, 0.7f, 0.58f, 0.46f, 0.35f, 0.25f };
            var tail = new Transform[widths.Length];
            Transform parent = rig;
            for (int i = 0; i < widths.Length; i++)
            {
                var seg = Pivot("Tail" + i, parent, i == 0 ? V(0f, 1.15f, -1.0f) : V(0f, 0f, -0.7f));
                Prim(PrimitiveType.Sphere, "Mesh", seg, V(0f, 0f, -0.3f), V(widths[i], widths[i] * 0.9f, 0.95f), mBody);
                tail[i] = seg;
                parent = seg;
            }
            Prim(PrimitiveType.Cube, "TailSpike", tail[tail.Length - 1], V(0f, 0f, -0.85f), V(0.2f, 0.2f, 0.55f), mAccent, V(0f, 0f, 45f));

            var trail = tail[tail.Length - 1].gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = 0.7f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.1f;
            trail.sharedMaterial = glowMat;
            trail.emitting = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var tg = new Gradient();
            tg.SetKeys(new[] { new GradientColorKey(flameHot, 0f), new GradientColorKey(flameMid, 1f) },
                       new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = tg;

            // ---- fire breath + light
            var fire = MakeFireBreath(mouth, flameHot, flameMid);
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(mouth, false);
            lightGo.transform.localPosition = V(0f, 0f, 1.5f);
            var fireLight = lightGo.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = flameMid;
            fireLight.intensity = 4f;
            fireLight.range = 10f;
            fireLight.shadows = LightShadows.None;
            fireLight.enabled = false;

            // ---- wire the visual
            visual.spinRoot = spin;
            visual.rig = rig;
            visual.head = head;
            visual.jaw = jaw;
            visual.wingL = wings[0];
            visual.wingR = wings[1];
            visual.tail = tail;
            visual.legs = legs;
            visual.tailTrail = trail;
            visual.renderers = model.GetComponentsInChildren<MeshRenderer>();

            // ---- gameplay components
            var health = root.AddComponent<Health>();
            var dragon = root.AddComponent<Dragon>();
            dragon.displayName = displayName;
            dragon.themeColor = Color.Lerp(body, Color.white, 0.25f);
            dragon.visual = visual;
            dragon.fireParticles = fire;
            dragon.fireLight = fireLight;
            dragon.arenaHalfSize = new Vector2(9.8f, 6.3f);
            dragon.fire.color = new Color(1f, 0.5f, 0.15f);
            dragon.tail.color = new Color(0.4f, 0.85f, 0.4f);
            dragon.fly.color = new Color(0.45f, 0.7f, 1f);

            if (isPlayer) root.AddComponent<PlayerController>();
            else root.AddComponent<EnemyAI>();

            BuildWorldHealthBar(root.transform, health, dragon);
            return SavePrefab(root, prefabName);
        }

        private static void BuildWorldHealthBar(Transform dragonRoot, Health health, Dragon dragon, float height = 4.7f)
        {
            var go = new GameObject("HealthBar", typeof(RectTransform));
            go.transform.SetParent(dragonRoot, false);
            go.transform.localPosition = V(0f, height, 0f);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(220f, 50f);
            rt.localScale = Vector3.one * 0.014f;

            var bg = Img("Background", go.transform, whiteSprite, new Color(0f, 0f, 0f, 0.8f), Anchor.Center, V(0f, -8f, 0f), new Vector2(224f, 24f));
            var trail = Img("Trail", go.transform, whiteSprite, new Color(1f, 1f, 1f, 0.85f), Anchor.Center, V(0f, -8f, 0f), new Vector2(216f, 16f));
            var fill = Img("Fill", go.transform, whiteSprite, dragon.themeColor, Anchor.Center, V(0f, -8f, 0f), new Vector2(216f, 16f));
            foreach (var img in new[] { trail, fill })
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = 0;
            }
            fill.color = Color.Lerp(dragon.themeColor, Color.white, 0.1f);
            bg.raycastTarget = false;

            Txt("Name", go.transform, dragon.displayName.ToUpper(), 30, Color.white, TextAnchor.MiddleCenter,
                Anchor.Center, V(0f, 18f, 0f), new Vector2(240f, 36f));

            var bar = go.AddComponent<WorldHealthBar>();
            bar.health = health;
            bar.fill = fill;
            bar.trail = trail;
        }

        // ================================================================ scene

        private static void BuildScene(GameObject playerPrefab, GameObject enemyPrefab,
                                       GameObject hitSpark, GameObject shockwave, GameObject popup)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Keep camera, light and volume; rebuild everything else.
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Main Camera" || root.name == "Directional Light" || root.name == "Global Volume") continue;
                Object.DestroyImmediate(root);
            }

            SetupLighting();
            BuildArena();

            var characters = new GameObject("Characters").transform;
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, characters);
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, characters);
            player.name = "PlayerDragon";
            enemy.name = "EnemyDragon";
            player.transform.SetPositionAndRotation(V(-5f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            enemy.transform.SetPositionAndRotation(V(5f, 0f, 0f), Quaternion.LookRotation(Vector3.left));

            var playerDragon = player.GetComponent<Dragon>();
            var enemyDragon = enemy.GetComponent<Dragon>();
            playerDragon.opponent = enemyDragon;
            enemyDragon.opponent = playerDragon;

            var camRig = SetupCamera(player.transform, enemy.transform);

            var managers = new GameObject("Managers").transform;
            var gm = new GameObject("GameManager").AddComponent<GameManager>();
            var fb = new GameObject("FeedbackManager").AddComponent<FeedbackManager>();
            var am = new GameObject("AudioManager").AddComponent<AudioManager>();
            gm.transform.SetParent(managers, false);
            fb.transform.SetParent(managers, false);
            am.transform.SetParent(managers, false);

            fb.hitSparkPrefab = hitSpark;
            fb.shockwavePrefab = shockwave;
            fb.damagePopupPrefab = popup;
            fb.cameraRig = camRig;

            am.roar = LoadClip("Roar");
            am.fireBreath = LoadClip("Fire");
            am.tailWhoosh = LoadClip("TailWhoosh");
            am.tailHit = LoadClip("TailHit");
            am.flap = LoadClip("Flap");
            am.slam = LoadClip("Slam");
            am.hit = LoadClip("Hit");
            am.win = LoadClip("Win");
            am.beep = LoadClip("Beep");
            am.go = LoadClip("Go");
            am.music = LoadClip("Music");

            gm.player = playerDragon;
            gm.enemy = enemyDragon;
            gm.ui = BuildUI(playerDragon, enemyDragon, gm);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static AudioClip LoadClip(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/{name}.wav");

        private static void SetupLighting()
        {
            foreach (var l in Object.FindObjectsOfType<Light>())
            {
                if (l.type != LightType.Directional) continue;
                l.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
                l.color = new Color(1f, 0.93f, 0.82f);
                l.intensity = 1.25f;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.9f;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.34f, 0.42f);

            // Bloom makes the fire glow. Added to the scene's existing global volume profile.
            var volume = Object.FindObjectOfType<Volume>();
            if (volume != null && volume.sharedProfile != null)
            {
                if (!volume.sharedProfile.TryGet(out Bloom bloom)) bloom = volume.sharedProfile.Add<Bloom>(true);
                bloom.active = true;
                bloom.intensity.Override(0.7f);
                bloom.threshold.Override(1f);
                bloom.scatter.Override(0.6f);
                EditorUtility.SetDirty(volume.sharedProfile);
            }
        }

        private static CameraRig SetupCamera(Transform a, Transform b)
        {
            var cam = Camera.main;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 150f;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var rig = cam.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
            rig.targetA = a;
            rig.targetB = b;
            rig.pitch = 60f;
            rig.minDistance = 20f;
            rig.maxDistance = 25f;

            var rot = Quaternion.Euler(rig.pitch, rig.yaw, 0f);
            cam.transform.SetPositionAndRotation(-(rot * Vector3.forward) * rig.minDistance, rot);
            return rig;
        }

        // ================================================================ arena

        private static void BuildArena()
        {
            var env = new GameObject("Environment").transform;

            var floorMat = Lit("Arena_Floor", Color.white, 0.25f, 0f, stoneTex, new Vector2(2.75f, 1.875f));
            var wallMat = Lit("Arena_Wall", new Color(0.36f, 0.34f, 0.36f), 0.2f, 0f, stoneTex, new Vector2(6f, 1f));
            var capMat = Lit("Arena_WallCap", new Color(0.55f, 0.52f, 0.5f), 0.3f);
            var pillarMat = Lit("Arena_Pillar", new Color(0.3f, 0.28f, 0.3f), 0.25f, 0f, stoneTex, new Vector2(1f, 2f));
            var groundMat = Lit("Arena_OuterGround", new Color(0.07f, 0.07f, 0.09f), 0.1f);
            var brazierMat = Lit("Arena_Brazier", new Color(0.15f, 0.12f, 0.1f), 0.6f, 0.6f);

            // Floor (22 x 15 m) and dark ground around it.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(env, false);
            floor.transform.localScale = V(2.2f, 1f, 1.5f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "OuterGround";
            ground.transform.SetParent(env, false);
            ground.transform.position = V(0f, -0.05f, 0f);
            ground.transform.localScale = V(10f, 1f, 10f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;
            Object.DestroyImmediate(ground.GetComponent<Collider>());

            // Centre emblem.
            var emblemMat = Additive("FX_Emblem", ringTex, new Color(0.45f, 0.28f, 0.12f, 0.5f));
            var emblem = Prim(PrimitiveType.Quad, "Emblem", env, V(0f, 0.03f, 0f), V(10f, 10f, 1f), emblemMat, V(90f, 0f, 0f));
            emblem.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Boundary walls (colliders keep the dragons inside the play area).
            var walls = new GameObject("Walls").transform;
            walls.SetParent(env, false);
            Wall("Wall_North", walls, V(0f, 0.8f, 8f), V(25f, 1.6f, 1f), wallMat);
            Wall("Wall_South", walls, V(0f, 0.8f, -8f), V(25f, 1.6f, 1f), wallMat);
            Wall("Wall_East", walls, V(11.5f, 0.8f, 0f), V(1f, 1.6f, 16f), wallMat);
            Wall("Wall_West", walls, V(-11.5f, 0.8f, 0f), V(1f, 1.6f, 16f), wallMat);
            Prim(PrimitiveType.Cube, "Cap_North", walls, V(0f, 1.75f, 8f), V(25.4f, 0.3f, 1.4f), capMat);
            Prim(PrimitiveType.Cube, "Cap_South", walls, V(0f, 1.75f, -8f), V(25.4f, 0.3f, 1.4f), capMat);
            Prim(PrimitiveType.Cube, "Cap_East", walls, V(11.5f, 1.75f, 0f), V(1.4f, 0.3f, 16.4f), capMat);
            Prim(PrimitiveType.Cube, "Cap_West", walls, V(-11.5f, 1.75f, 0f), V(1.4f, 0.3f, 16.4f), capMat);

            // Corner pillars with burning braziers.
            var torches = new GameObject("Torches").transform;
            torches.SetParent(env, false);
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    var pos = V(11.5f * sx, 0f, 8f * sz);
                    var pillar = Prim(PrimitiveType.Cube, "Pillar", torches, pos + V(0f, 1.7f, 0f), V(1.7f, 3.4f, 1.7f), pillarMat);
                    Prim(PrimitiveType.Cylinder, "Brazier", torches, pos + V(0f, 3.55f, 0f), V(1.1f, 0.2f, 1.1f), brazierMat);
                    var flame = MakeTorchFlame(torches, new Color(1f, 0.75f, 0.3f), new Color(1f, 0.3f, 0.08f));
                    flame.transform.position = pos + V(0f, 3.7f, 0f);

                    var lightGo = new GameObject("TorchLight");
                    lightGo.transform.SetParent(torches, false);
                    lightGo.transform.position = pos + V(0f, 4.4f, 0f) + V(-sx * 0.5f, 0f, -sz * 0.5f);
                    var l = lightGo.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.55f, 0.2f);
                    l.intensity = 3f;
                    l.range = 11f;
                    l.shadows = LightShadows.None;
                    lightGo.AddComponent<LightFlicker>();
                    pillar.name = "Pillar";
                }
            }
        }

        private static void Wall(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ================================================================ UI

        private enum Anchor { Center, TopLeft, TopRight, BottomCenter, BottomLeft, Stretch }

        private static RectTransform Rect(string name, Transform parent, Anchor anchor, Vector3 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            Vector2 a, pivot;
            switch (anchor)
            {
                case Anchor.TopLeft: a = new Vector2(0f, 1f); pivot = a; break;
                case Anchor.TopRight: a = new Vector2(1f, 1f); pivot = a; break;
                case Anchor.BottomCenter: a = new Vector2(0.5f, 0f); pivot = new Vector2(0.5f, 0.5f); break;
                case Anchor.BottomLeft: a = Vector2.zero; pivot = a; break;
                case Anchor.Stretch: a = Vector2.zero; pivot = new Vector2(0.5f, 0.5f); break;
                default: a = new Vector2(0.5f, 0.5f); pivot = a; break;
            }

            rt.anchorMin = a;
            rt.anchorMax = anchor == Anchor.Stretch ? Vector2.one : a;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = anchor == Anchor.Stretch ? Vector2.zero : size;
            return rt;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Color color, Anchor anchor, Vector3 pos, Vector2 size)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text Txt(string name, Transform parent, string text, int fontSize, Color color, TextAnchor align,
                                Anchor anchor, Vector3 pos, Vector2 size, bool outline = true)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = align;
            t.color = color;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.9f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        private static void Fit(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void BuildHudBar(Transform parent, Dragon dragon, bool left,
                                        out Image fill, out Image trail, out Text hp)
        {
            Anchor anchor = left ? Anchor.TopLeft : Anchor.TopRight;
            float x = left ? 40f : -40f;
            Color theme = Color.Lerp(dragon.themeColor, Color.white, 0.05f);

            Txt("Name", parent, dragon.displayName.ToUpper(), 38, Color.white,
                left ? TextAnchor.UpperLeft : TextAnchor.UpperRight, anchor, V(x, -26f, 0f), new Vector2(700f, 44f));

            var frame = Img("BarFrame", parent, whiteSprite, new Color(0f, 0f, 0f, 0.85f), anchor, V(x, -78f, 0f), new Vector2(640f, 40f));
            var trailImg = Img("Trail", frame.transform, whiteSprite, new Color(1f, 1f, 1f, 0.8f), Anchor.Stretch, Vector3.zero, Vector2.zero);
            var fillImg = Img("Fill", frame.transform, whiteSprite, theme, Anchor.Stretch, Vector3.zero, Vector2.zero);
            Fit((RectTransform)trailImg.transform, 4f);
            Fit((RectTransform)fillImg.transform, 4f);

            foreach (var img in new[] { trailImg, fillImg })
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = left ? 0 : 1;
            }

            var label = Txt("HpText", frame.transform, "200 / 200", 26, Color.white, TextAnchor.MiddleCenter, Anchor.Stretch, Vector3.zero, Vector2.zero);

            fill = fillImg;
            trail = trailImg;
            hp = label;
        }

        private static GameUI BuildUI(Dragon player, Dragon enemy, GameManager gm)
        {
            var uiRoot = new GameObject("UI");
            var canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            uiRoot.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(null);

            var ui = uiRoot.AddComponent<GameUI>();
            ui.player = player;
            ui.enemy = enemy;

            BuildHudBar(uiRoot.transform, player, true, out ui.playerFill, out ui.playerTrail, out ui.playerHp);
            BuildHudBar(uiRoot.transform, enemy, false, out ui.enemyFill, out ui.enemyTrail, out ui.enemyHp);

            // ---- ability bar
            var bar = Rect("AbilityBar", uiRoot.transform, Anchor.BottomCenter, V(0f, 110f, 0f), new Vector2(600f, 160f));
            Sprite[] icons = { iconFire, iconTail, iconFly };
            string[] names = { "FIRE BREATH", "TAIL WHIP", "SKY STRIKE" };
            string[] keys = { "1", "2", "3" };
            ui.slots = new GameUI.AbilitySlot[3];

            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 170f;
                var slot = Rect("Slot" + (i + 1), bar, Anchor.Center, V(x, 10f, 0f), new Vector2(130f, 130f));
                Img("Rim", slot, circleSprite, new Color(1f, 0.85f, 0.4f, 1f), Anchor.Center, Vector3.zero, new Vector2(132f, 132f));
                var icon = Img("Icon", slot, icons[i], Color.white, Anchor.Center, Vector3.zero, new Vector2(124f, 124f));
                icon.raycastTarget = true;
                var iconButton = icon.gameObject.AddComponent<Button>();
                iconButton.targetGraphic = icon;
                iconButton.transition = Selectable.Transition.None;
                iconButton.navigation = new Navigation { mode = Navigation.Mode.None };
                var overlay = Img("Cooldown", slot, circleSprite, new Color(0f, 0f, 0f, 0.7f), Anchor.Center, Vector3.zero, new Vector2(124f, 124f));
                overlay.type = Image.Type.Filled;
                overlay.fillMethod = Image.FillMethod.Radial360;
                overlay.fillOrigin = (int)Image.Origin360.Top;
                overlay.fillClockwise = false;
                overlay.fillAmount = 0f;

                var cd = Txt("CooldownText", slot, "", 46, Color.white, TextAnchor.MiddleCenter, Anchor.Center, Vector3.zero, new Vector2(124f, 60f));

                var badge = Img("KeyBadge", slot, circleSprite, new Color(0.08f, 0.08f, 0.1f, 0.95f), Anchor.Center, V(-50f, -50f, 0f), new Vector2(40f, 40f));
                Txt("Key", badge.transform, keys[i], 28, Color.white, TextAnchor.MiddleCenter, Anchor.Center, Vector3.zero, new Vector2(40f, 40f), false);
                Txt("Name", slot, names[i], 22, Color.white, TextAnchor.MiddleCenter, Anchor.Center, V(0f, -84f, 0f), new Vector2(200f, 30f));

                ui.slots[i] = new GameUI.AbilitySlot { icon = icon, cooldownOverlay = overlay, cooldownText = cd };
            }

            Txt("Controls", uiRoot.transform, "WASD  Move      1 / 2 / 3 or Click Icons  Abilities      R  Restart", 24,
                new Color(1f, 1f, 1f, 0.75f), TextAnchor.LowerLeft, Anchor.BottomLeft, V(30f, 20f, 0f), new Vector2(900f, 36f));

            // ---- countdown text
            ui.centerText = Txt("CenterText", uiRoot.transform, "3", 190, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter,
                                Anchor.Center, V(0f, 80f, 0f), new Vector2(1200f, 300f));
            ui.centerText.gameObject.SetActive(false);

            // ---- winner panel
            var panel = Img("WinnerPanel", uiRoot.transform, whiteSprite, new Color(0f, 0f, 0f, 0.78f), Anchor.Stretch, Vector3.zero, Vector2.zero);
            panel.raycastTarget = true;
            ui.winnerTitle = Txt("Title", panel.transform, "WINNER", 120, Color.white, TextAnchor.MiddleCenter, Anchor.Center, V(0f, 130f, 0f), new Vector2(1600f, 180f));

            var btnImg = Img("RestartButton", panel.transform, whiteSprite, new Color(0.95f, 0.38f, 0.1f), Anchor.Center, V(0f, -110f, 0f), new Vector2(400f, 110f));
            btnImg.raycastTarget = true;
            var button = btnImg.gameObject.AddComponent<Button>();
            button.targetGraphic = btnImg;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.75f, 0.5f);
            colors.pressedColor = new Color(0.7f, 0.25f, 0.05f);
            button.colors = colors;
            Txt("Label", btnImg.transform, "RESTART", 58, Color.white, TextAnchor.MiddleCenter, Anchor.Stretch, Vector3.zero, Vector2.zero);
            UnityEventTools.AddPersistentListener(button.onClick, new UnityAction(gm.Restart));

            ui.winnerPanel = panel.gameObject;
            panel.gameObject.SetActive(false);

            uiRoot.transform.SetAsLastSibling();
            return ui;
        }
    }
}
