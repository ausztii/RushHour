#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-shot editor builder that assembles the playable RushHour scene: the Veridia city block
/// along Route 7, the GreenLine delivery pod + rear-follow camera, the spawner, the obstacle and
/// collectible prefabs, and the HudController-driven HUD.
/// Menu: Tools/RushHour/Build Playable Scene
/// </summary>
public static class RushHourSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/RushHour.unity";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string MaterialFolder = "Assets/Materials";
    private const string SettingsFolder = "Assets/Settings";

    // Road runs from z = -20 to z = 600 so the 500 m delivery always has ground under it.
    private const float RoadCenterZ = 290f;
    private const float RoadLengthScale = 62f;

    // Destination: the Greenfield Community Clinic straddles the end of Route 7, at the far edge
    // of the outer district, five metres past the 500 m mark.
    private const float ClinicZ = 505f;

    // The clinic's stretch of Route 7 has been resurfaced - paler, smoother asphalt with the
    // markings repainted on top of it. This is the road's own "you have made it" cue.
    private const float ResurfacedFromZ = 370f;
    private const float ResurfacedToZ = 600f;
    private const float ResurfacedTopY = 0.03f;
    private const float MarkingY = 0.012f;

    // ------------------------------------------------------------------- palette

    private static readonly Color RoadColor = new Color(0.145f, 0.155f, 0.175f);
    private static readonly Color LaneColor = new Color(0.90f, 0.88f, 0.80f);
    private static readonly Color EdgeLineColor = new Color(0.86f, 0.82f, 0.62f);
    private static readonly Color CurbColor = new Color(0.52f, 0.55f, 0.58f);
    private static readonly Color SidewalkColor = new Color(0.38f, 0.41f, 0.44f);

    // GreenLine Logistics livery: pale grey bodywork, the company's green only on the stripes.
    private static readonly Color PodBodyColor = new Color(0.88f, 0.90f, 0.91f);
    private static readonly Color PodCargoColor = new Color(0.94f, 0.96f, 0.95f);
    private static readonly Color PodGlassColor = new Color(0.09f, 0.13f, 0.17f);
    private static readonly Color PodSkirtColor = new Color(0.10f, 0.11f, 0.13f);
    private static readonly Color PodWheelColor = new Color(0.06f, 0.06f, 0.07f);
    private static readonly Color HeadLightColor = new Color(1.00f, 0.95f, 0.80f);
    private static readonly Color TailLightColor = new Color(0.95f, 0.16f, 0.13f);
    private static readonly Color AccentColor = new Color(0.16f, 0.68f, 0.40f);

    private static readonly Color BarricadeColor = new Color(0.95f, 0.42f, 0.10f);
    private static readonly Color BarricadeStripeColor = new Color(0.93f, 0.93f, 0.90f);
    private static readonly Color BarricadeLegColor = new Color(0.20f, 0.21f, 0.23f);
    private static readonly Color BeaconColor = new Color(1.00f, 0.72f, 0.20f);

    private static readonly Color PotholeColor = new Color(0.055f, 0.060f, 0.075f);
    private static readonly Color PotholeRimColor = new Color(0.22f, 0.23f, 0.25f);
    private static readonly Color DebrisColor = new Color(0.40f, 0.33f, 0.26f);
    private static readonly Color DebrisBlockColor = new Color(0.55f, 0.53f, 0.50f);
    private static readonly Color ConeColor = new Color(0.95f, 0.45f, 0.12f);
    private static readonly Color CongestionColor = new Color(0.28f, 0.36f, 0.55f);
    private static readonly Color CongestionGlassColor = new Color(0.10f, 0.14f, 0.19f);
    private static readonly Color CongestionTrimColor = new Color(0.16f, 0.17f, 0.19f);

    private static readonly Color GrantColor = new Color(0.24f, 0.86f, 1.00f);
    private static readonly Color ChargeGlowColor = new Color(0.35f, 0.95f, 0.48f);

    // The clinic: community-health palette - pale green, white trim, clinical red, warm light.
    private static readonly Color ClinicWallColor = new Color(0.86f, 0.93f, 0.88f);
    private static readonly Color ClinicTrimColor = new Color(0.95f, 0.96f, 0.95f);
    private static readonly Color ClinicRoofColor = new Color(0.62f, 0.68f, 0.64f);
    private static readonly Color ClinicSoffitColor = new Color(0.95f, 0.88f, 0.76f);
    private static readonly Color ClinicSignColor = new Color(0.97f, 0.97f, 0.96f);
    private static readonly Color ClinicCrossColor = new Color(0.86f, 0.16f, 0.14f);
    private static readonly Color ClinicWindowColor = new Color(1.00f, 0.86f, 0.62f);
    private static readonly Color ClinicLampColor = new Color(1.00f, 0.90f, 0.72f);
    private static readonly Color ClinicRoadColor = new Color(0.24f, 0.25f, 0.27f);

    // The people waiting outside, in warm community colours rather than city workwear.
    private static readonly Color NpcSkinColor = new Color(0.72f, 0.58f, 0.46f);
    private static readonly Color NpcCoatWarmColor = new Color(0.85f, 0.55f, 0.35f);
    private static readonly Color NpcCoatCoolColor = new Color(0.55f, 0.68f, 0.80f);

    private static readonly Color[] BuildingColors =
    {
        new Color(0.34f, 0.38f, 0.44f),
        new Color(0.27f, 0.34f, 0.40f),
        new Color(0.41f, 0.39f, 0.36f),
        new Color(0.23f, 0.29f, 0.37f),
        new Color(0.44f, 0.46f, 0.47f),
        new Color(0.29f, 0.40f, 0.42f),
    };

    public static string Execute()
    {
        // --- 0. Safety: don't silently discard unsaved scene edits ---
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return "CANCELLED: unsaved scene changes were not saved.";
        }

        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);
        EnsureTag("Obstacle");

        // The fuel-era assets have no place in the Veridia build.
        DeleteStaleAssets();

        // --- 1. Materials ---
        Material roadMat = CreateLitMaterial("Road", RoadColor, 0.08f);
        Material laneMat = CreateLitMaterial("LaneMarking", LaneColor, 0.25f);
        Material edgeMat = CreateLitMaterial("EdgeLine", EdgeLineColor, 0.20f);
        Material curbMat = CreateLitMaterial("Curb", CurbColor, 0.10f);
        Material sidewalkMat = CreateLitMaterial("Sidewalk", SidewalkColor, 0.05f);

        Material podBodyMat = CreateLitMaterial("Pod_Body", PodBodyColor, 0.58f, 0.15f);
        Material podCargoMat = CreateLitMaterial("Pod_Cargo", PodCargoColor, 0.42f, 0.05f);
        Material podGlassMat = CreateLitMaterial("Pod_Glass", PodGlassColor, 0.90f, 0.35f);
        Material podSkirtMat = CreateLitMaterial("Pod_Skirt", PodSkirtColor, 0.18f);
        Material podWheelMat = CreateLitMaterial("Pod_Wheel", PodWheelColor, 0.22f);
        Material headLightMat = CreateLitMaterial("Pod_HeadLight", HeadLightColor, 0.60f, 0.05f, HeadLightColor, 2.2f);
        Material tailLightMat = CreateLitMaterial("Pod_TailLight", TailLightColor, 0.60f, 0.05f, TailLightColor, 2.2f);
        Material accentMat = CreateLitMaterial("Pod_Accent", AccentColor, 0.55f, 0.05f);
        Material chargeStripMat = CreateLitMaterial("Pod_ChargeStrip", ChargeGlowColor, 0.55f, 0.05f, ChargeGlowColor, 2.6f);
        Material cargoSealMat = CreateLitMaterial("Pod_CargoSeal",
            new Color(0.45f, 1.00f, 0.72f), 0.50f, 0.05f, new Color(0.45f, 1.00f, 0.72f), 1.6f);

        Material barricadeMat = CreateLitMaterial("Obstacle_Barricade", BarricadeColor, 0.15f);
        Material stripeMat = CreateLitMaterial("Obstacle_BarricadeStripe", BarricadeStripeColor, 0.25f);
        Material legMat = CreateLitMaterial("Obstacle_BarricadeLeg", BarricadeLegColor, 0.10f);
        Material beaconMat = CreateLitMaterial("Obstacle_Beacon", BeaconColor, 0.55f, 0.05f, BeaconColor, 2.0f);

        Material potholeMat = CreateLitMaterial("Obstacle_Pothole", PotholeColor, 0.02f);
        Material potholeRimMat = CreateLitMaterial("Obstacle_PotholeRim", PotholeRimColor, 0.05f);
        Material debrisMat = CreateLitMaterial("Obstacle_Debris", DebrisColor, 0.05f);
        Material debrisBlockMat = CreateLitMaterial("Obstacle_DebrisBlock", DebrisBlockColor, 0.08f);
        Material coneMat = CreateLitMaterial("Obstacle_Cone", ConeColor, 0.20f);
        Material congestionMat = CreateLitMaterial("Obstacle_Congestion", CongestionColor, 0.55f, 0.20f);
        Material congestionGlassMat = CreateLitMaterial("Obstacle_CongestionGlass", CongestionGlassColor, 0.85f, 0.30f);
        Material congestionTrimMat = CreateLitMaterial("Obstacle_CongestionTrim", CongestionTrimColor, 0.20f);
        Material congestionTailMat = CreateLitMaterial("Obstacle_CongestionTail", TailLightColor, 0.60f, 0.05f, TailLightColor, 2.0f);

        Material grantMat = CreateLitMaterial("ShieldPickup", GrantColor, 0.70f, 0.05f, GrantColor, 2.6f);
        Material chargeMat = CreateLitMaterial("ChargePickup", ChargeGlowColor, 0.70f, 0.05f, ChargeGlowColor, 2.6f);
        Material grantHaloMat = CreateTransparentGlowMaterial("GrantHalo", new Color(0.24f, 0.86f, 1f, 0.22f));
        Material chargeHaloMat = CreateTransparentGlowMaterial("ChargeHalo", new Color(0.35f, 0.95f, 0.48f, 0.22f));
        Material bubbleMat = CreateTransparentGlowMaterial("ShieldBubble", new Color(0.24f, 0.86f, 1f, 0.26f));

        Material[] buildingMats = new Material[BuildingColors.Length];
        for (int i = 0; i < BuildingColors.Length; i++)
        {
            buildingMats[i] = CreateLitMaterial("Building_" + i, BuildingColors[i], 0.12f);
        }
        Material crownMat = CreateLitMaterial("Building_Crown", new Color(0.50f, 0.53f, 0.55f), 0.30f);

        Material clinicWallMat = CreateLitMaterial("Clinic_Wall", ClinicWallColor, 0.25f);
        Material clinicTrimMat = CreateLitMaterial("Clinic_Trim", ClinicTrimColor, 0.30f);
        Material clinicRoofMat = CreateLitMaterial("Clinic_Roof", ClinicRoofColor, 0.18f);
        Material clinicSoffitMat = CreateLitMaterial("Clinic_Soffit", ClinicSoffitColor, 0.35f);
        Material clinicSignMat = CreateLitMaterial("Clinic_Sign", ClinicSignColor, 0.35f);
        Material clinicCrossMat = CreateLitMaterial("Clinic_Cross", ClinicCrossColor, 0.40f, 0.05f, ClinicCrossColor, 0.7f);
        Material clinicWindowMat = CreateLitMaterial("Clinic_Window", ClinicWindowColor, 0.60f, 0.05f, ClinicWindowColor, 2.0f);
        Material clinicLampMat = CreateLitMaterial("Clinic_Lamp", ClinicLampColor, 0.50f, 0.05f, ClinicLampColor, 3.0f);
        Material clinicRoadMat = CreateLitMaterial("Clinic_Road", ClinicRoadColor, 0.30f);
        Material npcSkinMat = CreateLitMaterial("NPC_Skin", NpcSkinColor, 0.20f);
        Material npcCoatWarmMat = CreateLitMaterial("NPC_CoatWarm", NpcCoatWarmColor, 0.25f);
        Material npcCoatCoolMat = CreateLitMaterial("NPC_CoatCool", NpcCoatCoolColor, 0.25f);

        // --- 2. Fresh scene ---
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- 3. Atmosphere: hazy overcast city ---
        ConfigureAtmosphere();

        // --- 4. Road, city, light ---
        BuildRoad(roadMat, laneMat, edgeMat, curbMat, sidewalkMat, clinicRoadMat);
        BuildCity(buildingMats, crownMat, sidewalkMat);
        Light dirLight = BuildSun();

        // --- 5. Pod, battery, shield, camera ---
        GameObject car = null;
        GameObject bubble = null;
        CarController carController = null;
        BatterySystem battery = null;
        ShieldSystem shieldSystem = null;
        CameraShake cameraShake = null;
        BuildPod(podBodyMat, podCargoMat, podGlassMat, podSkirtMat, podWheelMat,
            headLightMat, tailLightMat, accentMat, chargeStripMat, cargoSealMat, bubbleMat,
            out car, out bubble, out carController, out battery, out shieldSystem, out cameraShake);

        // --- 6. GameManager & spawner ---
        GameObject gmGo = new GameObject("GameManager");
        GameManager gameManager = gmGo.AddComponent<GameManager>();

        GameObject spawnerGo = new GameObject("Obstacle_Spawner");
        spawnerGo.transform.position = new Vector3(0f, 0f, 50f);
        ObstacleSpawner spawner = spawnerGo.AddComponent<ObstacleSpawner>();

        // --- 6b. Destination: the Greenfield Community Clinic, and the trigger that ends the run ---
        BuildClinic(clinicWallMat, clinicTrimMat, clinicRoofMat, clinicSoffitMat, clinicSignMat,
            clinicCrossMat, clinicWindowMat, clinicLampMat, npcSkinMat, npcCoatWarmMat, npcCoatCoolMat,
            gameManager);

        // --- 7. Prefabs ---
        GameObject barrierPrefab = BuildObstaclePrefab("Obstacle_Barrier",
            new Vector3(1.8f, 1.0f, 0.6f), Vector3.zero,
            new[]
            {
                new PartDef("Leg_L", PrimitiveType.Cube, new Vector3(-0.72f, -0.22f, 0f), new Vector3(0.13f, 0.56f, 0.42f), legMat),
                new PartDef("Leg_R", PrimitiveType.Cube, new Vector3(0.72f, -0.22f, 0f), new Vector3(0.13f, 0.56f, 0.42f), legMat),
                new PartDef("Foot_L", PrimitiveType.Cube, new Vector3(-0.72f, -0.46f, 0f), new Vector3(0.22f, 0.08f, 0.52f), legMat),
                new PartDef("Foot_R", PrimitiveType.Cube, new Vector3(0.72f, -0.46f, 0f), new Vector3(0.22f, 0.08f, 0.52f), legMat),
                new PartDef("Plank", PrimitiveType.Cube, new Vector3(0f, 0.26f, 0f), new Vector3(1.72f, 0.30f, 0.14f), barricadeMat),
                new PartDef("Stripe_A", PrimitiveType.Cube, new Vector3(-0.56f, 0.26f, 0f), new Vector3(0.26f, 0.32f, 0.16f), stripeMat),
                new PartDef("Stripe_B", PrimitiveType.Cube, new Vector3(0f, 0.26f, 0f), new Vector3(0.26f, 0.32f, 0.16f), stripeMat),
                new PartDef("Stripe_C", PrimitiveType.Cube, new Vector3(0.56f, 0.26f, 0f), new Vector3(0.26f, 0.32f, 0.16f), stripeMat),
                new PartDef("Beacon", PrimitiveType.Cube, new Vector3(0f, 0.46f, 0f), new Vector3(0.34f, 0.12f, 0.18f), beaconMat),
            });

        // A pothole is deferred road maintenance: a charge tax, not a wall.
        GameObject potholePrefab = BuildObstaclePrefab("Obstacle_Pothole",
            new Vector3(1.8f, 0.2f, 1.4f), new Vector3(0f, -0.42f, 0f),
            new[]
            {
                new PartDef("Patch", PrimitiveType.Cube, new Vector3(0f, -0.47f, 0f), new Vector3(1.52f, 0.05f, 1.12f), potholeMat),
                new PartDef("Rim_F", PrimitiveType.Cube, new Vector3(0f, -0.44f, 0.66f), new Vector3(1.70f, 0.09f, 0.16f), potholeRimMat),
                new PartDef("Rim_B", PrimitiveType.Cube, new Vector3(0f, -0.44f, -0.66f), new Vector3(1.70f, 0.09f, 0.16f), potholeRimMat),
                new PartDef("Rim_L", PrimitiveType.Cube, new Vector3(-0.80f, -0.44f, 0f), new Vector3(0.16f, 0.09f, 1.16f), potholeRimMat),
                new PartDef("Rim_R", PrimitiveType.Cube, new Vector3(0.80f, -0.44f, 0f), new Vector3(0.16f, 0.09f, 1.16f), potholeRimMat),
                new PartDef("Gravel", PrimitiveType.Cube, new Vector3(0.34f, -0.45f, -0.28f), new Vector3(0.30f, 0.05f, 0.24f), new Vector3(0f, 22f, 0f), potholeRimMat),
            },
            Obstacle.HazardKind.ChargePenalty, 12f);

        GameObject debrisPrefab = BuildObstaclePrefab("Obstacle_Debris",
            new Vector3(1.5f, 0.9f, 1.5f), Vector3.zero,
            new[]
            {
                new PartDef("Slab", PrimitiveType.Cube, new Vector3(0f, -0.42f, 0f), new Vector3(1.24f, 0.14f, 0.94f), new Vector3(0f, 16f, 0f), debrisMat),
                new PartDef("Block_A", PrimitiveType.Cube, new Vector3(-0.32f, -0.12f, 0.10f), new Vector3(0.52f, 0.44f, 0.50f), new Vector3(0f, 30f, 0f), debrisBlockMat),
                new PartDef("Block_B", PrimitiveType.Cube, new Vector3(0.36f, -0.16f, -0.08f), new Vector3(0.44f, 0.36f, 0.42f), new Vector3(0f, -26f, 0f), debrisBlockMat),
                new PartDef("Block_C", PrimitiveType.Cube, new Vector3(0.06f, 0.24f, 0.14f), new Vector3(0.32f, 0.32f, 0.32f), new Vector3(0f, 12f, 18f), debrisBlockMat),
                new PartDef("Cone_Base", PrimitiveType.Cube, new Vector3(0.54f, -0.42f, -0.46f), new Vector3(0.36f, 0.06f, 0.36f), coneMat),
                new PartDef("Cone_Body", PrimitiveType.Cylinder, new Vector3(0.54f, -0.24f, -0.46f), new Vector3(0.26f, 0.18f, 0.26f), coneMat),
            });

        GameObject congestionPrefab = BuildObstaclePrefab("Obstacle_Congestion",
            new Vector3(1.7f, 1.0f, 3.2f), Vector3.zero,
            new[]
            {
                new PartDef("Van_Body", PrimitiveType.Cube, new Vector3(0f, -0.10f, 0f), new Vector3(1.62f, 0.66f, 3.00f), congestionMat),
                new PartDef("Van_Hood", PrimitiveType.Cube, new Vector3(0f, 0.02f, 1.16f), new Vector3(1.46f, 0.34f, 0.72f), congestionMat),
                new PartDef("Van_Roof", PrimitiveType.Cube, new Vector3(0f, 0.36f, -0.35f), new Vector3(1.38f, 0.36f, 1.80f), congestionGlassMat),
                new PartDef("Van_Bumper", PrimitiveType.Cube, new Vector3(0f, -0.34f, -1.52f), new Vector3(1.64f, 0.20f, 0.14f), congestionTrimMat),
                new PartDef("Tail_L", PrimitiveType.Cube, new Vector3(-0.54f, -0.06f, -1.52f), new Vector3(0.32f, 0.14f, 0.10f), congestionTailMat),
                new PartDef("Tail_R", PrimitiveType.Cube, new Vector3(0.54f, -0.06f, -1.52f), new Vector3(0.32f, 0.14f, 0.10f), congestionTailMat),
                new PartDef("Wheel_FL", PrimitiveType.Cylinder, new Vector3(-0.82f, -0.24f, 0.98f), new Vector3(0.48f, 0.09f, 0.48f), new Vector3(0f, 0f, 90f), congestionTrimMat),
                new PartDef("Wheel_FR", PrimitiveType.Cylinder, new Vector3(0.82f, -0.24f, 0.98f), new Vector3(0.48f, 0.09f, 0.48f), new Vector3(0f, 0f, 90f), congestionTrimMat),
                new PartDef("Wheel_RL", PrimitiveType.Cylinder, new Vector3(-0.82f, -0.24f, -0.98f), new Vector3(0.48f, 0.09f, 0.48f), new Vector3(0f, 0f, 90f), congestionTrimMat),
                new PartDef("Wheel_RR", PrimitiveType.Cylinder, new Vector3(0.82f, -0.24f, -0.98f), new Vector3(0.48f, 0.09f, 0.48f), new Vector3(0f, 0f, 90f), congestionTrimMat),
            });

        GameObject shieldPrefab = BuildShieldPickupPrefab(grantMat, grantHaloMat);
        GameObject chargePrefab = BuildChargePickupPrefab(chargeMat, chargeHaloMat);

        // --- 8. HUD (the single owner of everything the player reads) ---
        HudController hudController = BuildHud(gameManager, battery, out HudRefs hud);

        // --- 9. Wiring ---
        carController.battery = battery;
        carController.gameManager = gameManager;
        carController.visualRoot = car.transform.Find("Visual");

        battery.gameManager = gameManager;
        SerializedObject batterySo = new SerializedObject(battery);
        SetRef(batterySo, "hud", hudController);
        batterySo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject shieldSo = new SerializedObject(shieldSystem);
        SetRef(shieldSo, "hud", hudController);
        SetRef(shieldSo, "cameraShake", cameraShake);
        shieldSo.ApplyModifiedPropertiesWithoutUndo();

        gameManager.car = car.transform;
        gameManager.autoRestartOnFailure = false; // hold the result card until the player restarts

        SerializedObject gmSo = new SerializedObject(gameManager);
        SetRef(gmSo, "hud", hudController);
        SetRef(gmSo, "cameraShake", cameraShake);
        gmSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject spawnerSo = new SerializedObject(spawner);
        SerializedProperty obstacleArray = spawnerSo.FindProperty("obstaclePrefabs");
        GameObject[] obstaclePrefabs = { barrierPrefab, potholePrefab, debrisPrefab, congestionPrefab };
        obstacleArray.arraySize = obstaclePrefabs.Length;
        for (int i = 0; i < obstaclePrefabs.Length; i++)
        {
            obstacleArray.GetArrayElementAtIndex(i).objectReferenceValue = obstaclePrefabs[i];
        }
        spawnerSo.FindProperty("carController").objectReferenceValue = carController;
        spawnerSo.FindProperty("gameManager").objectReferenceValue = gameManager;
        spawnerSo.FindProperty("shieldPickupPrefab").objectReferenceValue = shieldPrefab;
        spawnerSo.FindProperty("chargePickupPrefab").objectReferenceValue = chargePrefab;
        spawnerSo.ApplyModifiedPropertiesWithoutUndo();

        // --- 10. Save ---
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return string.Format(
            "RushHour (Veridia) scene built at {0}. Road + dashed lane markings + curbs + sidewalks + {1} city blocks + fog + sun, " +
            "with the last stretch resurfaced and the skyline thinning out on the clinic approach. " +
            "Car (Player) = narrow GreenLine courier pod: CarController + BatterySystem + ShieldSystem + PodSignals + CameraShake, " +
            "with a bankable Visual rig, light strips, a diegetic pack strip and a breathing cargo seal. " +
            "GameManager (hud + cameraShake), Obstacle_Spawner (potholes stop once the road is maintained). " +
            "Destination: the Greenfield Community Clinic at z = {2} - single-storey building, covered drop-off bay, " +
            "warm porch light, patient queue, and a ClinicTrigger that completes the delivery on arrival. " +
            "Prefabs: {3} hazards (Barrier/Pothole/Debris/Congestion; Pothole costs charge only) + ShieldPickup + ChargePickup. " +
            "HUD: pack charge meter + route/distance/district readout + route banner + result card + restart button + impact flash + EventSystem. " +
            "Recharge points restore {4}% and swerving costs {5}%.",
            ScenePath, BuildingColors.Length, ClinicZ, obstaclePrefabs.Length, 30f, 1f);
    }

    [MenuItem("Tools/RushHour/Build Playable Scene", false, 10)]
    public static void BuildFromMenu()
    {
        string result = Execute();
        Debug.Log("<color=green>RushHour builder:</color> " + result);
    }

    // =============================================================== atmosphere

    /// <summary>
    /// Hazy overcast city air. Shared by the playable scene and the start screen, so the menu and
    /// the run are unmistakably the same road.
    /// </summary>
    public static void ConfigureAtmosphere()
    {
        Material skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        if (skybox != null) RenderSettings.skybox = skybox;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0045f;
        RenderSettings.fogColor = new Color(0.60f, 0.66f, 0.74f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.52f);

        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.6f;
    }

    /// <summary>
    /// Neutral daylight, high enough to keep the street readable. Shared by the playable scene and
    /// the start screen.
    /// </summary>
    public static Light BuildSun()
    {
        GameObject lightGo = new GameObject("Directional Light");
        Light dirLight = lightGo.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(1f, 0.97f, 0.92f);
        dirLight.intensity = 1.05f;
        dirLight.shadows = LightShadows.Soft;
        dirLight.shadowStrength = 0.75f;
        lightGo.transform.position = new Vector3(0f, 20f, 0f);
        lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        RenderSettings.sun = dirLight;
        return dirLight;
    }

    // ===================================================================== road

    private static void BuildRoad(Material roadMat, Material laneMat, Material edgeMat,
        Material curbMat, Material sidewalkMat, Material resurfacedMat)
    {
        GameObject road = CreatePrimitive("Road", PrimitiveType.Plane,
            new Vector3(0f, 0f, RoadCenterZ),
            new Vector3(1.2f, 1f, RoadLengthScale),
            roadMat, null, false);
        MarkStatic(road);

        // The clinic approach has been resurfaced: a paler, smoother slab laid over the worn
        // asphalt. Markings on this stretch are painted on top of it, hence the raised height.
        MarkStatic(CreatePrimitive("Road_Resurfaced", PrimitiveType.Cube,
            new Vector3(0f, ResurfacedTopY * 0.5f, (ResurfacedFromZ + ResurfacedToZ) * 0.5f),
            new Vector3(8.4f, ResurfacedTopY, ResurfacedToZ - ResurfacedFromZ),
            resurfacedMat, null, false));

        // Solid edge lines marking the limits of the drivable surface, laid in two runs so they
        // sit on the worn asphalt and then on top of the resurfaced slab.
        float roadFrom = RoadCenterZ - (RoadLengthScale * 5f);
        float roadTo = RoadCenterZ + (RoadLengthScale * 5f);

        foreach (float x in new[] { -3.85f, 3.85f })
        {
            string label = x < 0f ? "EdgeLine_Left" : "EdgeLine_Right";

            MarkStatic(CreatePrimitive(label + "_Worn", PrimitiveType.Cube,
                new Vector3(x, MarkingY, (roadFrom + ResurfacedFromZ) * 0.5f),
                new Vector3(0.13f, 0.02f, ResurfacedFromZ - roadFrom),
                edgeMat, null, false));

            MarkStatic(CreatePrimitive(label + "_Fresh", PrimitiveType.Cube,
                new Vector3(x, ResurfacedTopY + 0.01f, (ResurfacedFromZ + ResurfacedToZ) * 0.5f),
                new Vector3(0.13f, 0.02f, ResurfacedToZ - ResurfacedFromZ),
                edgeMat, null, false));
        }

        // Dashed lane dividers: 4.5 m dash, 5.5 m gap.
        const float cycle = 10f;
        const float dashLength = 4.5f;
        int steps = Mathf.FloorToInt((roadTo - roadFrom) / cycle);

        foreach (float x in new[] { -1.25f, 1.25f })
        {
            for (int i = 0; i <= steps; i++)
            {
                float z = roadFrom + (i * cycle);

                // Repainted on the resurfaced slab, worn off before it.
                float y = z >= ResurfacedFromZ ? ResurfacedTopY + 0.01f : MarkingY;

                MarkStatic(CreatePrimitive("LaneDash", PrimitiveType.Cube,
                    new Vector3(x, y, z), new Vector3(0.13f, 0.02f, dashLength),
                    laneMat, null, false));
            }
        }

        // Kerbs and pavements either side of the road.
        for (int side = -1; side <= 1; side += 2)
        {
            string label = side < 0 ? "Left" : "Right";
            MarkStatic(CreatePrimitive("Curb_" + label, PrimitiveType.Cube,
                new Vector3(side * 4.25f, 0.14f, RoadCenterZ), new Vector3(0.5f, 0.28f, RoadLengthScale),
                curbMat, null, false));

            MarkStatic(CreatePrimitive("Sidewalk_" + label, PrimitiveType.Cube,
                new Vector3(side * 5.5f, 0.10f, RoadCenterZ), new Vector3(2.5f, 0.20f, RoadLengthScale),
                sidewalkMat, null, false));
        }
    }

    // ===================================================================== city

    private static void BuildCity(Material[] buildingMats, Material crownMat, Material sidewalkMat)
    {
        GameObject cityRoot = new GameObject("City");

        // Deterministic skyline: rebuilding the scene reproduces the same Veridia block.
        UnityEngine.Random.InitState(20240617);

        const float fromZ = -30f;
        const float toZ = 620f;
        const float spacing = 26f;

        int steps = Mathf.FloorToInt((toZ - fromZ) / spacing);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i <= steps; i++)
            {
                float depth = UnityEngine.Random.Range(9f, 16f);
                float width = UnityEngine.Random.Range(7f, 12f);
                float height = UnityEngine.Random.Range(9f, 34f);
                float z = fromZ + (i * spacing) + UnityEngine.Random.Range(-4f, 4f);
                float x = side * (7.0f + depth * 0.5f);

                // The outer district thins out towards the clinic: the skyline stops at the
                // destination, the plot beside it is left clear for the building and its forecourt,
                // and the last blocks before arrival are low and sparse.
                float approach = Mathf.InverseLerp(380f, ClinicZ, z);

                if (z > ClinicZ + 45f) continue;
                if (side < 0 && z > ClinicZ - 30f && z < ClinicZ + 30f) continue;
                if (approach > 0.25f && UnityEngine.Random.value < Mathf.Lerp(0f, 0.85f, approach)) continue;

                height = Mathf.Lerp(height, UnityEngine.Random.Range(4.5f, 8.5f), approach);

                Material mat = buildingMats[UnityEngine.Random.Range(0, buildingMats.Length)];
                string label = (side < 0 ? "W" : "E") + i.ToString("00");

                GameObject block = CreatePrimitive("Building_" + label, PrimitiveType.Cube,
                    new Vector3(x, height * 0.5f, z), new Vector3(width, height, depth),
                    mat, cityRoot.transform, false);
                MarkStatic(block);

                // A narrower crown stops the skyline reading as one flat row of boxes.
                GameObject crown = CreatePrimitive("BuildingCrown_" + label, PrimitiveType.Cube,
                    new Vector3(x, height + 0.6f, z),
                    new Vector3(width * 0.55f, 1.2f, depth * 0.55f),
                    crownMat, cityRoot.transform, false);
                MarkStatic(crown);
            }
        }

        // Cross-street strips joining the pavements into the city so the block reads as a district.
        for (int side = -1; side <= 1; side += 2)
        {
            MarkStatic(CreatePrimitive(side < 0 ? "Forecourt_Left" : "Forecourt_Right", PrimitiveType.Cube,
                new Vector3(side * 6.9f, 0.05f, RoadCenterZ), new Vector3(4.0f, 0.10f, RoadLengthScale),
                sidewalkMat, cityRoot.transform, false));
        }
    }

    // =================================================================== clinic

    /// <summary>
    /// The destination: a single-storey community clinic set back from the road, with a covered
    /// drop-off bay spanning it, warm porch light, the queue outside, and the arrival trigger that
    /// completes the delivery. Built from primitives like the rest of the world, so nothing here
    /// depends on an imported mesh.
    /// </summary>
    private static ClinicTrigger BuildClinic(Material wallMat, Material trimMat, Material roofMat,
        Material soffitMat, Material signMat, Material crossMat, Material windowMat, Material lampMat,
        Material npcSkinMat, Material npcCoatWarmMat, Material npcCoatCoolMat, GameManager gameManager)
    {
        GameObject root = new GameObject("Clinic");
        root.transform.position = new Vector3(0f, 0f, ClinicZ);

        Transform t = root.transform;

        // --- the building, on the far side of the far pavement, facade facing the road ---
        MarkStatic(CreatePrimitive("Clinic_Plinth", PrimitiveType.Cube,
            new Vector3(-13.4f, 0.35f, 0f), new Vector3(12.1f, 0.70f, 10.1f), trimMat, t, false));

        MarkStatic(CreatePrimitive("Clinic_Wall", PrimitiveType.Cube,
            new Vector3(-13.4f, 1.90f, 0f), new Vector3(12.0f, 3.10f, 10.0f), wallMat, t, false));

        MarkStatic(CreatePrimitive("Clinic_Roof", PrimitiveType.Cube,
            new Vector3(-13.4f, 3.60f, 0f), new Vector3(12.8f, 0.30f, 10.8f), roofMat, t, false));

        // Entrance: a door and warm-lit windows along the facade at x = -7.4.
        MarkStatic(CreatePrimitive("Clinic_Door", PrimitiveType.Cube,
            new Vector3(-7.34f, 1.15f, 0f), new Vector3(0.16f, 2.30f, 1.70f), trimMat, t, false));

        float[] windowZ = { -3.30f, -1.70f, 1.70f, 3.30f };
        for (int i = 0; i < windowZ.Length; i++)
        {
            MarkStatic(CreatePrimitive("Clinic_Window_" + i, PrimitiveType.Cube,
                new Vector3(-7.34f, 2.05f, windowZ[i]), new Vector3(0.10f, 1.00f, 1.20f),
                windowMat, t, false));
        }

        // Signage: a white board over the door carrying the health cross.
        MarkStatic(CreatePrimitive("Clinic_Sign", PrimitiveType.Cube,
            new Vector3(-7.30f, 2.95f, 0f), new Vector3(0.14f, 1.10f, 4.60f), signMat, t, false));

        MarkStatic(CreatePrimitive("Clinic_Cross_V", PrimitiveType.Cube,
            new Vector3(-7.22f, 2.95f, -1.45f), new Vector3(0.06f, 0.70f, 0.22f), crossMat, t, false));

        MarkStatic(CreatePrimitive("Clinic_Cross_H", PrimitiveType.Cube,
            new Vector3(-7.22f, 2.95f, -1.45f), new Vector3(0.06f, 0.22f, 0.70f), crossMat, t, false));

        // --- the covered drop-off bay spanning the road: the finish line the player can see ---
        for (int side = -1; side <= 1; side += 2)
        {
            for (int end = -1; end <= 1; end += 2)
            {
                MarkStatic(CreatePrimitive(side < 0 ? "Clinic_Post_L" : "Clinic_Post_R", PrimitiveType.Cube,
                    new Vector3(side * 5.0f, 2.05f, end * 5.0f), new Vector3(0.26f, 3.70f, 0.26f),
                    trimMat, t, false));
            }
        }

        MarkStatic(CreatePrimitive("Clinic_Canopy", PrimitiveType.Cube,
            new Vector3(-1.2f, 4.04f, 0f), new Vector3(13.6f, 0.28f, 11.6f), roofMat, t, false));

        MarkStatic(CreatePrimitive("Clinic_Soffit", PrimitiveType.Cube,
            new Vector3(-1.2f, 3.88f, 0f), new Vector3(13.4f, 0.06f, 11.4f), soffitMat, t, false));

        // The painted arrival line, on the resurfaced surface and above the repainted markings.
        MarkStatic(CreatePrimitive("Clinic_Threshold", PrimitiveType.Cube,
            new Vector3(0f, ResurfacedTopY + 0.03f, -5.6f), new Vector3(8.4f, 0.02f, 0.8f),
            trimMat, t, false));

        // --- warm porch light: the one thing the rest of Route 7 never had ---
        Color porch = new Color(1.00f, 0.84f, 0.62f);
        WarmLamp(t, "Lamp_Bay_Left", new Vector3(-3.6f, 3.74f, 0f), porch, 2.6f, 16f, lampMat);
        WarmLamp(t, "Lamp_Bay_Right", new Vector3(3.6f, 3.74f, 0f), porch, 2.6f, 16f, lampMat);
        WarmLamp(t, "Lamp_Entrance", new Vector3(-6.4f, 3.10f, 0f), porch, 2.0f, 12f, lampMat);

        // --- the queue outside, because the need is not abstract ---
        float[] queueZ = { -3.40f, -1.90f, -0.40f, 1.10f, 2.60f };

        for (int i = 0; i < queueZ.Length; i++)
        {
            Material coat = (i % 2 == 0) ? npcCoatWarmMat : npcCoatCoolMat;
            float turn = (i % 2 == 0) ? 0f : 14f;

            Transform figure = new GameObject("Clinic_Patient_" + i).transform;
            figure.SetParent(t, false);
            figure.localPosition = new Vector3(-6.25f, 0f, queueZ[i]);
            figure.localEulerAngles = new Vector3(0f, turn, 0f);

            AddPart(figure, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.62f, 0f),
                new Vector3(0.38f, 0.62f, 0.38f), Vector3.zero, coat);

            AddPart(figure, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.39f, 0f),
                new Vector3(0.30f, 0.30f, 0.30f), Vector3.zero, npcSkinMat);
        }

        // --- the arrival volume: entering the bay completes the delivery ---
        GameObject volume = new GameObject("Clinic_Trigger");
        volume.transform.SetParent(t, false);

        BoxCollider box = volume.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(10.0f, 4.0f, 10.6f);
        box.center = new Vector3(0f, 2.0f, 0f);

        Rigidbody rb = volume.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        ClinicTrigger trigger = volume.AddComponent<ClinicTrigger>();
        trigger.gameManager = gameManager;
        return trigger;
    }

    /// <summary>A warm porch light, with its housing so the source itself is visible.</summary>
    private static void WarmLamp(Transform parent, string name, Vector3 localPosition, Color color,
        float intensity, float range, Material housingMat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;

        AddPart(go.transform, name + "_Housing", PrimitiveType.Cube, Vector3.zero,
            new Vector3(0.34f, 0.10f, 0.34f), Vector3.zero, housingMat);
    }

    // ====================================================================== pod

    private static void BuildPod(Material bodyMat, Material cargoMat, Material glassMat,
        Material skirtMat, Material wheelMat, Material headLightMat, Material tailLightMat,
        Material accentMat, Material chargeStripMat, Material cargoSealMat, Material bubbleMat,
        out GameObject car, out GameObject bubble, out CarController carController,
        out BatterySystem battery, out ShieldSystem shieldSystem, out CameraShake cameraShake)
    {
        // A narrow single-occupant courier pod, not a car: it fits the bike lane the city was never
        // built around. Root stays at y = 0 so the collider lines up with the visual, and it never rolls.
        car = new GameObject("Car");
        car.transform.position = Vector3.zero;
        try { car.tag = "Player"; } catch { }

        BoxCollider carCol = car.AddComponent<BoxCollider>();
        carCol.size = new Vector3(1.34f, 1.05f, 3.40f);
        carCol.center = new Vector3(0f, 0.52f, 0f);
        carCol.isTrigger = false;

        Rigidbody carRb = car.AddComponent<Rigidbody>();
        carRb.isKinematic = true;
        carRb.useGravity = false;

        // Everything the player sees lives under "Visual".
        // Banking tilts this, so the car root - and the camera parented to it - stays level.
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(car.transform, false);

        AddPart(visual.transform, "Skirt", PrimitiveType.Cube,
            new Vector3(0f, 0.24f, 0f), new Vector3(1.26f, 0.30f, 3.28f), Vector3.zero, skirtMat);

        AddPart(visual.transform, "Body", PrimitiveType.Cube,
            new Vector3(0f, 0.64f, 0f), new Vector3(1.20f, 0.52f, 3.18f), Vector3.zero, bodyMat);

        // Rider cabin at the front (+Z), the sealed temperature-controlled shipment box behind it.
        AddPart(visual.transform, "Cabin", PrimitiveType.Cube,
            new Vector3(0f, 1.04f, 0.74f), new Vector3(1.04f, 0.50f, 1.34f), Vector3.zero, glassMat);

        AddPart(visual.transform, "Cargo", PrimitiveType.Cube,
            new Vector3(0f, 1.03f, -0.86f), new Vector3(1.10f, 0.62f, 1.46f), Vector3.zero, cargoMat);

        AddPart(visual.transform, "CargoLid", PrimitiveType.Cube,
            new Vector3(0f, 1.36f, -0.86f), new Vector3(1.14f, 0.06f, 1.50f), Vector3.zero, skirtMat);

        // GreenLine livery: a green stripe down each flank, the leaf mark on the cargo box.
        AddPart(visual.transform, "AccentStripe", PrimitiveType.Cube,
            new Vector3(0f, 0.42f, 0f), new Vector3(1.24f, 0.09f, 3.20f), Vector3.zero, accentMat);

        AddPart(visual.transform, "Logo_Leaf_L", PrimitiveType.Cube,
            new Vector3(-0.57f, 0.84f, -0.86f), new Vector3(0.20f, 0.20f, 0.20f), new Vector3(0f, 0f, 45f), accentMat);
        AddPart(visual.transform, "Logo_Leaf_R", PrimitiveType.Cube,
            new Vector3(0.57f, 0.84f, -0.86f), new Vector3(0.20f, 0.20f, 0.20f), new Vector3(0f, 0f, 45f), accentMat);

        // A small EV plate on the nose, rather than a badge worth of chrome.
        AddPart(visual.transform, "Badge_EV", PrimitiveType.Cube,
            new Vector3(0f, 0.82f, 1.61f), new Vector3(0.32f, 0.15f, 0.04f), Vector3.zero, skirtMat);

        // Light strips rather than round lamps, so it reads as purpose-built courier hardware.
        AddPart(visual.transform, "HeadLight_Strip", PrimitiveType.Cube,
            new Vector3(0f, 0.58f, 1.60f), new Vector3(1.10f, 0.09f, 0.06f), Vector3.zero, headLightMat);
        AddPart(visual.transform, "TailLight_Strip", PrimitiveType.Cube,
            new Vector3(0f, 0.58f, -1.60f), new Vector3(1.06f, 0.09f, 0.06f), Vector3.zero, tailLightMat);

        // Diegetic pack readout on the rear, and the cargo seal that breathes. Both are driven by
        // PodSignals, so the pack level and the stakes are visible on the pod itself.
        GameObject chargeStrip = AddPart(visual.transform, "ChargeStrip", PrimitiveType.Cube,
            new Vector3(0f, 1.03f, -1.61f), new Vector3(0.82f, 0.10f, 0.05f), Vector3.zero, chargeStripMat);
        GameObject cargoSeal = AddPart(visual.transform, "CargoSeal", PrimitiveType.Cube,
            new Vector3(0f, 1.40f, -0.86f), new Vector3(0.92f, 0.04f, 1.20f), Vector3.zero, cargoSealMat);

        AddWheel(visual.transform, "Wheel_FL", new Vector3(-0.62f, 0.25f, 1.14f), wheelMat, 0.50f);
        AddWheel(visual.transform, "Wheel_FR", new Vector3(0.62f, 0.25f, 1.14f), wheelMat, 0.50f);
        AddWheel(visual.transform, "Wheel_RL", new Vector3(-0.62f, 0.26f, -1.14f), wheelMat, 0.52f);
        AddWheel(visual.transform, "Wheel_RR", new Vector3(0.62f, 0.26f, -1.14f), wheelMat, 0.52f);

        // Glowing grant bubble (hidden until a Civic Grant is collected)
        bubble = AddPart(visual.transform, "ShieldBubble", PrimitiveType.Sphere,
            new Vector3(0f, 0.85f, 0f), new Vector3(2.30f, 2.00f, 4.40f), Vector3.zero, bubbleMat);
        bubble.SetActive(false);

        carController = car.AddComponent<CarController>();
        carController.enableBanking = true;
        carController.colliderSize = new Vector3(1.34f, 1.05f, 3.40f);
        carController.colliderCenter = new Vector3(0f, 0.52f, 0f);
        carController.smoothDampTime = 0.10f;   // a light pod darts across a lane, it does not drift

        battery = car.AddComponent<BatterySystem>();

        shieldSystem = car.AddComponent<ShieldSystem>();
        shieldSystem.shieldVisual = bubble;

        PodSignals podSignals = car.AddComponent<PodSignals>();
        SerializedObject podSignalsSo = new SerializedObject(podSignals);
        SetRef(podSignalsSo, "battery", battery);
        SetRef(podSignalsSo, "chargeStrip", chargeStrip.GetComponent<Renderer>());
        SetRef(podSignalsSo, "cargoGlow", cargoSeal.GetComponent<Renderer>());
        podSignalsSo.ApplyModifiedPropertiesWithoutUndo();

        // --- rear-follow camera, parented to the (level) car root ---
        GameObject camGo = new GameObject("Camera");
        camGo.transform.SetParent(car.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 3.0f, -6.2f);
        camGo.transform.localRotation = Quaternion.Euler(11f, 0f, 0f);
        try { camGo.tag = "MainCamera"; } catch { }

        Camera cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 62f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1200f;
        cam.clearFlags = CameraClearFlags.Skybox;

        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camGo.AddComponent<AudioListener>();

        cameraShake = camGo.AddComponent<CameraShake>();

        BuildPostProcessing();
    }

    private static void AddWheel(Transform parent, string name, Vector3 localPosition, Material mat,
        float diameter)
    {
        AddPart(parent, name, PrimitiveType.Cylinder, localPosition,
            new Vector3(diameter, 0.10f, diameter), new Vector3(0f, 0f, 90f), mat);
    }

    public static void BuildPostProcessing()
    {
        GameObject volumeGo = new GameObject("Post Processing");
        Volume volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;

        string profilePath = SettingsFolder + "/RushHour_PostProcessing.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        if (!profile.TryGet(out Bloom bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }

        bloom.active = true;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.85f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.85f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.65f;

        EditorUtility.SetDirty(profile);
        volume.sharedProfile = profile;
    }

    // ============================================================ start screen

    /// <summary>
    /// The road slice behind the start menu: the resurfaced clinic stretch of Route 7 with kerbs,
    /// pavements and a thinning skyline. Built from the same palette as gameplay, so the menu is
    /// unmistakably the same road. Returns the lane dashes, which are the only pieces the menu
    /// needs to move.
    /// </summary>
    public static GameObject[] BuildHeroStreet()
    {
        GameObject root = new GameObject("StartHero");
        Random.InitState(20240921);

        Material roadMat = Palette("Road");
        Material laneMat = Palette("LaneMarking");
        Material edgeMat = Palette("EdgeLine");
        Material curbMat = Palette("Curb");
        Material sidewalkMat = Palette("Sidewalk");
        Material resurfacedMat = Palette("Clinic_Road");

        MarkStatic(CreatePrimitive("Road", PrimitiveType.Plane,
            new Vector3(0f, 0f, 180f), new Vector3(1.2f, 1f, 20f), roadMat, root.transform, false));

        MarkStatic(CreatePrimitive("Road_Resurfaced", PrimitiveType.Cube,
            new Vector3(0f, ResurfacedTopY * 0.5f, 180f), new Vector3(8.4f, ResurfacedTopY, 200f),
            resurfacedMat, root.transform, false));

        for (int side = -1; side <= 1; side += 2)
        {
            string label = side < 0 ? "Left" : "Right";

            MarkStatic(CreatePrimitive("EdgeLine_" + label, PrimitiveType.Cube,
                new Vector3(side * 3.85f, ResurfacedTopY + 0.01f, 180f), new Vector3(0.13f, 0.02f, 200f),
                edgeMat, root.transform, false));

            MarkStatic(CreatePrimitive("Curb_" + label, PrimitiveType.Cube,
                new Vector3(side * 4.25f, 0.14f, 180f), new Vector3(0.5f, 0.28f, 200f),
                curbMat, root.transform, false));

            MarkStatic(CreatePrimitive("Sidewalk_" + label, PrimitiveType.Cube,
                new Vector3(side * 5.5f, 0.10f, 180f), new Vector3(2.5f, 0.20f, 200f),
                sidewalkMat, root.transform, false));
        }

        // The dashes span exactly one wrap cycle, so recycling them by that same length keeps
        // their spacing intact instead of stacking two of them on the same z.
        List<GameObject> dashes = new List<GameObject>();
        for (float z = 100f; z < 280f; z += 10f)
        {
            foreach (float x in new[] { -1.25f, 1.25f })
            {
                dashes.Add(CreatePrimitive("LaneDash", PrimitiveType.Cube,
                    new Vector3(x, ResurfacedTopY + 0.01f, z), new Vector3(0.13f, 0.02f, 4.5f),
                    laneMat, root.transform, false));
            }
        }

        int index = 0;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 5; i++)
            {
                float depth = Random.Range(9f, 15f);
                float width = Random.Range(7f, 12f);
                float height = Random.Range(5f, 14f);
                float z = 112f + (i * 36f) + Random.Range(-6f, 6f);
                float x = side * (12f + (depth * 0.5f));
                string label = (side < 0 ? "W" : "E") + index.ToString("00");

                MarkStatic(CreatePrimitive("Building_" + label, PrimitiveType.Cube,
                    new Vector3(x, height * 0.5f, z), new Vector3(width, height, depth),
                    Palette("Building_" + Random.Range(0, BuildingColors.Length)), root.transform, false));

                MarkStatic(CreatePrimitive("BuildingCrown_" + label, PrimitiveType.Cube,
                    new Vector3(x, height + 0.6f, z), new Vector3(width * 0.55f, 1.2f, depth * 0.55f),
                    Palette("Building_Crown"), root.transform, false));

                index++;
            }
        }

        return dashes.ToArray();
    }

    /// <summary>
    /// The courier pod as a static prop for the start screen: the same skirt, body, cabin, cargo
    /// box, light strips, pack strip, breathing seal and wheels as the playable pod, with none of
    /// the runtime components, so nothing on the menu drives or drains charge.
    /// </summary>
    public static Transform BuildHeroPod(Vector3 position, float yaw)
    {
        GameObject pod = new GameObject("HeroPod");
        pod.transform.position = position;
        pod.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(pod.transform, false);
        Transform rig = visual.transform;

        AddPart(rig, "Skirt", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f),
            new Vector3(1.26f, 0.30f, 3.28f), Vector3.zero, Palette("Pod_Skirt"));
        AddPart(rig, "Body", PrimitiveType.Cube, new Vector3(0f, 0.64f, 0f),
            new Vector3(1.20f, 0.52f, 3.18f), Vector3.zero, Palette("Pod_Body"));
        AddPart(rig, "Cabin", PrimitiveType.Cube, new Vector3(0f, 1.04f, 0.74f),
            new Vector3(1.04f, 0.44f, 1.30f), Vector3.zero, Palette("Pod_Glass"));
        AddPart(rig, "Cargo", PrimitiveType.Cube, new Vector3(0f, 1.03f, -0.86f),
            new Vector3(1.10f, 0.62f, 1.30f), Vector3.zero, Palette("Pod_Cargo"));
        AddPart(rig, "AccentStripe", PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f),
            new Vector3(1.28f, 0.12f, 3.24f), Vector3.zero, Palette("Pod_Accent"));
        AddPart(rig, "HeadLight_Strip", PrimitiveType.Cube, new Vector3(0f, 0.58f, 1.60f),
            new Vector3(1.10f, 0.09f, 0.06f), Vector3.zero, Palette("Pod_HeadLight"));
        AddPart(rig, "TailLight_Strip", PrimitiveType.Cube, new Vector3(0f, 0.58f, -1.60f),
            new Vector3(1.06f, 0.09f, 0.06f), Vector3.zero, Palette("Pod_TailLight"));
        AddPart(rig, "ChargeStrip", PrimitiveType.Cube, new Vector3(0f, 1.03f, -1.61f),
            new Vector3(0.96f, 0.34f, 0.06f), Vector3.zero, Palette("Pod_ChargeStrip"));
        AddPart(rig, "CargoSeal", PrimitiveType.Cube, new Vector3(0f, 1.40f, -0.86f),
            new Vector3(0.62f, 0.42f, 0.08f), Vector3.zero, Palette("Pod_CargoSeal"));

        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                AddPart(rig, "Wheel_" + sx + "_" + sz, PrimitiveType.Cylinder,
                    new Vector3(sx * 0.62f, 0.25f, sz * 1.14f), new Vector3(0.50f, 0.10f, 0.50f),
                    new Vector3(0f, 0f, 90f), Palette("Pod_Wheel"));
            }
        }

        return pod.transform;
    }

    /// <summary>
    /// Material lookup by asset name, recreating any swatch the playable scene has not written
    /// yet from the same palette it uses, so a start-screen build on a fresh project still gets
    /// the right colours instead of untextured magenta.
    /// </summary>
    private static Material Palette(string name)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
        if (existing != null) return existing;

        switch (name)
        {
            case "Road": return CreateLitMaterial("Road", RoadColor, 0.08f);
            case "LaneMarking": return CreateLitMaterial("LaneMarking", LaneColor, 0.25f);
            case "EdgeLine": return CreateLitMaterial("EdgeLine", EdgeLineColor, 0.20f);
            case "Curb": return CreateLitMaterial("Curb", CurbColor, 0.10f);
            case "Sidewalk": return CreateLitMaterial("Sidewalk", SidewalkColor, 0.05f);
            case "Clinic_Road": return CreateLitMaterial("Clinic_Road", ClinicRoadColor, 0.30f);
            case "Building_Crown": return CreateLitMaterial("Building_Crown", new Color(0.50f, 0.53f, 0.55f), 0.30f);
            case "Pod_Body": return CreateLitMaterial("Pod_Body", PodBodyColor, 0.58f, 0.15f);
            case "Pod_Cargo": return CreateLitMaterial("Pod_Cargo", PodCargoColor, 0.42f, 0.05f);
            case "Pod_Glass": return CreateLitMaterial("Pod_Glass", PodGlassColor, 0.90f, 0.35f);
            case "Pod_Skirt": return CreateLitMaterial("Pod_Skirt", PodSkirtColor, 0.18f);
            case "Pod_Wheel": return CreateLitMaterial("Pod_Wheel", PodWheelColor, 0.22f);
            case "Pod_Accent": return CreateLitMaterial("Pod_Accent", AccentColor, 0.55f, 0.05f);
            case "Pod_HeadLight": return CreateLitMaterial("Pod_HeadLight", HeadLightColor, 0.60f, 0.05f, HeadLightColor, 2.2f);
            case "Pod_TailLight": return CreateLitMaterial("Pod_TailLight", TailLightColor, 0.60f, 0.05f, TailLightColor, 2.2f);
            case "Pod_ChargeStrip": return CreateLitMaterial("Pod_ChargeStrip", ChargeGlowColor, 0.55f, 0.05f, ChargeGlowColor, 2.6f);
            case "Pod_CargoSeal": return CreateLitMaterial("Pod_CargoSeal", new Color(0.45f, 1.00f, 0.72f), 0.50f, 0.05f, new Color(0.45f, 1.00f, 0.72f), 1.6f);
        }

        if (name.StartsWith("Building_"))
        {
            int swatch;
            if (int.TryParse(name.Substring("Building_".Length), out swatch) &&
                swatch >= 0 && swatch < BuildingColors.Length)
            {
                return CreateLitMaterial(name, BuildingColors[swatch], 0.12f);
            }
        }

        return null;
    }

    // ===================================================================== HUD

    private struct HudRefs
    {
        public Image chargeFill;
        public Text chargeValue;
        public Text lowCharge;
        public Text distanceText;
        public Text districtText;
        public CanvasGroup bannerGroup;
        public Text bannerTitle;
        public Text bannerBody;
        public Text resultTitle;
        public Text resultBody;
        public GameObject restartButton;
        public GameObject menuButton;
        public Image flashOverlay;
        public Image resultScrim;
    }

    private static HudController BuildHud(GameManager gameManager, BatterySystem battery, out HudRefs hud)
    {
        Font font = GetFont();
        hud = new HudRefs();

        GameObject canvasGo = new GameObject("HUD",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // This project runs the new Input System exclusively, so the UI needs its module.
        GameObject esGo = new GameObject("EventSystem", typeof(EventSystem));
        System.Type uiModule = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (uiModule != null) esGo.AddComponent(uiModule);
        else esGo.AddComponent<StandaloneInputModule>();

        // Impact flash sits at the bottom of the stack so it never washes out the readouts.
        hud.flashOverlay = RushHourUiTheme.Box(canvasGo.transform, "FlashOverlay", new Color(0f, 0f, 0f, 0f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero).GetComponent<Image>();

        // The scrim the result card reads over: ink across the frozen road, but under the chrome
        // and under the impact flash, both of which have to survive the end of a run.
        hud.resultScrim = RushHourUiTheme.Box(canvasGo.transform, "ResultScrim",
            RushHourUiTheme.WithAlpha(RushHourUiTheme.Ink, 0.84f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero).GetComponent<Image>();

        // ---------------------------------------------------------- charge panel
        GameObject panel = RushHourUiTheme.Outline(canvasGo.transform, "ChargePanel",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(28f, -28f), new Vector2(384f, 150f), 2f, 0.70f);

        RushHourUiTheme.Type(panel.transform, "Caption", RushHourUiTheme.Spread("PACK CHARGE"), font, 18,
            TextAnchor.MiddleLeft, RushHourUiTheme.Amber, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(20f, -14f), new Vector2(280f, 28f), false);

        hud.chargeValue = RushHourUiTheme.Type(panel.transform, "Value", "100%", font, 32,
            TextAnchor.MiddleRight, RushHourUiTheme.Bone, FontStyle.Bold,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-20f, -12f), new Vector2(160f, 36f));

        RushHourUiTheme.Hairline(panel.transform, "CaptionRule",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -50f), new Vector2(-40f, 2f), RushHourUiTheme.RuleFaintInk);

        GameObject barBg = RushHourUiTheme.Box(panel.transform, "ChargeBar", RushHourUiTheme.Track,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 18f), new Vector2(-40f, 26f));

        hud.chargeFill = RushHourUiTheme.Box(barBg.transform, "Fill", RushHourUiTheme.Bone,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-6f, -6f)).GetComponent<Image>();
        hud.chargeFill.sprite = GetUiSprite();
        hud.chargeFill.type = Image.Type.Filled;
        hud.chargeFill.fillMethod = Image.FillMethod.Horizontal;
        hud.chargeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        hud.chargeFill.fillAmount = 1f;

        hud.lowCharge = RushHourUiTheme.Type(panel.transform, "LowCharge",
            "LOW CHARGE  ·  FIND A RECHARGE POINT", font, 19,
            TextAnchor.MiddleLeft, RushHourUiTheme.Blood, FontStyle.Bold,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(20f, 54f), new Vector2(344f, 26f), false);

        // --------------------------------------------------------- route readout
        RushHourUiTheme.Type(canvasGo.transform, "DestinationText",
            RushHourUiTheme.Spread("GREENFIELD COMMUNITY CLINIC"), font, 24,
            TextAnchor.MiddleCenter, RushHourUiTheme.Cream, FontStyle.Bold,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(1000f, 34f));

        RushHourUiTheme.Hairline(canvasGo.transform, "DestinationRule",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -64f), new Vector2(560f, 2f));

        hud.distanceText = RushHourUiTheme.Type(canvasGo.transform, "DistanceText", "0 / 500 m", font, 34,
            TextAnchor.MiddleCenter, RushHourUiTheme.Bone, FontStyle.Bold,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -84f), new Vector2(1000f, 44f));

        hud.districtText = RushHourUiTheme.Type(canvasGo.transform, "DistrictText", "OLD TRANSIT CORRIDOR", font, 18,
            TextAnchor.MiddleCenter, RushHourUiTheme.Amber, FontStyle.Bold,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -124f), new Vector2(1000f, 28f));

        CreateUIText(canvasGo.transform, "RouteText", "GREENLINE LOGISTICS\nROUTE 7  ·  MEDICAL RUN", font, 20,
            TextAnchor.UpperRight, RushHourUiTheme.Faint, FontStyle.Bold,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-28f, -26f), new Vector2(460f, 64f));

        RushHourUiTheme.HintBar(canvasGo.transform, font, "A/D", "CHANGE LANE",
            "GREENLINE LOGISTICS  \u00b7  ROUTE 7");

        // ----------------------------------------------------------- route banner
        GameObject bannerGo = new GameObject("RouteBanner", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform bannerRt = bannerGo.GetComponent<RectTransform>();
        bannerRt.SetParent(canvasGo.transform, false);
        bannerRt.anchorMin = new Vector2(0.5f, 0.5f);
        bannerRt.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRt.pivot = new Vector2(0.5f, 0.5f);
        bannerRt.anchoredPosition = new Vector2(0f, 232f);
        bannerRt.sizeDelta = new Vector2(1120f, 156f);

        hud.bannerGroup = bannerGo.GetComponent<CanvasGroup>();
        hud.bannerGroup.alpha = 0f;
        hud.bannerGroup.interactable = false;
        hud.bannerGroup.blocksRaycasts = false;

        RushHourUiTheme.Box(bannerGo.transform, "Backdrop", RushHourUiTheme.Panel,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        RushHourUiTheme.Box(bannerGo.transform, "Accent", RushHourUiTheme.Amber,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            Vector2.zero, new Vector2(7f, -22f));

        RushHourUiTheme.Hairline(bannerGo.transform, "HeadRule",
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0f, 2f));

        RushHourUiTheme.Hairline(bannerGo.transform, "FootRule",
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0f, 2f));

        hud.bannerTitle = CreateUIText(bannerGo.transform, "Title", "ROUTE 7  ·  MEDICAL RUN", font, 34,
            TextAnchor.MiddleLeft, RushHourUiTheme.Bone, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(34f, -24f), new Vector2(1040f, 44f));

        hud.bannerBody = RushHourUiTheme.Type(bannerGo.transform, "Body", "", font, 21,
            TextAnchor.UpperLeft, RushHourUiTheme.Muted, FontStyle.Normal,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(34f, -76f), new Vector2(1040f, 64f));

        // ------------------------------------------------------------- result card
        hud.resultTitle = RushHourUiTheme.Type(canvasGo.transform, "ResultTitle", "", font, 72,
            TextAnchor.MiddleCenter, RushHourUiTheme.Bone, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 132f), new Vector2(1400f, 100f));

        hud.resultBody = RushHourUiTheme.Type(canvasGo.transform, "ResultBody", "", font, 28,
            TextAnchor.MiddleCenter, RushHourUiTheme.Cream, FontStyle.Normal,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 52f), new Vector2(1400f, 46f));

        Button restart = RushHourUiTheme.FramedButton(canvasGo.transform, "RestartButton", "RUN AGAIN",
            font, 22,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-178f, -60f), new Vector2(340f, 78f),
            RushHourUiTheme.Bone, RushHourUiTheme.Bone, gameManager.RestartLevel);

        hud.restartButton = restart.gameObject;

        // Sits beside RUN AGAIN on a lost shipment and nowhere else, so a wrecked run is never a
        // dead end. HudController keeps it hidden and slides RUN AGAIN back to the middle for a
        // completed delivery.
        Button menu = RushHourUiTheme.FramedButton(canvasGo.transform, "MenuButton", "RETURN TO MENU",
            font, 22,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(178f, -60f), new Vector2(340f, 78f),
            RushHourUiTheme.Bone, RushHourUiTheme.Bone, gameManager.ReturnToMenu);

        hud.menuButton = menu.gameObject;

        // --------------------------------------------------------- HudController
        HudController hudController = canvasGo.AddComponent<HudController>();

        SerializedObject so = new SerializedObject(hudController);
        SetRef(so, "battery", battery);
        SetRef(so, "chargeFill", hud.chargeFill);
        SetRef(so, "chargeValueText", hud.chargeValue);
        SetRef(so, "lowChargeText", hud.lowCharge);
        SetRef(so, "distanceText", hud.distanceText);
        SetRef(so, "districtText", hud.districtText);
        SetRef(so, "bannerGroup", hud.bannerGroup);
        SetRef(so, "bannerTitleText", hud.bannerTitle);
        SetRef(so, "bannerBodyText", hud.bannerBody);
        SetRef(so, "resultTitleText", hud.resultTitle);
        SetRef(so, "resultBodyText", hud.resultBody);
        SetRef(so, "restartButton", hud.restartButton);
        SetRef(so, "menuButton", hud.menuButton);
        SetRef(so, "flashOverlay", hud.flashOverlay);
        SetRef(so, "resultScrim", hud.resultScrim);
        SetRef(so, "gameManager", gameManager);

        // The interface palette, carried into the runtime component so the HUD is set in the same
        // inks as the menu. HudController owns these from here on; nothing else writes them.
        SerializedProperty chargeHigh = so.FindProperty("chargeHighColor");
        if (chargeHigh != null) chargeHigh.colorValue = RushHourUiTheme.Bone;
        SerializedProperty chargeMid = so.FindProperty("chargeMidColor");
        if (chargeMid != null) chargeMid.colorValue = RushHourUiTheme.Amber;
        SerializedProperty chargeLow = so.FindProperty("chargeLowColor");
        if (chargeLow != null) chargeLow.colorValue = RushHourUiTheme.Blood;
        SerializedProperty successTint = so.FindProperty("successColor");
        if (successTint != null) successTint.colorValue = RushHourUiTheme.Amber;
        SerializedProperty failureTint = so.FindProperty("failureColor");
        if (failureTint != null) failureTint.colorValue = RushHourUiTheme.Blood;

        SerializedProperty flashFade = so.FindProperty("flashFadeSpeed");
        if (flashFade != null) flashFade.floatValue = 0.9f; // let the impact colour survive the hit-stop

        SerializedProperty districts = so.FindProperty("districts");
        if (districts != null)
        {
            districts.arraySize = 4;
            SetDistrict(districts.GetArrayElementAtIndex(0), 0f, "OLD TRANSIT CORRIDOR");
            SetDistrict(districts.GetArrayElementAtIndex(1), 120f, "DEFERRED MAINTENANCE ZONE");
            SetDistrict(districts.GetArrayElementAtIndex(2), 260f, "STALLED CONSTRUCTION DISTRICT");
            SetDistrict(districts.GetArrayElementAtIndex(3), 400f, "GREENFIELD CLINIC APPROACH");
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        return hudController;
    }

    private static void SetDistrict(SerializedProperty element, float fromDistance, string name)
    {
        SerializedProperty fromProp = element.FindPropertyRelative("fromDistance");
        if (fromProp != null) fromProp.floatValue = fromDistance;

        SerializedProperty nameProp = element.FindPropertyRelative("districtName");
        if (nameProp != null) nameProp.stringValue = name;
    }

    // ================================================================= prefabs

    private struct PartDef
    {
        public string name;
        public PrimitiveType shape;
        public Vector3 position;
        public Vector3 scale;
        public Vector3 euler;
        public Material material;

        public PartDef(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
            : this(name, shape, position, scale, Vector3.zero, material)
        {
        }

        public PartDef(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Vector3 euler, Material material)
        {
            this.name = name;
            this.shape = shape;
            this.position = position;
            this.scale = scale;
            this.euler = euler;
            this.material = material;
        }
    }

    private static GameObject BuildObstaclePrefab(string name, Vector3 colliderSize, Vector3 colliderCenter,
        PartDef[] parts,
        Obstacle.HazardKind hazardKind = Obstacle.HazardKind.Crash, float chargePenalty = 0f)
    {
        GameObject root = new GameObject(name);

        BoxCollider col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = colliderSize;
        col.center = colliderCenter;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        try { root.tag = "Obstacle"; } catch { }

        Obstacle obstacle = root.AddComponent<Obstacle>();

        SerializedObject so = new SerializedObject(obstacle);

        SerializedProperty kindProp = so.FindProperty("hazardKind");
        if (kindProp != null) kindProp.enumValueIndex = (int)hazardKind;

        if (chargePenalty > 0f)
        {
            SerializedProperty penaltyProp = so.FindProperty("chargePenalty");
            if (penaltyProp != null) penaltyProp.floatValue = chargePenalty;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // Visual parts live under a child so the prefab root stays unrotated for the spawner.
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        for (int i = 0; i < parts.Length; i++)
        {
            PartDef p = parts[i];
            AddPart(visual.transform, p.name, p.shape, p.position, p.scale, p.euler, p.material);
        }

        return SavePrefab(root, name);
    }

    private static GameObject BuildShieldPickupPrefab(Material coreMat, Material haloMat)
    {
        // A Civic Grant: a spinning emissive core inside a soft halo.
        GameObject root = new GameObject("ShieldPickup");

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.80f;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        AddPart(root.transform, "GrantHalo", PrimitiveType.Sphere, Vector3.zero,
            new Vector3(1.30f, 1.30f, 1.30f), Vector3.zero, haloMat);
        AddPart(root.transform, "GrantDiamond", PrimitiveType.Cube, Vector3.zero,
            new Vector3(0.62f, 0.62f, 0.62f), new Vector3(45f, 0f, 45f), coreMat);
        AddPart(root.transform, "GrantCore", PrimitiveType.Sphere, Vector3.zero,
            new Vector3(0.42f, 0.42f, 0.42f), Vector3.zero, coreMat);

        root.AddComponent<ShieldPickup>();
        return SavePrefab(root, "ShieldPickup");
    }

    private static GameObject BuildChargePickupPrefab(Material coreMat, Material haloMat)
    {
        // A grid recharge point: a breathing emissive orb.
        GameObject root = new GameObject("ChargePickup");

        SphereCollider col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.85f;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        AddPart(root.transform, "Halo", PrimitiveType.Sphere, Vector3.zero,
            new Vector3(1.35f, 1.35f, 1.35f), Vector3.zero, haloMat);
        AddPart(root.transform, "Orb", PrimitiveType.Sphere, Vector3.zero,
            new Vector3(0.58f, 0.58f, 0.58f), Vector3.zero, coreMat);

        root.AddComponent<ChargePickup>();
        return SavePrefab(root, "ChargePickup");
    }

    private static GameObject SavePrefab(GameObject instance, string name)
    {
        string path = PrefabFolder + "/" + name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    // ================================================================= helpers

    private static GameObject AddPart(Transform parent, string name, PrimitiveType shape,
        Vector3 localPosition, Vector3 localScale, Vector3 localEuler, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;

        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.transform.localEulerAngles = localEuler;

        if (mat != null)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        return go;
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position,
        Vector3 scale, Material mat, Transform parent, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;

        Collider col = go.GetComponent<Collider>();
        if (col != null && !keepCollider) Object.DestroyImmediate(col);

        if (mat != null)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        return go;
    }

    private static void MarkStatic(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    public static GameObject CreateUIImage(Transform parent, string name, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta,
        bool raycastTarget)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return go;
    }

    public static Text CreateUIText(Transform parent, string name, string content, Font font,
        int fontSize, TextAnchor anchor, Color color, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;

        Text text = go.GetComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = color;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public static Font GetFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return font;
    }

    public static Sprite GetUiSprite()
    {
        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        if (sprite == null) sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        return sprite;
    }

    private static void SetRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null) prop.objectReferenceValue = value;
    }

    private static Material CreateLitMaterial(string name, Color color, float smoothness,
        float metallic = 0.05f, Color? emission = null, float emissionIntensity = 1f)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

        if (emission.HasValue && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission.Value * emissionIntensity);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material CreateTransparentGlowMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetOverrideTag("RenderType", "Transparent");
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);       // Alpha
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * 2.5f);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void DeleteStaleAssets()
    {
        string[] stale =
        {
            PrefabFolder + "/FuelPickup.prefab",
            MaterialFolder + "/FuelPickup.mat",
            MaterialFolder + "/Car.mat",
        };

        foreach (string path in stale)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static void EnsureTag(string tag)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return;

        SerializedObject tagManager = new SerializedObject(assets[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        if (tags == null) return;

        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void AddSceneToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == path) return;
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
