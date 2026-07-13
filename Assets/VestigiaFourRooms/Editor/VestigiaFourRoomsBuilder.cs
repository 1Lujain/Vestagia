#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Vestigia.EditorTools
{
    /// <summary>
    /// Builds the four-room Victorian environment into a Save-As duplicate of the active scene.
    /// It deliberately touches only the Vestigia_FourRooms hierarchy and generated assets.
    /// </summary>
    [InitializeOnLoad]
    public static class VestigiaFourRoomsBuilder
    {
        // The marker-driven hook keeps regeneration explicit, repeatable, and restricted to edit mode.
        private const string RootName = "Vestigia_FourRooms";
        private const string TargetScenePath = "Assets/Scenes/Vestigia_FourRooms.unity";
        private const string GeneratedRoot = "Assets/VestigiaFourRooms";
        private const string MaterialsFolder = GeneratedRoot + "/Materials";
        private const string PackedTexturesFolder = GeneratedRoot + "/GeneratedTextures";
        private const string ValidationFolder = GeneratedRoot + "/Validation";
        private const string BuildRequestName = "VestigiaFourRooms.buildrequest";
        private const string FemaleCharacter = "Assets/Characters/Ch22_nonPBR.fbx";
        private const string InputActionsAsset = "Assets/InputSystem_Actions.inputactions";
        private const string TitleFont = "Assets/_3DStealthGame/Art/Fonts/EmilysCandy-Regular.ttf";
        private const string BodyFont = "Assets/_3DStealthGame/Art/Fonts/Underdog-Regular.ttf";

        // A compact 7.5 m footprint keeps each composition intimate and furnished like the
        // supplied Victorian references instead of reading as a large, sparse blockout.
        private const float RoomSize = 7.5f;
        private const float RoomHeight = 3.45f;
        private const float WallSegment = 1.25f;
        private const float DoorWidth = 2.5f;

        private const string FloorTile = "Assets/7th Side/Modular Prison Asset Pack/Prefabs/Floors/Floor Tile.prefab";
        private const string CeilingTile = "Assets/7th Side/Modular Prison Asset Pack/Prefabs/Floors/Double Sided Floor Tile.prefab";
        private const string Wall = "Assets/_3DStealthGame/Prefabs/Environment/Walls/Wall_Straight_A.prefab";
        private const string Corner = "Assets/_3DStealthGame/Prefabs/Environment/Walls/Wall_Corner_A.prefab";
        private const string Doorway = "Assets/7th Side/Modular Prison Asset Pack/Prefabs/Doors/Doorway.prefab";

        private const string Bedside = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/Bedroom/Bedside_Table.prefab";
        private const string Wardrobe = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/Bedroom/Wardrobe.prefab";
        private const string RugDining = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/Diningroom/Rug_Diningroom.prefab";
        private const string SmallTableA = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/SmallTable_A.prefab";
        private const string SmallTableB = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/SmallTable_B.prefab";
        private const string Fireplace = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/Fireplace.prefab";
        private const string Candlestick = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/Diningroom/Candlestick.prefab";
        private const string ClockPrefab = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/Clock.prefab";
        private const string VictorianChair = "Assets/Resources/Prefabs/chair_big_01.prefab";
        private const string VictorianStool = "Assets/Resources/Prefabs/stool.prefab";
        private const string FrameCat = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/PictureFrame_CatLady.prefab";
        private const string FrameCurvy = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/PictureFrame_Curvy.prefab";
        private const string FrameGhost = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/PictureFrame_GhostMan.prefab";
        private const string FrameMan = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/PictureFrame_MrMan.prefab";
        private const string FrameThin = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/PictureFrame_StraightThin.prefab";
        private const string FrameWolf = "Assets/_3DStealthGame/Prefabs/Environment/Decorations/General/PictureFrame_Wolf.prefab";

        private const string PH = "Assets/PolyHaven/";
        private const string GothicBed = PH + "GothicBed_01_2k.fbx/GothicBed_01_2k.fbx";
        private const string ChineseCabinet = PH + "chinese_cabinet_2k.fbx/chinese_cabinet_2k.fbx";
        private const string ChineseTeaTable = PH + "chinese_tea_table_2k.fbx/chinese_tea_table_2k.fbx";
        private const string WoodenTable01 = PH + "WoodenTable_01_2k.fbx/WoodenTable_01_2k.fbx";
        private const string WoodenTable02 = PH + "WoodenTable_02_2k.fbx/WoodenTable_02_2k.fbx";
        private const string ChessSet = PH + "chess_set_2k.fbx/chess_set_2k.fbx";
        private const string Notebook = PH + "binder_notebook_2k.fbx/binder_notebook_2k.fbx";
        private const string AlarmClock = PH + "alarm_clock_01_2k.fbx/alarm_clock_01_2k.fbx";
        private const string BrassPot = PH + "brass_pot_01_2k.fbx/brass_pot_01_2k.fbx";
        private const string FancyFrame = PH + "fancy_picture_frame_01_2k.fbx/fancy_picture_frame_01_2k.fbx";
        private const string TreasureChest = PH + "treasure_chest_2k.fbx (1)/treasure_chest_2k.fbx";
        private const string CardboardBox = PH + "cardboard_box_01_2k.fbx/cardboard_box_01_2k.fbx";
        private const string BullHead = PH + "bull_head_2k.fbx/bull_head_2k.fbx";

        private static readonly string[] RoomNames =
        {
            "Room_01_Main",
            "Room_02_Memory",
            "Room_03_Study",
            "Room_04_Final"
        };

        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Material> ConvertedMaterialCache = new Dictionary<string, Material>();

        private static bool s_Building;
        private static Material s_WallMaterial;
        private static Material s_WallpaperMaterial;
        private static Material s_FloorMaterial;
        private static Material s_CeilingMaterial;
        private static Material s_DarkWoodMaterial;
        private static Material s_BrassMaterial;
        private static Material s_ParchmentMaterial;
        private static Material s_GreenShadeMaterial;
        private static Material s_LeatherMaterial;
        private static Material s_BurgundyMaterial;
        private static Material s_BookRedMaterial;
        private static Material s_BookGreenMaterial;
        private static Material s_BookBlueMaterial;

        static VestigiaFourRoomsBuilder()
        {
            EditorApplication.delayCall += TryAutomaticBuild;
        }

        [MenuItem("Tools/Vestigia/Build Four Connected Rooms")]
        public static void BuildFromMenu()
        {
            Build();
        }

        [MenuItem("Tools/Vestigia/Validate Four Connected Rooms")]
        public static void ValidateFromMenu()
        {
            GameObject root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogError("[Vestigia] No Vestigia_FourRooms hierarchy exists in the active scene.");
                return;
            }

            string report = Validate(root, GetRoomCenters(root.transform.position));
            WriteTextAsset(ValidationFolder + "/ValidationReport.txt", report);
            Debug.Log(report);
        }

        private static void TryAutomaticBuild()
        {
            if (s_Building || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutomaticBuild;
                return;
            }

            string requestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library", BuildRequestName));
            if (!File.Exists(requestPath))
                return;

            if (!EnsureTmpEssentialsAvailable())
            {
                EditorApplication.delayCall += TryAutomaticBuild;
                return;
            }

            File.Delete(requestPath);
            Build();
        }

        private static bool EnsureTmpEssentialsAvailable()
        {
            if (TMP_Settings.LoadDefaultSettings() != null)
                return true;

            string packageCache = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/PackageCache"));
            string package = Directory.GetFiles(packageCache, "TMP Essential Resources.unitypackage",
                SearchOption.AllDirectories).FirstOrDefault();
            if (string.IsNullOrEmpty(package))
                throw new FileNotFoundException("TextMeshPro is installed, but its bundled Essential Resources package was not found.");

            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return TMP_Settings.LoadDefaultSettings() != null;
        }

        private static void Build()
        {
            if (s_Building)
                return;

            s_Building = true;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Four-room generation is edit-mode only.");
                if (SceneManager.sceneCount != 1)
                    throw new InvalidOperationException("Close additively loaded scenes before generating the four-room scene.");
                if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                    throw new InvalidOperationException("Exit Prefab Mode before generating the four-room scene.");

                EnsureRequiredAssetsExist();
                EnsureFolders();
                PrepareMaterials();
                ConfigureSceneAtmosphere();

                Scene active = EditorSceneManager.GetActiveScene();
                if (!active.IsValid())
                    throw new InvalidOperationException("No valid active scene was available.");

                if (!string.Equals(active.path, TargetScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    // Save-As preserves the original scene file while retaining every unsaved live-scene object.
                    if (!EditorSceneManager.SaveScene(active, TargetScenePath, false))
                        throw new InvalidOperationException("Could not save the duplicated scene to " + TargetScenePath);
                    active = EditorSceneManager.GetActiveScene();
                }
                else
                {
                    BackupCurrentGeneratedScene(active);
                }

                GameObject existing = active.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
                Vector3 anchor = existing != null ? existing.transform.position : FindAnchor();
                Camera preservedCamera = Camera.main;
                if (existing != null && preservedCamera != null && preservedCamera.transform.IsChildOf(existing.transform))
                    preservedCamera.transform.SetParent(null, true);
                if (existing != null)
                    UnityEngine.Object.DestroyImmediate(existing);

                Vector3[] centers = GetRoomCenters(anchor);
                GameObject root = new GameObject(RootName);
                root.transform.position = anchor;

                Transform[] rooms = new Transform[4];
                Transform[] structures = new Transform[4];
                Transform[] furniture = new Transform[4];
                Transform[] props = new Transform[4];
                Transform[] lighting = new Transform[4];

                for (int i = 0; i < 4; i++)
                {
                    GameObject room = NewChild(root.transform, RoomNames[i]);
                    room.transform.position = centers[i];
                    rooms[i] = room.transform;
                    structures[i] = NewChild(room.transform, "Structure").transform;
                    furniture[i] = NewChild(room.transform, "Furniture").transform;
                    props[i] = NewChild(room.transform, "Props").transform;
                    lighting[i] = NewChild(room.transform, "Lighting").transform;
                }

                Transform sharedLighting = NewChild(root.transform, "Shared_Lighting").transform;
                Transform doors = NewChild(root.transform, "Doors_And_Connections").transform;

                for (int i = 0; i < 4; i++)
                    BuildRoomShell(i, structures[i], centers[i]);

                BuildBoundaryWalls(structures, doors, centers);
                BuildMainRoom(furniture[0], props[0], lighting[0]);
                BuildMemoryRoom(furniture[1], props[1], lighting[1]);
                BuildStudyRoom(furniture[2], props[2], lighting[2]);
                BuildFinalRoom(furniture[3], props[3], lighting[3]);
                BuildSharedLighting(sharedLighting, centers);

                StaticizeEnvironment(root);
                BuildOpeningExperience(root.transform, rooms[0], props[0], centers[0]);
                EnsureVestigiaFirstInBuildSettings();
                EditorSceneManager.MarkSceneDirty(active);
                EditorSceneManager.SaveScene(active);

                string report = Validate(root, centers);
                WriteTextAsset(ValidationFolder + "/ValidationReport.txt", report);
                WriteTextAsset(ValidationFolder + "/AssetUsageReport.txt", BuildAssetUsageReport());
                RenderPreviews(centers);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorSceneManager.SaveScene(active);

                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView?.FrameSelected();
                if (report.Contains("RESULT: VALIDATION FAILED", StringComparison.Ordinal))
                    Debug.LogError("[Vestigia] Four-room scene was generated but failed validation at " + TargetScenePath + "\n" + report);
                else
                    Debug.Log("[Vestigia] Four-room scene generated at " + TargetScenePath + "\n" + report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                s_Building = false;
            }
        }

        private static void EnsureRequiredAssetsExist()
        {
            string[] required =
            {
                FloorTile, CeilingTile, Wall, Corner, Doorway,
                Bedside, Wardrobe, RugDining, SmallTableA, SmallTableB, Fireplace, Candlestick, ClockPrefab,
                VictorianChair, VictorianStool,
                FrameCat, FrameCurvy, FrameGhost, FrameMan, FrameThin, FrameWolf,
                GothicBed, ChineseCabinet, ChineseTeaTable, WoodenTable01,
                WoodenTable02, ChessSet, Notebook, AlarmClock, BrassPot, FancyFrame,
                TreasureChest, CardboardBox, BullHead,
                FemaleCharacter, InputActionsAsset, TitleFont, BodyFont
            };

            List<string> missing = required.Where(path => AssetDatabase.LoadMainAssetAtPath(path) == null).ToList();
            if (missing.Count > 0)
                throw new FileNotFoundException("Verified project assets disappeared before build:\n" + string.Join("\n", missing));
        }

        private static void EnsureFolders()
        {
            EnsureFolder(GeneratedRoot);
            EnsureFolder(GeneratedRoot + "/Editor");
            EnsureFolder(MaterialsFolder);
            EnsureFolder(MaterialsFolder + "/Converted");
            EnsureFolder(PackedTexturesFolder);
            EnsureFolder(ValidationFolder);
            EnsureFolder(GeneratedRoot + "/Fonts");
        }

        private static void BackupCurrentGeneratedScene(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path) || !File.Exists(AssetPathToAbsolute(scene.path)))
                return;
            EnsureFolder("Assets/Scenes/Backups");
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = "Assets/Scenes/Backups/Vestigia_FourRooms_" + stamp + ".unity";
            if (!EditorSceneManager.SaveScene(scene, backupPath, true))
                throw new InvalidOperationException("Could not create the pre-rebuild scene backup at " + backupPath);
        }

        private static void EnsureFolder(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static void PrepareMaterials()
        {
            MaterialCache.Clear();
            ConvertedMaterialCache.Clear();

            s_WallMaterial = CreateOrUpdateLit(
                "Victorian_Aged_Plaster",
                "Assets/_3DStealthGame/Art/Textures/BedRoom/Wall_Bedroom_Albedo.tif",
                "Assets/_3DStealthGame/Art/Textures/BedRoom/Wall_Bedroom_Normal.tif",
                "Assets/_3DStealthGame/Art/Textures/BedRoom/Wall_Bedroom_MetallicSmooth.tif",
                "Assets/_3DStealthGame/Art/Textures/BedRoom/Wall_Bedroom_Occlusion.tif",
                new Color(0.64f, 0.49f, 0.37f, 1f), 0.16f, new Vector2(1.15f, 1.15f));

            // The interior skin deliberately keeps the worn wallpaper texture.  The previous
            // solid-color skin hid every age mark and produced the large blank brown panels.
            s_WallpaperMaterial = CreateOrUpdateLit(
                "Victorian_Aged_Wallpaper_Overlay",
                "Assets/_3DStealthGame/Art/Textures/BedRoom/Wall_Bedroom_Albedo.tif",
                "Assets/_3DStealthGame/Art/Textures/BedRoom/Wall_Bedroom_Normal.tif",
                null,
                "Assets/_3DStealthGame/Art/Textures/BedRoom/Wall_Bedroom_Occlusion.tif",
                new Color(0.66f, 0.50f, 0.36f, 1f), 0.14f, new Vector2(1.25f, 0.72f));

            s_FloorMaterial = CreateOrUpdateLit(
                "Victorian_Dark_Wood_Floor",
                "Assets/_3DStealthGame/Art/Textures/Floors/Floor_WoodPlanks_Albedo.png",
                "Assets/_3DStealthGame/Art/Textures/Floors/Floor_WoodPlanks_Normal.png",
                "Assets/_3DStealthGame/Art/Textures/Floors/Floor_WoodPlanks_MetallicSmooth.png",
                "Assets/_3DStealthGame/Art/Textures/Floors/Floor_WoodPlanks_AO.png",
                new Color(0.50f, 0.31f, 0.18f, 1f), 0.28f, new Vector2(1.2f, 1.2f));

            s_CeilingMaterial = CreateSolidLit("Victorian_Aged_Ceiling",
                new Color(0.46f, 0.38f, 0.30f, 1f), 0f, 0.12f);

            s_DarkWoodMaterial = CreateOrUpdateLit(
                "Victorian_Dark_Wood_Trim",
                "Assets/_3DStealthGame/Art/Textures/Floors/Floor_WoodPlanks_Albedo.png",
                "Assets/_3DStealthGame/Art/Textures/Floors/Floor_WoodPlanks_Normal.png",
                "Assets/_3DStealthGame/Art/Textures/Floors/Floor_WoodPlanks_MetallicSmooth.png",
                "Assets/_3DStealthGame/Art/Textures/Floors/Floor_WoodPlanks_AO.png",
                new Color(0.26f, 0.13f, 0.062f, 1f), 0.38f, new Vector2(2.2f, 1f));

            s_BrassMaterial = CreateSolidLit("Antique_Brass", new Color(0.43f, 0.25f, 0.075f, 1f), 0.78f, 0.55f);
            s_ParchmentMaterial = CreateSolidLit("Aged_Parchment", new Color(0.63f, 0.43f, 0.22f, 1f), 0.02f, 0.16f);
            s_GreenShadeMaterial = CreateSolidLit("Bankers_Lamp_Green", new Color(0.035f, 0.16f, 0.08f, 1f), 0.05f, 0.42f);
            s_LeatherMaterial = CreateSolidLit("Deep_Brown_Leather", new Color(0.24f, 0.085f, 0.035f, 1f), 0f, 0.31f);
            s_BurgundyMaterial = CreateSolidLit("Victorian_Burgundy_Fabric", new Color(0.22f, 0.022f, 0.018f, 1f), 0f, 0.18f);
            s_BookRedMaterial = CreateSolidLit("Old_Book_Burgundy", new Color(0.25f, 0.035f, 0.026f, 1f), 0f, 0.25f);
            s_BookGreenMaterial = CreateSolidLit("Old_Book_Forest", new Color(0.035f, 0.16f, 0.095f, 1f), 0f, 0.24f);
            s_BookBlueMaterial = CreateSolidLit("Old_Book_Navy", new Color(0.035f, 0.075f, 0.15f, 1f), 0f, 0.24f);
        }

        private static void ConfigureSceneAtmosphere()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.20f, 0.17f, 0.145f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.115f, 0.092f, 0.078f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.046f, 0.037f, 0.032f, 1f);
            RenderSettings.ambientIntensity = 1.02f;
            RenderSettings.reflectionIntensity = 0.18f;
            RenderSettings.fog = false;

            // Preserve the original scene light object but grade it as a very soft warm ambient
            // source.  The localized room lights remain responsible for the Victorian pools of light.
            Light directional = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(light => light.type == LightType.Directional &&
                    light.gameObject.scene == SceneManager.GetActiveScene());
            if (directional != null)
            {
                directional.color = new Color(0.95f, 0.84f, 0.73f, 1f);
                directional.intensity = 0.28f;
                directional.shadows = LightShadows.None;
            }
        }

        private static Material CreateOrUpdateLit(string name, string basePath, string normalPath,
            string metallicSmoothPath, string occlusionPath, Color tint, float smoothness, Vector2 tiling)
        {
            Material material = LoadOrCreateMaterial(name);
            SetTextureIfPresent(material, "_BaseMap", basePath);
            SetTextureIfPresent(material, "_BumpMap", normalPath);
            SetTextureIfPresent(material, "_MetallicGlossMap", metallicSmoothPath);
            SetTextureIfPresent(material, "_OcclusionMap", occlusionPath);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetTextureScale("_BaseMap", tiling);
            if (material.GetTexture("_BumpMap") != null)
                material.EnableKeyword("_NORMALMAP");
            else
                material.DisableKeyword("_NORMALMAP");
            if (material.GetTexture("_MetallicGlossMap") != null)
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            else
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
            if (material.GetTexture("_OcclusionMap") != null)
                material.EnableKeyword("_OCCLUSIONMAP");
            else
                material.DisableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateSolidLit(string name, Color color, float metallic, float smoothness)
        {
            Material material = LoadOrCreateMaterial(name);
            material.SetTexture("_BaseMap", null);
            material.SetTexture("_BumpMap", null);
            material.SetTexture("_MetallicGlossMap", null);
            material.SetTexture("_OcclusionMap", null);
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_OCCLUSIONMAP");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string name)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit shader was not available.");

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        private static void SetTextureIfPresent(Material material, string property, string path)
        {
            if (!material.HasProperty(property))
                return;
            Texture texture = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture>(path);
            material.SetTexture(property, texture);
        }

        private static Vector3 FindAnchor()
        {
            GameObject namedPlayer = GameObject.Find("Player");
            if (namedPlayer != null)
                return new Vector3(namedPlayer.transform.position.x, 0f, namedPlayer.transform.position.z);

            CharacterController controller = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.transform.root.gameObject.scene == SceneManager.GetActiveScene());
            if (controller != null)
                return new Vector3(controller.transform.position.x, 0f, controller.transform.position.z);

            Camera camera = Camera.main;
            if (camera != null)
                return new Vector3(camera.transform.position.x, 0f, camera.transform.position.z);

            return Vector3.zero;
        }

        private static Vector3[] GetRoomCenters(Vector3 roomOneCenter)
        {
            Vector3[] centers = new Vector3[4];
            for (int i = 0; i < 4; i++)
                centers[i] = roomOneCenter + Vector3.forward * (RoomSize * i);
            return centers;
        }

        private static void BuildRoomShell(int roomIndex, Transform structure, Vector3 center)
        {
            Transform floor = NewChild(structure, "Floor_Modules").transform;
            Transform ceiling = NewChild(structure, "Ceiling_Modules").transform;
            Transform walls = NewChild(structure, "Wall_Modules").transform;
            Transform trim = NewChild(structure, "Victorian_Trim_And_Wainscot").transform;

            float[] tileCenters = { -2.5f, 0f, 2.5f };
            int tileIndex = 1;
            foreach (float x in tileCenters)
            {
                foreach (float z in tileCenters)
                {
                    GameObject floorTile = InstantiateAsset(FloorTile, floor, $"Floor_Tile_{tileIndex:00}");
                    floorTile.transform.position = center + new Vector3(x, 0f, z);
                    FitHorizontal(floorTile, 2.5f, 2.5f);
                    AlignBoundsMaxY(floorTile, 0f);
                    OverrideAllMaterials(floorTile, s_FloorMaterial);

                    GameObject ceilingTile = InstantiateAsset(CeilingTile, ceiling, $"Ceiling_Tile_{tileIndex:00}");
                    ceilingTile.transform.position = center + new Vector3(x, RoomHeight, z);
                    ceilingTile.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
                    FitHorizontal(ceilingTile, 2.5f, 2.5f);
                    AlignBoundsMinY(ceilingTile, RoomHeight);
                    OverrideAllMaterials(ceilingTile, s_CeilingMaterial);
                    tileIndex++;
                }
            }

            CreateTrimCube(ceiling, "Aged_Plaster_Ceiling_Cover",
                center + new Vector3(0f, RoomHeight - 0.035f, 0f),
                new Vector3(7.20f, 0.06f, 7.20f), s_CeilingMaterial);

            float[] segments = Enumerable.Range(0, 6).Select(i => -3.125f + i * WallSegment).ToArray();
            int wallIndex = 1;
            foreach (float z in segments)
            {
                PlaceWallModule(walls, $"West_Wall_{wallIndex:00}", center + new Vector3(-3.75f, 0f, z), 90f);
                PlaceWallModule(walls, $"East_Wall_{wallIndex:00}", center + new Vector3(3.75f, 0f, z), -90f);
                wallIndex++;
            }

            // Corner modules are deliberately wood-clad to read as Victorian pilasters, not prison masonry.
            PlaceCorner(trim, "Corner_SW", center + new Vector3(-3.51f, 0f, -3.51f), 0f);
            PlaceCorner(trim, "Corner_SE", center + new Vector3(3.51f, 0f, -3.51f), 90f);
            PlaceCorner(trim, "Corner_NE", center + new Vector3(3.51f, 0f, 3.51f), 180f);
            PlaceCorner(trim, "Corner_NW", center + new Vector3(-3.51f, 0f, 3.51f), 270f);

            BuildVictorianTrim(trim, center, roomIndex);
        }

        private static void BuildBoundaryWalls(Transform[] structures, Transform doors, Vector3[] centers)
        {
            // Five boundaries: closed entrance, three passable inter-room connections, accessible closed exit.
            float[] boundaryZ =
            {
                centers[0].z - RoomSize * 0.5f,
                centers[0].z + RoomSize * 0.5f,
                centers[1].z + RoomSize * 0.5f,
                centers[2].z + RoomSize * 0.5f,
                centers[3].z + RoomSize * 0.5f
            };

            string[] boundaryNames =
            {
                "Entrance_Door_Closed",
                "Connection_01_02_Open",
                "Connection_02_03_Open",
                "Connection_03_04_Open",
                "Final_Exit_Door_Closed"
            };

            for (int boundary = 0; boundary < boundaryZ.Length; boundary++)
            {
                Transform wallParent = boundary == 0 ? structures[0]
                    : boundary == 1 ? structures[0]
                    : boundary == 2 ? structures[1]
                    : boundary == 3 ? structures[2]
                    : structures[3];

                Transform row = NewChild(wallParent, boundaryNames[boundary] + "_Wall").transform;
                if (boundary == 4)
                {
                    float[] standardCenters = { -3.125f, -1.875f, -0.625f };
                    for (int i = 0; i < standardCenters.Length; i++)
                        PlaceWallModule(row, $"Wall_Segment_{i + 1:00}", new Vector3(centers[0].x + standardCenters[i], 0f, boundaryZ[boundary]), 0f);
                    PlaceWallModule(row, "Wall_Segment_04", new Vector3(centers[0].x + 0.375f, 0f, boundaryZ[boundary]), 0f, 0.75f);
                    PlaceWallModule(row, "Wall_Segment_05", new Vector3(centers[0].x + 3.50f, 0f, boundaryZ[boundary]), 0f, 0.50f);
                }
                else
                {
                    float[] centersX = { -3.125f, -1.875f, 1.875f, 3.125f };
                    for (int i = 0; i < centersX.Length; i++)
                        PlaceWallModule(row, $"Wall_Segment_{i + 1:00}", new Vector3(centers[0].x + centersX[i], 0f, boundaryZ[boundary]), 0f);
                }

                GameObject doorway = InstantiateAsset(Doorway, doors, boundaryNames[boundary]);
                float doorwayX = centers[0].x + (boundary == 4 ? 2.0f : 0f);
                doorway.transform.position = new Vector3(doorwayX, 0f, boundaryZ[boundary]);
                doorway.transform.rotation = Quaternion.identity;
                ApplyDoorwayMaterials(doorway);
                bool open = boundary >= 1 && boundary <= 3;
                SetDoorOpen(doorway, open, boundary % 2 == 0 ? 108f : -108f);
            }
        }

        private static void PlaceWallModule(Transform parent, string name, Vector3 position, float yaw, float targetWidth = WallSegment)
        {
            GameObject wall = InstantiateAsset(Wall, parent, name);
            wall.transform.position = position;
            wall.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            FitWallModule(wall, targetWidth, RoomHeight, yaw);
            AlignBoundsMinY(wall, 0f);
            OverrideAllMaterials(wall, s_WallMaterial);
        }

        private static void PlaceCorner(Transform parent, string name, Vector3 position, float yaw)
        {
            GameObject corner = InstantiateAsset(Corner, parent, name);
            corner.transform.position = position;
            corner.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            ScaleToHeight(corner, RoomHeight);
            AlignBoundsMinY(corner, 0f);
            OverrideAllMaterials(corner, s_DarkWoodMaterial);
        }

        private static void BuildVictorianTrim(Transform parent, Vector3 center, int roomIndex)
        {
            Material wood = s_DarkWoodMaterial;
            const float innerWall = 3.55f;
            const float panelCenter = 2.50f;
            const float panelWidth = 2.45f;

            // A thin interior skin hides the modular wall silhouette/UV seams and restores the
            // worn wallpaper-over-dark-wood look from the supplied Victorian references.
            CreateTrimCube(parent, "West_Aged_Wallpaper", center + new Vector3(-innerWall, 2.22f, 0f),
                new Vector3(0.07f, 1.98f, 7.05f), s_WallpaperMaterial);
            CreateTrimCube(parent, "East_Aged_Wallpaper", center + new Vector3(innerWall, 2.22f, 0f),
                new Vector3(0.07f, 1.98f, 7.05f), s_WallpaperMaterial);
            CreateTrimCube(parent, "South_Aged_Wallpaper_Left", center + new Vector3(-panelCenter, 2.22f, -innerWall),
                new Vector3(panelWidth, 1.98f, 0.07f), s_WallpaperMaterial);
            CreateTrimCube(parent, "South_Aged_Wallpaper_Right", center + new Vector3(panelCenter, 2.22f, -innerWall),
                new Vector3(panelWidth, 1.98f, 0.07f), s_WallpaperMaterial);
            if (roomIndex == 3)
            {
                CreateTrimCube(parent, "North_Aged_Wallpaper_Left", center + new Vector3(-1.40f, 2.22f, innerWall),
                    new Vector3(4.28f, 1.98f, 0.07f), s_WallpaperMaterial);
                CreateTrimCube(parent, "North_Aged_Wallpaper_Right", center + new Vector3(3.40f, 2.22f, innerWall),
                    new Vector3(0.28f, 1.98f, 0.07f), s_WallpaperMaterial);
            }
            else
            {
                CreateTrimCube(parent, "North_Aged_Wallpaper_Left", center + new Vector3(-panelCenter, 2.22f, innerWall),
                    new Vector3(panelWidth, 1.98f, 0.07f), s_WallpaperMaterial);
                CreateTrimCube(parent, "North_Aged_Wallpaper_Right", center + new Vector3(panelCenter, 2.22f, innerWall),
                    new Vector3(panelWidth, 1.98f, 0.07f), s_WallpaperMaterial);
            }

            CreateTrimCube(parent, "West_Wainscot", center + new Vector3(-3.60f, 0.58f, 0f), new Vector3(0.12f, 1.12f, 6.82f), wood);
            CreateTrimCube(parent, "East_Wainscot", center + new Vector3(3.60f, 0.58f, 0f), new Vector3(0.12f, 1.12f, 6.82f), wood);
            CreateTrimCube(parent, "South_Wainscot_Left", center + new Vector3(-panelCenter, 0.58f, -3.60f), new Vector3(panelWidth, 1.12f, 0.12f), wood);
            CreateTrimCube(parent, "South_Wainscot_Right", center + new Vector3(panelCenter, 0.58f, -3.60f), new Vector3(panelWidth, 1.12f, 0.12f), wood);
            if (roomIndex == 3)
            {
                CreateTrimCube(parent, "North_Wainscot_Left", center + new Vector3(-1.40f, 0.58f, 3.60f), new Vector3(4.28f, 1.12f, 0.12f), wood);
                CreateTrimCube(parent, "North_Wainscot_Right", center + new Vector3(3.40f, 0.58f, 3.60f), new Vector3(0.28f, 1.12f, 0.12f), wood);
            }
            else
            {
                CreateTrimCube(parent, "North_Wainscot_Left", center + new Vector3(-panelCenter, 0.58f, 3.60f), new Vector3(panelWidth, 1.12f, 0.12f), wood);
                CreateTrimCube(parent, "North_Wainscot_Right", center + new Vector3(panelCenter, 0.58f, 3.60f), new Vector3(panelWidth, 1.12f, 0.12f), wood);
            }

            CreateTrimCube(parent, "West_Chair_Rail", center + new Vector3(-3.54f, 1.18f, 0f), new Vector3(0.16f, 0.13f, 7.15f), wood);
            CreateTrimCube(parent, "East_Chair_Rail", center + new Vector3(3.54f, 1.18f, 0f), new Vector3(0.16f, 0.13f, 7.15f), wood);
            CreateTrimCube(parent, "South_Chair_Rail_Left", center + new Vector3(-panelCenter, 1.18f, -3.54f), new Vector3(panelWidth, 0.13f, 0.16f), wood);
            CreateTrimCube(parent, "South_Chair_Rail_Right", center + new Vector3(panelCenter, 1.18f, -3.54f), new Vector3(panelWidth, 0.13f, 0.16f), wood);
            if (roomIndex == 3)
            {
                CreateTrimCube(parent, "North_Chair_Rail_Left", center + new Vector3(-1.40f, 1.18f, 3.54f), new Vector3(4.28f, 0.13f, 0.16f), wood);
                CreateTrimCube(parent, "North_Chair_Rail_Right", center + new Vector3(3.40f, 1.18f, 3.54f), new Vector3(0.28f, 0.13f, 0.16f), wood);
            }
            else
            {
                CreateTrimCube(parent, "North_Chair_Rail_Left", center + new Vector3(-panelCenter, 1.18f, 3.54f), new Vector3(panelWidth, 0.13f, 0.16f), wood);
                CreateTrimCube(parent, "North_Chair_Rail_Right", center + new Vector3(panelCenter, 1.18f, 3.54f), new Vector3(panelWidth, 0.13f, 0.16f), wood);
            }

            CreateTrimCube(parent, "West_Crown", center + new Vector3(-3.54f, RoomHeight - 0.12f, 0f), new Vector3(0.20f, 0.22f, 7.20f), wood);
            CreateTrimCube(parent, "East_Crown", center + new Vector3(3.54f, RoomHeight - 0.12f, 0f), new Vector3(0.20f, 0.22f, 7.20f), wood);
            CreateTrimCube(parent, "South_Crown", center + new Vector3(0f, RoomHeight - 0.12f, -3.54f), new Vector3(7.20f, 0.22f, 0.20f), wood);
            CreateTrimCube(parent, "North_Crown", center + new Vector3(0f, RoomHeight - 0.12f, 3.54f), new Vector3(7.20f, 0.22f, 0.20f), wood);

            // Slim pilasters split the wallpaper into period-sized panels and conceal the last
            // visible modular seams without narrowing the playable floor.
            foreach (float offset in new[] { -2.35f, 0f, 2.35f })
            {
                CreateTrimCube(parent, $"West_Wall_Pilaster_{offset:0.00}", center + new Vector3(-3.50f, 2.22f, offset), new Vector3(0.10f, 1.96f, 0.11f), wood);
                CreateTrimCube(parent, $"East_Wall_Pilaster_{offset:0.00}", center + new Vector3(3.50f, 2.22f, offset), new Vector3(0.10f, 1.96f, 0.11f), wood);
            }
            foreach (float offset in new[] { -2.50f, 2.50f })
            {
                CreateTrimCube(parent, $"South_Wall_Pilaster_{offset:0.00}", center + new Vector3(offset, 2.22f, -3.50f), new Vector3(0.11f, 1.96f, 0.10f), wood);
                if (roomIndex != 3)
                    CreateTrimCube(parent, $"North_Wall_Pilaster_{offset:0.00}", center + new Vector3(offset, 2.22f, 3.50f), new Vector3(0.11f, 1.96f, 0.10f), wood);
            }
            if (roomIndex == 3)
                CreateTrimCube(parent, "North_Wall_Pilaster_Final_Left", center + new Vector3(-2.50f, 2.22f, 3.50f), new Vector3(0.11f, 1.96f, 0.10f), wood);
        }

        private static void BuildMainRoom(Transform furniture, Transform props, Transform lighting)
        {
            GameObject bed = PlacePoly(GothicBed, furniture, "Gothic_Antique_Bed",
                new Vector3(-1.70f, 0f, -0.48f), 90f, 3.05f, false);
            AddBedCollision(furniture, bed);
            PlacePrefab(Bedside, furniture, "Bedside_Nightstand", new Vector3(-2.82f, 0f, -1.72f), 8f, 0.86f, true);
            PlacePrefab(Wardrobe, furniture, "Large_Wooden_Wardrobe", new Vector3(2.82f, 0f, 1.72f), -90f, 2.28f, true);
            PlacePrefab(Bedside, furniture, "Small_Wooden_Drawer_Cabinet", new Vector3(1.78f, 0f, 3.03f), 180f, 1.06f, true);
            PlaceOrientedPrefab(VictorianChair, furniture, "Bedroom_Victorian_Chair",
                new Vector3(2.42f, 0f, -2.08f), -138f, 1.34f, true);
            PlacePrefab(SmallTableB, furniture, "Bedroom_Round_Side_Table", new Vector3(1.46f, 0f, -2.02f), 0f, 0.78f, true);
            GameObject rug = PlacePrefab(RugDining, props, "Bedroom_Persian_Rug", new Vector3(-0.10f, 0.012f, 0.30f), 0f, 4.05f, false);
            GradeAllMaterials(rug, "Bedroom_Burgundy", new Color(0.42f, 0.13f, 0.10f, 1f), 0.16f);
            PlaceWallPrefab(FrameCat, props, "Old_Portrait_Above_Bed", new Vector3(-3.42f, 2.16f, -0.45f), 90f, 1.22f);
            PlaceWallPrefab(FrameThin, props, "Old_Landscape_North_Wall", new Vector3(-0.52f, 2.14f, 3.42f), 180f, 1.16f);
            PlaceWallPrefab(FrameCurvy, props, "Bedroom_Portrait_North", new Vector3(0.68f, 2.12f, 3.42f), 180f, 0.84f);
            PlacePolyWall(FancyFrame, props, "Antique_Frame_East_Wall", new Vector3(3.42f, 2.10f, -0.72f), -90f, 1.00f);
            BuildWallShelfWithBooks(props, "Bedroom_Book_Shelf", new Vector3(0.15f, 1.58f, 3.34f), 1.35f);
            // Keep the nightstand readable for the physical introduction note instead of
            // stacking the clock, lamp, and note on the same small surface.
            PlacePoly(AlarmClock, props, "Dresser_Alarm_Clock", new Vector3(1.60f, 0.80f, 3.02f), 180f, 0.27f, false, 0.80f);
            PlacePoly(BrassPot, props, "Small_Antique_Brass_Decoration", new Vector3(1.82f, 0.80f, 3.02f), -10f, 0.30f, false, 0.80f);

            CreateBankersLamp(lighting, "Warm_Bedside_Lamp", new Vector3(-3.02f, 0.70f, -1.84f), 2.25f, 2.65f, false);
            CreateWallSconce(lighting, "Bedroom_Wall_Sconce", new Vector3(3.36f, 2.12f, 0.78f), 2.2f);
            CreateRoomFillLight(lighting, "Main_Room_Warm_Fill", new Vector3(0.45f, 3.05f, 0.45f), 8.9f, 5.2f,
                new Color(1f, 0.82f, 0.67f), LightShadows.Soft, true);
        }

        private static void BuildMemoryRoom(Transform furniture, Transform props, Transform lighting)
        {
            PlaceOrientedPrefab(VictorianChair, furniture, "Victorian_Tufted_Armchair",
                new Vector3(-2.18f, 0f, 1.88f), 18f, 1.42f, true);
            PlacePoly(ChineseTeaTable, furniture, "Ornate_Side_Table", new Vector3(-0.90f, 0f, 1.94f), 0f, 1.02f, true);
            PlacePoly(ChineseCabinet, furniture, "Memory_Room_Display_Cabinet", new Vector3(1.42f, 0f, 3.12f), 180f, 2.45f, true);
            PlacePoly(ChineseTeaTable, furniture, "Chess_Tea_Table", new Vector3(1.72f, 0f, -0.18f), 90f, 1.18f, true);
            PlaceOrientedPrefab(VictorianStool, furniture, "Memory_Chess_Stool",
                new Vector3(2.82f, 0f, -0.18f), -90f, 0.86f, true);
            GameObject rug = PlacePrefab(RugDining, props, "Memory_Room_Rug", new Vector3(0f, 0.012f, 0.20f), 90f, 4.15f, false);
            GradeAllMaterials(rug, "Memory_Muted_Red", new Color(0.40f, 0.12f, 0.09f, 1f), 0.15f);
            PlacePoly(ChessSet, props, "Recognizable_Chess_Set", new Vector3(1.72f, 0.73f, -0.18f), 7f, 0.68f, false, 0.73f);
            PlacePoly(AlarmClock, props, "Cabinet_Clock", new Vector3(1.78f, 1.34f, 3.02f), 180f, 0.28f, false, 1.34f);
            PlacePoly(BrassPot, props, "Brass_Pot_Decoration", new Vector3(-0.90f, 0.70f, 1.94f), 0f, 0.27f, false, 0.70f);
            PlaceWallPrefab(FrameCurvy, props, "Memory_Painting_Above_Chair", new Vector3(-1.62f, 2.18f, 3.42f), 180f, 1.30f);
            PlaceWallPrefab(FrameWolf, props, "Memory_Portrait_West", new Vector3(-3.42f, 2.04f, -1.12f), 90f, 1.06f);
            PlacePolyWall(FancyFrame, props, "Memory_Antique_Frame_East", new Vector3(3.42f, 2.02f, -1.08f), -90f, 0.94f);
            PlacePrefab(ClockPrefab, props, "Victorian_Grandfather_Clock", new Vector3(-2.90f, 0f, 2.78f), 180f, 1.74f, true);
            PlacePoly(Notebook, props, "Memory_Notebook", new Vector3(-0.82f, 0.70f, 1.88f), 22f, 0.28f, false, 0.70f);
            CreatePottedPlant(props, "Memory_Room_Potted_Plant", new Vector3(-3.02f, 0f, 0.70f));

            CreateBankersLamp(lighting, "Memory_Side_Lamp", new Vector3(-1.00f, 0.70f, 1.98f), 3.0f, 2.8f, false);
            CreateWallSconce(lighting, "Memory_Wall_Sconce_Left", new Vector3(-3.36f, 2.10f, -0.15f), 2.0f);
            CreateRoomFillLight(lighting, "Memory_Room_Warm_Fill", new Vector3(0f, 3.05f, 0.55f), 8.0f, 5.1f,
                new Color(1f, 0.82f, 0.68f), LightShadows.None, true);
        }

        private static void BuildStudyRoom(Transform furniture, Transform props, Transform lighting)
        {
            PlacePoly(WoodenTable01, furniture, "Large_Wooden_Study_Desk", new Vector3(-0.20f, 0f, 0.34f), 0f, 2.42f, true);
            PlaceOrientedPrefab(VictorianStool, furniture, "Study_Upholstered_Stool",
                new Vector3(-0.20f, 0f, 1.42f), 180f, 0.88f, true);
            PlacePoly(ChineseCabinet, furniture, "Study_Book_And_Display_Cabinet", new Vector3(2.10f, 0f, 3.12f), 180f, 2.50f, true);
            PlacePoly(WoodenTable02, furniture, "Study_Side_Table", new Vector3(-2.64f, 0f, 2.30f), 0f, 1.08f, true);
            GameObject rug = PlacePrefab(RugDining, props, "Study_Rug", new Vector3(-0.10f, 0.012f, 0.28f), 0f, 4.15f, false);
            GradeAllMaterials(rug, "Study_Deep_Red", new Color(0.43f, 0.10f, 0.075f, 1f), 0.16f);
            PlacePoly(Notebook, props, "Desk_Notebook_Open", new Vector3(-0.55f, 0.78f, 0.18f), -12f, 0.34f, false, 0.78f);
            PlacePoly(Notebook, props, "Desk_Notebook_Stack_01", new Vector3(-0.96f, 0.78f, 0.42f), 16f, 0.28f, false, 0.78f);
            PlacePoly(Notebook, props, "Desk_Notebook_Stack_02", new Vector3(-0.93f, 0.84f, 0.41f), -8f, 0.26f, false, 0.84f);
            PlacePoly(Notebook, props, "Cabinet_Book_01", new Vector3(1.50f, 1.30f, 3.01f), -78f, 0.27f, false, 1.30f);
            CreateAntiqueGlobe(props, "Study_Antique_Globe", new Vector3(-2.64f, 0.77f, 2.30f));
            PlacePoly(TreasureChest, props, "Study_Antique_Chest", new Vector3(-2.62f, 0f, -2.12f), 18f, 1.10f, true);
            PlacePoly(CardboardBox, props, "Study_Archive_Box_01", new Vector3(-2.95f, 0f, -2.82f), -12f, 0.60f, true);
            PlacePoly(CardboardBox, props, "Study_Archive_Box_02", new Vector3(-2.30f, 0f, -2.90f), 18f, 0.50f, true);
            PlacePoly(BrassPot, props, "Study_Brass_Desk_Object", new Vector3(0.52f, 0.78f, 0.42f), 0f, 0.26f, false, 0.78f);
            CreatePaper(props, "Desk_Paper_01", new Vector3(-0.12f, 0.785f, 0.05f), new Vector2(0.34f, 0.24f), -7f);
            CreatePaper(props, "Desk_Paper_02", new Vector3(0.24f, 0.788f, 0.12f), new Vector2(0.30f, 0.22f), 10f);
            PlaceWallPrefab(FrameMan, props, "Study_Portrait_North", new Vector3(-1.72f, 2.16f, 3.42f), 180f, 1.08f);
            PlaceWallPrefab(FrameThin, props, "Study_Picture_West", new Vector3(-3.42f, 2.10f, 0.10f), 90f, 1.02f);
            PlaceWallPrefab(FrameCurvy, props, "Study_Gallery_West", new Vector3(-3.42f, 2.18f, 1.38f), 90f, 0.74f);
            PlacePolyWall(FancyFrame, props, "Study_Antique_Frame_East", new Vector3(3.42f, 2.06f, -0.72f), -90f, 0.92f);
            BuildWallShelfWithBooks(props, "Study_Wall_Book_Shelf", new Vector3(-1.85f, 1.42f, 3.34f), 1.24f);

            CreateBankersLamp(lighting, "Green_Desk_Lamp", new Vector3(0.46f, 0.79f, 0.22f), 3.2f, 2.8f, true);
            PlaceCandlestick(lighting, "Study_Candle", new Vector3(-2.64f, 0.76f, 2.02f), 0.54f, 1.1f);
            CreateWallSconce(lighting, "Study_Wall_Sconce", new Vector3(3.36f, 2.14f, 1.20f), 1.8f);
            CreateRoomFillLight(lighting, "Study_Room_Warm_Fill", new Vector3(0.55f, 3.05f, 0.75f), 7.5f, 5.0f,
                new Color(1f, 0.79f, 0.63f), LightShadows.None, true);
        }

        private static void BuildFinalRoom(Transform furniture, Transform props, Transform lighting)
        {
            PlacePrefab(Fireplace, furniture, "Final_Old_Fireplace", new Vector3(0f, 0f, 3.18f), 180f, 2.46f, true);
            CreateDisplayPedestal(furniture, new Vector3(0f, 0f, 1.58f));
            CreateHauntedPortraitDisplay(furniture, lighting, new Vector3(0f, 0f, 1.58f));
            PlacePoly(TreasureChest, furniture, "Final_Antique_Chest", new Vector3(-2.38f, 0f, -1.86f), 14f, 1.08f, true);
            PlaceOrientedPrefab(VictorianChair, furniture, "Final_Red_Victorian_Chair",
                new Vector3(-2.52f, 0f, 0.72f), 26f, 1.40f, true);
            PlacePrefab(SmallTableB, furniture, "Final_Small_Table_Left", new Vector3(-1.62f, 0f, 1.05f), 0f, 0.80f, true);
            PlacePrefab(SmallTableA, furniture, "Final_Small_Table_Right", new Vector3(2.55f, 0f, 2.18f), 0f, 0.82f, true);
            PlacePrefab(Wardrobe, furniture, "Final_Dark_Cabinet", new Vector3(2.84f, 0f, -1.42f), -90f, 2.02f, true);
            GameObject rug = PlacePrefab(RugDining, props, "Final_Dark_Rug", new Vector3(0f, 0.012f, 0.72f), 0f, 3.92f, false);
            GradeAllMaterials(rug, "Final_Dark_Burgundy", new Color(0.27f, 0.055f, 0.045f, 1f), 0.12f);
            PlacePoly(CardboardBox, props, "Scattered_Box_01", new Vector3(2.78f, 0f, -2.56f), -18f, 0.58f, true);
            PlacePoly(CardboardBox, props, "Scattered_Box_02", new Vector3(2.25f, 0f, -2.92f), 24f, 0.48f, true);
            PlacePoly(CardboardBox, props, "Scattered_Box_03", new Vector3(3.05f, 0f, -3.02f), 8f, 0.42f, true);
            PlacePoly(BrassPot, props, "Final_Brass_Relic", new Vector3(2.55f, 0.58f, 2.18f), -10f, 0.24f, false, 0.58f);
            PlacePolyWall(BullHead, props, "Unsettling_Bull_Head_Trophy", new Vector3(0f, 2.48f, 3.40f), 180f, 1.30f);
            PlaceWallPrefab(FrameWolf, props, "Final_Dark_Portrait_West", new Vector3(-3.42f, 2.06f, -0.58f), 90f, 1.04f);
            PlaceWallPrefab(FrameMan, props, "Final_Somber_Portrait_North_Left", new Vector3(-2.28f, 2.16f, 3.42f), 180f, 0.82f);
            PlacePolyWall(FancyFrame, props, "Final_Antique_Frame_East", new Vector3(3.42f, 2.02f, 0.22f), -90f, 0.92f);
            PlacePoly(Notebook, props, "Final_Clue_Notebook", new Vector3(-1.62f, 0.62f, 1.05f), -18f, 0.28f, false, 0.62f);

            PlaceCandlestick(lighting, "Final_Candles_Left", new Vector3(-1.60f, 0.62f, 0.96f), 0.54f, 1.1f);
            PlaceCandlestick(lighting, "Final_Candles_Right", new Vector3(2.54f, 0.58f, 2.20f), 0.48f, 0.9f);
            CreateWallSconce(lighting, "Final_Wall_Sconce", new Vector3(-3.36f, 2.10f, 2.18f), 1.4f);
            CreateRoomFillLight(lighting, "Final_Room_Dim_Fill", new Vector3(0f, 3.02f, 0.80f), 5.8f, 4.7f,
                new Color(0.86f, 0.68f, 0.57f), LightShadows.None, true);
        }

        private static void BuildSharedLighting(Transform sharedLighting, Vector3[] centers)
        {
            for (int i = 1; i <= 3; i++)
            {
                float boundaryZ = centers[i - 1].z + RoomSize * 0.5f;
                Vector3 worldPosition = new Vector3(centers[0].x + 0.82f, 2.45f, boundaryZ);
                CreateRoomFillLight(sharedLighting, $"Doorway_Amber_Guide_{i:00}",
                    sharedLighting.InverseTransformPoint(worldPosition), 2.5f, 2.35f,
                    new Color(1f, 0.58f, 0.30f), LightShadows.None);
            }
        }

        private static GameObject InstantiateAsset(string path, Transform parent, string name)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                throw new FileNotFoundException("Could not load asset", path);
            GameObject instance = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate " + path);
            instance.name = name;
            return instance;
        }

        private static GameObject PlacePrefab(string path, Transform parent, string name, Vector3 localPosition,
            float yaw, float targetMaxDimension, bool addCollider)
        {
            GameObject instance = InstantiateAsset(path, parent, name);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            ScaleToMaxDimension(instance, targetMaxDimension);
            AlignBoundsMinY(instance, parent.TransformPoint(new Vector3(0f, localPosition.y, 0f)).y);
            EnsureURPMaterials(instance, s_DarkWoodMaterial);
            if (addCollider)
                EnsureBoundsCollider(instance);
            return instance;
        }

        private static GameObject PlaceOrientedPrefab(string path, Transform parent, string name, Vector3 localPosition,
            float yaw, float targetMaxDimension, bool addCollider)
        {
            GameObject instance = InstantiateAsset(path, parent, name);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);
            ScaleToMaxDimension(instance, targetMaxDimension);
            CenterBoundsXZAt(instance, parent.TransformPoint(localPosition));
            AlignBoundsMinY(instance, parent.TransformPoint(new Vector3(0f, localPosition.y, 0f)).y);
            EnsureURPMaterials(instance, s_DarkWoodMaterial);
            if (addCollider)
                EnsureBoundsCollider(instance);
            return instance;
        }

        private static void GradeAllMaterials(GameObject root, string style, Color tint, float smoothness)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null)
                        continue;
                    string shaderName = source.shader != null ? source.shader.name : string.Empty;
                    if (!shaderName.Contains("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase))
                        source = ConvertToURP(source, s_DarkWoodMaterial);

                    string sourcePath = AssetDatabase.GetAssetPath(source);
                    string guid = string.IsNullOrEmpty(sourcePath) ? source.name : AssetDatabase.AssetPathToGUID(sourcePath);
                    string cacheKey = "grade_" + style + "_" + guid;
                    if (!ConvertedMaterialCache.TryGetValue(cacheKey, out Material graded))
                    {
                        string path = MaterialsFolder + "/Converted/" + Sanitize(style + "_" + source.name) + ".mat";
                        graded = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (graded == null)
                        {
                            graded = new Material(source) { name = Sanitize(style + "_" + source.name) };
                            AssetDatabase.CreateAsset(graded, path);
                        }
                        graded.shader = source.shader;
                        graded.CopyPropertiesFromMaterial(source);
                        if (graded.HasProperty("_BaseColor"))
                            graded.SetColor("_BaseColor", tint);
                        if (graded.HasProperty("_Smoothness"))
                            graded.SetFloat("_Smoothness", smoothness);
                        EditorUtility.SetDirty(graded);
                        ConvertedMaterialCache[cacheKey] = graded;
                    }
                    materials[i] = graded;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static GameObject PlacePoly(string path, Transform parent, string name, Vector3 localPosition,
            float yaw, float targetMaxDimension, bool addCollider, float surfaceY = 0f)
        {
            GameObject instance = InstantiateAsset(path, parent, name);
            Quaternion importedAxisCorrection = instance.transform.localRotation;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * importedAxisCorrection;
            ScaleToMaxDimension(instance, targetMaxDimension);
            CenterBoundsXZAt(instance, parent.TransformPoint(localPosition));
            AlignBoundsMinY(instance, parent.TransformPoint(new Vector3(0f, surfaceY, 0f)).y);
            ApplyPolyHavenMaterials(instance, path);
            if (addCollider)
                EnsureBoundsCollider(instance);
            return instance;
        }

        private static void AddBedCollision(Transform parent, GameObject bed)
        {
            Bounds bounds = GetWorldBounds(bed);
            GameObject mattressCollision = NewChild(parent, "Gothic_Bed_Mattress_Collider");
            mattressCollision.transform.position = new Vector3(bounds.center.x, 0.48f, bounds.center.z);
            BoxCollider collider = mattressCollision.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = new Vector3(bounds.size.x * 0.86f, 0.46f, bounds.size.z * 0.80f);
        }

        private static GameObject PlaceWallPrefab(string path, Transform parent, string name, Vector3 localCenter,
            float yaw, float targetMaxDimension)
        {
            GameObject instance = InstantiateAsset(path, parent, name);
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            ScaleToMaxDimension(instance, targetMaxDimension);
            CenterBoundsAt(instance, parent.TransformPoint(localCenter));
            EnsureURPMaterials(instance, s_DarkWoodMaterial);
            return instance;
        }

        private static GameObject PlacePolyWall(string path, Transform parent, string name, Vector3 localCenter,
            float yaw, float targetMaxDimension)
        {
            GameObject instance = InstantiateAsset(path, parent, name);
            Quaternion importedAxisCorrection = instance.transform.localRotation;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * importedAxisCorrection;
            ScaleToMaxDimension(instance, targetMaxDimension);
            CenterBoundsAt(instance, parent.TransformPoint(localCenter));
            ApplyPolyHavenMaterials(instance, path);
            return instance;
        }

        private static void PlaceCandlestick(Transform parent, string name, Vector3 localPosition, float height, float intensity)
        {
            GameObject candle = PlacePrefab(Candlestick, parent, name, localPosition, 0f, Mathf.Max(0.34f, height), false);
            foreach (Light light in candle.GetComponentsInChildren<Light>(true))
            {
                light.color = new Color(1f, 0.43f, 0.14f, 1f);
                light.intensity = intensity;
                light.range = 2.7f;
                light.shadows = LightShadows.None;
                light.lightmapBakeType = LightmapBakeType.Mixed;
            }
        }

        private static void CreateWallSconce(Transform parent, string name, Vector3 localPosition, float intensity)
        {
            GameObject root = NewChild(parent, name);
            root.transform.localPosition = localPosition;
            CreatePrimitiveChild(root.transform, "Antique_Brass_Backplate", PrimitiveType.Sphere,
                Vector3.zero, new Vector3(0.13f, 0.20f, 0.08f), s_BrassMaterial);
            GameObject arm = CreatePrimitiveChild(root.transform, "Curved_Brass_Arm", PrimitiveType.Cylinder,
                new Vector3(0f, -0.11f, -0.10f), new Vector3(0.025f, 0.16f, 0.025f), s_BrassMaterial);
            arm.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            CreatePrimitiveChild(root.transform, "Warm_Frosted_Shade", PrimitiveType.Sphere,
                new Vector3(0f, -0.24f, -0.22f), new Vector3(0.16f, 0.13f, 0.16f), s_ParchmentMaterial);
            CreateRoomFillLight(root.transform, "Sconce_Warm_Light", new Vector3(0f, -0.20f, -0.32f),
                intensity, 2.65f, new Color(1f, 0.48f, 0.18f), LightShadows.None);
        }

        private static void BuildWallShelfWithBooks(Transform parent, string name, Vector3 localPosition, float width)
        {
            GameObject root = NewChild(parent, name);
            root.transform.localPosition = localPosition;
            CreatePrimitiveChild(root.transform, "Dark_Wood_Shelf", PrimitiveType.Cube,
                Vector3.zero, new Vector3(width, 0.10f, 0.28f), s_DarkWoodMaterial);
            CreatePrimitiveChild(root.transform, "Shelf_Back", PrimitiveType.Cube,
                new Vector3(0f, 0.28f, 0.11f), new Vector3(width, 0.52f, 0.08f), s_DarkWoodMaterial);
            CreatePrimitiveChild(root.transform, "Bracket_Left", PrimitiveType.Cube,
                new Vector3(-width * 0.40f, -0.17f, 0.04f), new Vector3(0.08f, 0.32f, 0.18f), s_DarkWoodMaterial);
            CreatePrimitiveChild(root.transform, "Bracket_Right", PrimitiveType.Cube,
                new Vector3(width * 0.40f, -0.17f, 0.04f), new Vector3(0.08f, 0.32f, 0.18f), s_DarkWoodMaterial);

            Material[] bookMaterials = { s_BookRedMaterial, s_BookGreenMaterial, s_BookBlueMaterial, s_ParchmentMaterial };
            float x = -width * 0.42f;
            for (int i = 0; i < 9; i++)
            {
                float bookWidth = 0.075f + (i % 3) * 0.018f;
                float bookHeight = 0.28f + (i % 4) * 0.035f;
                CreatePrimitiveChild(root.transform, $"Old_Book_{i + 1:00}", PrimitiveType.Cube,
                    new Vector3(x + bookWidth * 0.5f, 0.05f + bookHeight * 0.5f, -0.055f),
                    new Vector3(bookWidth, bookHeight, 0.16f), bookMaterials[i % bookMaterials.Length]);
                x += bookWidth + 0.022f;
            }
        }

        private static void CreateBankersLamp(Transform parent, string name, Vector3 localPosition,
            float intensity, float range, bool greenShade)
        {
            GameObject root = NewChild(parent, name);
            root.transform.localPosition = localPosition;
            Material shadeMaterial = greenShade ? s_GreenShadeMaterial : s_ParchmentMaterial;
            CreatePrimitiveChild(root.transform, "Brass_Base", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f),
                new Vector3(0.18f, 0.035f, 0.18f), s_BrassMaterial);
            CreatePrimitiveChild(root.transform, "Brass_Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.30f, 0f),
                new Vector3(0.032f, 0.27f, 0.032f), s_BrassMaterial);
            CreatePrimitiveChild(root.transform, "Lamp_Shade", PrimitiveType.Sphere, new Vector3(0f, 0.57f, 0f),
                new Vector3(0.33f, 0.13f, 0.22f), shadeMaterial);
            CreateRoomFillLight(root.transform, "Warm_Lamp_Light", new Vector3(0f, 0.49f, 0f), intensity, range,
                new Color(1f, 0.52f, 0.18f), LightShadows.None);
        }

        private static void CreateRoomFillLight(Transform parent, string name, Vector3 localPosition, float intensity,
            float range, Color color, LightShadows shadows = LightShadows.None, bool downwardSpot = false)
        {
            GameObject lightObject = NewChild(parent, name);
            lightObject.transform.localPosition = localPosition;
            Light light = lightObject.AddComponent<Light>();
            light.type = downwardSpot ? LightType.Spot : LightType.Point;
            if (downwardSpot)
            {
                lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                light.spotAngle = 124f;
                light.innerSpotAngle = 82f;
            }
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows;
            if (shadows != LightShadows.None)
            {
                light.shadowResolution = LightShadowResolution.Low;
                light.shadowCustomResolution = 256;
            }
            light.shadowStrength = 0.82f;
            light.bounceIntensity = 0.65f;
            light.lightmapBakeType = LightmapBakeType.Mixed;
        }

        private static void CreatePaper(Transform parent, string name, Vector3 localPosition, Vector2 size, float yaw)
        {
            GameObject paper = CreateTrimCube(parent, name, parent.TransformPoint(localPosition),
                new Vector3(size.x, 0.012f, size.y), s_ParchmentMaterial, false);
            paper.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static void CreateDisplayPedestal(Transform parent, Vector3 localPosition)
        {
            CreateTrimCube(parent, "Central_Display_Pedestal_Base",
                parent.TransformPoint(localPosition + new Vector3(0f, 0.07f, 0f)),
                new Vector3(1.55f, 0.14f, 1.08f), s_DarkWoodMaterial);
            CreateTrimCube(parent, "Central_Display_Pedestal_Cabinet",
                parent.TransformPoint(localPosition + new Vector3(0f, 0.34f, 0f)),
                new Vector3(1.35f, 0.54f, 0.90f), s_DarkWoodMaterial, true);
            CreateTrimCube(parent, "Central_Display_Pedestal_Top",
                parent.TransformPoint(localPosition + new Vector3(0f, 0.63f, 0f)),
                new Vector3(1.50f, 0.12f, 1.04f), s_DarkWoodMaterial);
        }

        private static void CreatePottedPlant(Transform parent, string name, Vector3 localPosition)
        {
            GameObject root = NewChild(parent, name);
            root.transform.localPosition = localPosition;
            CreatePrimitiveChild(root.transform, "Aged_Brass_Plant_Pot", PrimitiveType.Cylinder,
                new Vector3(0f, 0.22f, 0f), new Vector3(0.32f, 0.22f, 0.32f), s_BrassMaterial);
            CreatePrimitiveChild(root.transform, "Plant_Central_Stem", PrimitiveType.Cylinder,
                new Vector3(0f, 0.72f, 0f), new Vector3(0.035f, 0.42f, 0.035f), s_BookGreenMaterial);

            Vector3[] leafPositions =
            {
                new Vector3(-0.22f, 0.74f, -0.02f), new Vector3(0.24f, 0.82f, 0.04f),
                new Vector3(-0.16f, 1.03f, 0.03f), new Vector3(0.15f, 1.12f, -0.04f),
                new Vector3(-0.05f, 1.28f, 0.02f)
            };
            float[] angles = { -42f, 48f, -28f, 30f, -8f };
            for (int i = 0; i < leafPositions.Length; i++)
            {
                GameObject leaf = CreatePrimitiveChild(root.transform, $"Dark_Leaf_{i + 1:00}", PrimitiveType.Sphere,
                    leafPositions[i], new Vector3(0.16f, 0.42f, 0.075f), s_BookGreenMaterial);
                leaf.transform.localRotation = Quaternion.Euler(0f, i * 37f, angles[i]);
            }
        }

        private static void CreateAntiqueGlobe(Transform parent, string name, Vector3 localPosition)
        {
            GameObject root = NewChild(parent, name);
            root.transform.localPosition = localPosition;
            CreatePrimitiveChild(root.transform, "Globe_Brass_Base", PrimitiveType.Cylinder,
                new Vector3(0f, 0.055f, 0f), new Vector3(0.24f, 0.055f, 0.24f), s_BrassMaterial);
            CreatePrimitiveChild(root.transform, "Globe_Brass_Stem", PrimitiveType.Cylinder,
                new Vector3(0f, 0.25f, 0f), new Vector3(0.035f, 0.18f, 0.035f), s_BrassMaterial);
            GameObject globe = CreatePrimitiveChild(root.transform, "Faded_World_Globe", PrimitiveType.Sphere,
                new Vector3(0f, 0.55f, 0f), new Vector3(0.50f, 0.50f, 0.50f), s_BookBlueMaterial);
            globe.transform.localRotation = Quaternion.Euler(0f, 18f, -16f);
            GameObject axis = CreatePrimitiveChild(root.transform, "Globe_Antique_Axis", PrimitiveType.Cylinder,
                new Vector3(0f, 0.55f, 0f), new Vector3(0.025f, 0.34f, 0.025f), s_BrassMaterial);
            axis.transform.localRotation = Quaternion.Euler(0f, 0f, -16f);
        }

        private static void CreateHauntedPortraitDisplay(Transform furniture, Transform lighting, Vector3 localPosition)
        {
            GameObject display = NewChild(furniture, "Final_Haunted_Portrait_Display");
            display.transform.localPosition = localPosition;

            CreatePrimitiveChild(display.transform, "Display_Burgundy_Back", PrimitiveType.Cube,
                new Vector3(0f, 1.48f, 0.34f), new Vector3(1.32f, 1.55f, 0.07f), s_BurgundyMaterial);
            CreatePrimitiveChild(display.transform, "Display_Left_Post", PrimitiveType.Cube,
                new Vector3(-0.70f, 1.48f, 0.13f), new Vector3(0.15f, 1.72f, 0.22f), s_DarkWoodMaterial);
            CreatePrimitiveChild(display.transform, "Display_Right_Post", PrimitiveType.Cube,
                new Vector3(0.70f, 1.48f, 0.13f), new Vector3(0.15f, 1.72f, 0.22f), s_DarkWoodMaterial);
            CreatePrimitiveChild(display.transform, "Display_Carved_Top", PrimitiveType.Cube,
                new Vector3(0f, 2.31f, 0.13f), new Vector3(1.62f, 0.18f, 0.34f), s_DarkWoodMaterial);
            CreatePrimitiveChild(display.transform, "Display_Inner_Shelf", PrimitiveType.Cube,
                new Vector3(0f, 0.76f, 0.08f), new Vector3(1.34f, 0.10f, 0.62f), s_DarkWoodMaterial);

            PlaceWallPrefab(FrameCurvy, display.transform, "Final_Central_Unsettling_Portrait",
                new Vector3(0f, 1.53f, -0.02f), 180f, 1.18f);
            CreatePrimitiveChild(display.transform, "Display_Brass_Relic_Left", PrimitiveType.Sphere,
                new Vector3(-0.43f, 0.87f, -0.10f), new Vector3(0.16f, 0.16f, 0.16f), s_BrassMaterial);
            CreatePrimitiveChild(display.transform, "Display_Brass_Relic_Right", PrimitiveType.Sphere,
                new Vector3(0.43f, 0.87f, -0.10f), new Vector3(0.16f, 0.16f, 0.16f), s_BrassMaterial);

            CreateRoomFillLight(lighting, "Portrait_Display_Spotlight",
                localPosition + new Vector3(0f, 2.38f, -0.05f), 3.2f, 2.6f,
                new Color(0.92f, 0.72f, 0.58f), LightShadows.Soft, true);
        }
        private static GameObject CreateTrimCube(Transform parent, string name, Vector3 worldPosition, Vector3 scale,
            Material material, bool keepCollider = false)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, true);
            cube.transform.position = worldPosition;
            cube.transform.localScale = scale;
            Renderer renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            Collider collider = cube.GetComponent<Collider>();
            if (!keepCollider && collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            return cube;
        }

        private static GameObject CreatePrimitiveChild(Transform parent, string name, PrimitiveType type,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            return primitive;
        }

        private static GameObject NewChild(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void SetDoorOpen(GameObject doorway, bool open, float angle)
        {
            Transform leaf = doorway.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Door" && t.GetComponent<Renderer>() != null);
            if (leaf == null)
                throw new InvalidOperationException("Doorway prefab no longer contains a renderable Door leaf.");

            leaf.localRotation = open ? Quaternion.Euler(0f, angle, 0f) : Quaternion.identity;
            if (open)
            {
                foreach (Collider collider in leaf.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
            }
            doorway.name = open ? doorway.name.Replace("_Open", "_Open") : doorway.name.Replace("_Closed", "_Closed");
        }

        private static void ApplyDoorwayMaterials(GameObject doorway)
        {
            foreach (Renderer renderer in doorway.GetComponentsInChildren<Renderer>(true))
            {
                string lower = renderer.gameObject.name.ToLowerInvariant();
                Material material = lower.Contains("doorway") ? s_WallMaterial : s_DarkWoodMaterial;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }

        private static void OverrideAllMaterials(GameObject root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }

        private static void EnsureURPMaterials(GameObject root, Material fallback)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null)
                    {
                        materials[i] = fallback;
                        continue;
                    }

                    string shaderName = source.shader != null ? source.shader.name : string.Empty;
                    if (shaderName.Contains("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase))
                        continue;

                    materials[i] = ConvertToURP(source, fallback);
                }
                renderer.sharedMaterials = materials;
            }
        }

        private static Material ConvertToURP(Material source, Material fallback)
        {
            string assetPath = AssetDatabase.GetAssetPath(source);
            string guid = string.IsNullOrEmpty(assetPath) ? "instance" : AssetDatabase.AssetPathToGUID(assetPath);
            string key = source.name + "_" + guid;
            if (ConvertedMaterialCache.TryGetValue(key, out Material cached))
                return cached;

            string safeName = Sanitize(source.name) + "_" + (guid.Length >= 8 ? guid.Substring(0, 8) : guid);
            string path = MaterialsFolder + "/Converted/" + safeName + ".mat";
            Material converted = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (converted == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                converted = new Material(shader) { name = safeName };
                AssetDatabase.CreateAsset(converted, path);
            }

            Texture baseMap = GetFirstTexture(source, "_BaseMap", "_MainTex");
            Texture normal = GetFirstTexture(source, "_BumpMap", "_NormalMap");
            Texture metallic = GetFirstTexture(source, "_MetallicGlossMap", "_SpecGlossMap");
            Texture occlusion = GetFirstTexture(source, "_OcclusionMap");
            Color color = GetFirstColor(source, Color.white, "_BaseColor", "_Color");
            if (baseMap == null && fallback != null)
            {
                baseMap = fallback.GetTexture("_BaseMap");
                color *= fallback.GetColor("_BaseColor");
            }
            if (baseMap != null) converted.SetTexture("_BaseMap", baseMap);
            if (normal != null)
            {
                converted.SetTexture("_BumpMap", normal);
                converted.EnableKeyword("_NORMALMAP");
            }
            if (metallic != null)
            {
                converted.SetTexture("_MetallicGlossMap", metallic);
                converted.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (occlusion != null)
            {
                converted.SetTexture("_OcclusionMap", occlusion);
                converted.EnableKeyword("_OCCLUSIONMAP");
            }
            converted.SetColor("_BaseColor", color);
            converted.SetFloat("_Smoothness", 0.32f);
            EditorUtility.SetDirty(converted);
            ConvertedMaterialCache[key] = converted;
            return converted;
        }

        private static Texture GetFirstTexture(Material material, params string[] names)
        {
            foreach (string name in names)
                if (material.HasProperty(name) && material.GetTexture(name) != null)
                    return material.GetTexture(name);
            return null;
        }

        private static Color GetFirstColor(Material material, Color fallback, params string[] names)
        {
            foreach (string name in names)
                if (material.HasProperty(name))
                    return material.GetColor(name);
            return fallback;
        }

        private static void ApplyPolyHavenMaterials(GameObject instance, string modelPath)
        {
            string directory = Path.GetDirectoryName(modelPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Invalid Poly Haven path: " + modelPath);

            string assetId = Path.GetFileNameWithoutExtension(modelPath);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] sourceSlots = renderer.sharedMaterials;
                if (sourceSlots.Length == 0)
                    sourceSlots = new Material[1];
                Material[] assigned = new Material[sourceSlots.Length];
                for (int i = 0; i < assigned.Length; i++)
                {
                    string slotName = sourceSlots[i] != null ? sourceSlots[i].name : renderer.gameObject.name;
                    string variant = DetermineVariant(assetId, slotName);
                    assigned[i] = GetPolyMaterial(assetId, directory, variant);
                }
                renderer.sharedMaterials = assigned;
            }
        }

        private static string DetermineVariant(string assetId, string slotName)
        {
            string value = (assetId + " " + slotName).ToLowerInvariant();
            if (assetId.Contains("chess_set", StringComparison.OrdinalIgnoreCase))
            {
                if (value.Contains("black")) return "pieces_black";
                if (value.Contains("white")) return "pieces_white";
                return "board";
            }
            if (assetId.Contains("fancy_picture_frame", StringComparison.OrdinalIgnoreCase))
                return value.Contains("canvas") ? "canvas" : "frame";
            if (assetId.Contains("wooden_broom", StringComparison.OrdinalIgnoreCase))
                return value.Contains("bristle") ? "bristles" : "wood";
            return "default";
        }

        private static Material GetPolyMaterial(string assetId, string directory, string variant)
        {
            string cacheKey = assetId + "_" + variant;
            if (MaterialCache.TryGetValue(cacheKey, out Material cached))
                return cached;

            string textureDirectory = directory + "/textures";
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureDirectory });
            List<string> paths = textureGuids.Select(AssetDatabase.GUIDToAssetPath).ToList();
            string token = VariantToken(assetId, variant);
            string basePath = FindTexture(paths, token, "diff");
            string normalPath = FindTexture(paths, token, "nor_gl");
            string metalPath = FindTexture(paths, token, "metal");
            string roughPath = FindTexture(paths, token, "rough");

            if (basePath == null && variant != "default")
                basePath = FindTexture(paths, string.Empty, "diff");
            if (normalPath == null && variant != "default")
                normalPath = FindTexture(paths, string.Empty, "nor_gl");
            if (metalPath == null && variant != "default")
                metalPath = FindTexture(paths, string.Empty, "metal");
            if (roughPath == null && variant != "default")
                roughPath = FindTexture(paths, string.Empty, "rough");

            SetNormalImport(normalPath);
            SetLinearImport(metalPath);
            SetLinearImport(roughPath);
            string packedPath = BuildMetallicSmoothness(assetId + "_" + variant, metalPath, roughPath);

            string materialName = "PH_" + Sanitize(assetId + "_" + variant);
            Material material = LoadOrCreateMaterial(materialName);
            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            Texture2D packed = AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath);
            material.SetTexture("_BaseMap", baseMap);
            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }
            if (packed != null)
            {
                material.SetTexture("_MetallicGlossMap", packed);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                material.SetTexture("_MetallicGlossMap", null);
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
            }
            Color tint = new Color(0.72f, 0.67f, 0.60f, 1f);
            float smoothness = 0.32f;
            if (assetId.Contains("GothicBed", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.46f, 0.36f, 0.29f, 1f);
                smoothness = 0.25f;
            }
            else if (assetId.Contains("cabinet", StringComparison.OrdinalIgnoreCase) ||
                     assetId.Contains("table", StringComparison.OrdinalIgnoreCase) ||
                     assetId.Contains("treasure_chest", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.58f, 0.45f, 0.34f, 1f);
                smoothness = 0.29f;
            }
            else if (assetId.Contains("cardboard", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.58f, 0.49f, 0.40f, 1f);
                smoothness = 0.12f;
            }
            else if (assetId.Contains("brass_pot", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.84f, 0.66f, 0.36f, 1f);
                smoothness = 0.46f;
            }
            else if (assetId.Contains("bull_head", StringComparison.OrdinalIgnoreCase) ||
                     assetId.Contains("fancy_picture_frame", StringComparison.OrdinalIgnoreCase))
            {
                tint = new Color(0.62f, 0.48f, 0.30f, 1f);
                smoothness = 0.38f;
            }
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Metallic", packed != null ? 1f : 0f);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            MaterialCache[cacheKey] = material;
            return material;
        }

        private static string VariantToken(string assetId, string variant)
        {
            if (assetId.Contains("chess_set", StringComparison.OrdinalIgnoreCase))
                return "chess_set_" + variant;
            if (assetId.Contains("fancy_picture_frame", StringComparison.OrdinalIgnoreCase))
                return variant == "canvas" ? "canvas" : "fancy_picture_frame_01";
            if (assetId.Contains("wooden_broom", StringComparison.OrdinalIgnoreCase))
                return variant == "bristles" ? "bristles" : "wooden_broom";
            return assetId.Replace("_2k", string.Empty);
        }

        private static string FindTexture(IEnumerable<string> paths, string token, string mapToken)
        {
            return paths.FirstOrDefault(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                string normalizedToken = token.ToLowerInvariant();
                if (normalizedToken == "fancy_picture_frame_01" && name.Contains("canvas"))
                    return false;
                if (normalizedToken == "wooden_broom" && name.Contains("bristles"))
                    return false;
                bool mapMatch = name.Contains(mapToken.ToLowerInvariant());
                bool tokenMatch = string.IsNullOrEmpty(token) || name.Contains(normalizedToken);
                return mapMatch && tokenMatch;
            });
        }

        private static void SetNormalImport(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            bool changed = importer.textureType != TextureImporterType.NormalMap || importer.sRGBTexture;
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.maxTextureSize = Math.Min(importer.maxTextureSize, 2048);
            if (changed)
                importer.SaveAndReimport();
        }

        private static void SetLinearImport(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            bool changed = importer.sRGBTexture || importer.maxTextureSize > 2048;
            importer.sRGBTexture = false;
            importer.maxTextureSize = Math.Min(importer.maxTextureSize, 2048);
            if (changed)
                importer.SaveAndReimport();
        }

        private static string BuildMetallicSmoothness(string name, string metalPath, string roughPath)
        {
            string outputPath = PackedTexturesFolder + "/" + Sanitize(name) + "_MetallicSmoothness.png";
            Texture2D metal = AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath);
            Texture2D rough = AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            const int size = 1024;
            Color32[] metalPixels = ReadTexturePixels(metal, size, Color.black);
            Color32[] roughPixels = ReadTexturePixels(rough, size, Color.white);
            Color32[] packedPixels = new Color32[size * size];
            for (int i = 0; i < packedPixels.Length; i++)
            {
                byte metallic = metal != null ? metalPixels[i].r : (byte)0;
                byte smoothness = rough != null ? (byte)(255 - roughPixels[i].r) : (byte)102;
                packedPixels[i] = new Color32(metallic, 0, 0, smoothness);
            }

            Texture2D packed = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            packed.SetPixels32(packedPixels);
            packed.Apply(false, false);
            string absolute = AssetPathToAbsolute(outputPath);
            File.WriteAllBytes(absolute, packed.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (importer != null)
            {
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.maxTextureSize = size;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return outputPath;
        }

        private static Color32[] ReadTexturePixels(Texture source, int size, Color fallback)
        {
            if (source == null)
                return Enumerable.Repeat((Color32)fallback, size * size).ToArray();

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            Texture2D readable = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            readable.Apply(false, false);
            Color32[] pixels = readable.GetPixels32();
            UnityEngine.Object.DestroyImmediate(readable);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            return pixels;
        }

        private static void FitHorizontal(GameObject root, float targetX, float targetZ)
        {
            Bounds bounds = GetWorldBounds(root);
            Vector3 scale = root.transform.localScale;
            if (bounds.size.x > 0.001f) scale.x *= targetX / bounds.size.x;
            if (bounds.size.z > 0.001f) scale.z *= targetZ / bounds.size.z;
            root.transform.localScale = scale;
        }

        private static void FitWallModule(GameObject root, float targetWidth, float targetHeight, float yaw)
        {
            Bounds bounds = GetWorldBounds(root);
            Vector3 scale = root.transform.localScale;
            bool rotatedSideways = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.5f;
            float currentWidth = rotatedSideways ? bounds.size.z : bounds.size.x;
            if (currentWidth > 0.001f)
                scale.x *= targetWidth / currentWidth;
            if (bounds.size.y > 0.001f)
                scale.y *= targetHeight / bounds.size.y;
            root.transform.localScale = scale;
        }

        private static void ScaleToHeight(GameObject root, float targetHeight)
        {
            Bounds bounds = GetWorldBounds(root);
            if (bounds.size.y < 0.001f)
                return;
            root.transform.localScale *= targetHeight / bounds.size.y;
        }

        private static void ScaleToMaxDimension(GameObject root, float target)
        {
            Bounds bounds = GetWorldBounds(root);
            float max = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (max < 0.001f)
                return;
            root.transform.localScale *= target / max;
        }

        private static void AlignBoundsMinY(GameObject root, float targetY)
        {
            Bounds bounds = GetWorldBounds(root);
            root.transform.position += Vector3.up * (targetY - bounds.min.y);
        }

        private static void AlignBoundsMaxY(GameObject root, float targetY)
        {
            Bounds bounds = GetWorldBounds(root);
            root.transform.position += Vector3.up * (targetY - bounds.max.y);
        }

        private static void CenterBoundsAt(GameObject root, Vector3 target)
        {
            Bounds bounds = GetWorldBounds(root);
            root.transform.position += target - bounds.center;
        }

        private static void CenterBoundsXZAt(GameObject root, Vector3 target)
        {
            Bounds bounds = GetWorldBounds(root);
            root.transform.position += new Vector3(target.x - bounds.center.x, 0f, target.z - bounds.center.z);
        }

        private static Bounds GetWorldBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one * 0.01f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void EnsureBoundsCollider(GameObject root)
        {
            if (root.GetComponentsInChildren<Collider>(true).Any(c => !c.isTrigger))
                return;

            Bounds local = GetLocalRendererBounds(root.transform);
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = local.center;
            collider.size = local.size;
        }

        private static Bounds GetLocalRendererBounds(Transform root)
        {
            MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (meshFilters.Length == 0 && skinnedRenderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            bool initialized = false;
            Bounds localBounds = new Bounds();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == null) continue;
                EncapsulateTransformedBounds(root, meshFilter.transform, meshFilter.sharedMesh.bounds, ref localBounds, ref initialized);
            }
            foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
                EncapsulateTransformedBounds(root, renderer.transform, renderer.localBounds, ref localBounds, ref initialized);
            return initialized ? localBounds : new Bounds(Vector3.zero, Vector3.one);
        }

        private static void EncapsulateTransformedBounds(Transform root, Transform source, Bounds sourceBounds,
            ref Bounds destination, ref bool initialized)
        {
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 sourceCorner = sourceBounds.center + Vector3.Scale(sourceBounds.extents, new Vector3(x, y, z));
                Vector3 local = root.InverseTransformPoint(source.TransformPoint(sourceCorner));
                if (!initialized)
                {
                    destination = new Bounds(local, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    destination.Encapsulate(local);
                }
            }
        }

        private static void BuildOpeningExperience(Transform generatedRoot, Transform room, Transform props, Vector3 roomCenter)
        {
            Camera mainCamera = Camera.main ?? UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (mainCamera == null)
                throw new InvalidOperationException("The opening sequence requires the existing Main Camera.");

            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAsset);
            TMP_FontAsset titleFont = LoadOrCreateTmpFontAsset(TitleFont, "Vestigia_Title_TMP");
            TMP_FontAsset bodyFont = LoadOrCreateTmpFontAsset(BodyFont, "Vestigia_Body_TMP");

            GameObject opening = NewChild(props, "OpeningSequence");
            GameObject playerStart = NewChild(opening.transform, "PlayerStart_Bed");
            GameObject standingPose = NewChild(playerStart.transform, "Player_Standing_Pose");
            // Stand well clear of the ornate footboard and face diagonally across the nightstand.
            // This keeps the bed readable in the first post-wake view and gives the capsule a
            // comfortable route into the room instead of only a few centimetres of clearance.
            standingPose.transform.position = roomCenter + new Vector3(-0.95f, 0f, -2.55f);
            standingPose.transform.rotation = Quaternion.Euler(0f, -62.8f, 0f);

            GameObject player = NewChild(playerStart.transform, "Player");
            player.transform.SetPositionAndRotation(standingPose.transform.position, standingPose.transform.rotation);
            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.78f;
            characterController.radius = 0.32f;
            characterController.center = new Vector3(0f, 0.89f, 0f);
            characterController.stepOffset = 0.30f;
            characterController.slopeLimit = 48f;
            characterController.skinWidth = 0.045f;

            GameObject cameraPitch = NewChild(player.transform, "FirstPerson_Camera_Pitch");
            cameraPitch.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            cameraPitch.transform.localRotation = Quaternion.Euler(25.1f, 0f, 0f);
            mainCamera.transform.SetParent(cameraPitch.transform, false);
            mainCamera.transform.localPosition = Vector3.zero;
            mainCamera.transform.localRotation = Quaternion.identity;
            mainCamera.fieldOfView = 54f;
            mainCamera.cullingMask &= ~(1 << 31);

            VestigiaFirstPersonController controller = player.AddComponent<VestigiaFirstPersonController>();
            controller.Configure(mainCamera, cameraPitch.transform, actions);
            controller.SetControlLocked(true);

            GameObject femaleBody = InstantiateAsset(FemaleCharacter, player.transform, "FemaleBody_Ch22");
            femaleBody.transform.localPosition = Vector3.zero;
            femaleBody.transform.localRotation = Quaternion.identity;
            ScaleToHeight(femaleBody, 1.68f);
            AlignBoundsMinY(femaleBody, player.transform.position.y);
            SetLayerRecursively(femaleBody, 31);

            GameObject bodyStandingPose = NewChild(player.transform, "FemaleBody_Standing_Pose");
            bodyStandingPose.transform.localPosition = femaleBody.transform.localPosition;
            bodyStandingPose.transform.localRotation = femaleBody.transform.localRotation;
            GameObject bodyLyingPose = NewChild(opening.transform, "FemaleBody_Lying_Pose");
            bodyLyingPose.transform.position = roomCenter + new Vector3(-1.70f, 0.82f, -0.48f);
            bodyLyingPose.transform.rotation = Quaternion.Euler(0f, -8f, 90f);

            GameObject wakeSequence = NewChild(opening.transform, "WakeUpCameraSequence");
            GameObject cameraStart = NewChild(wakeSequence.transform, "WakeCamera_Start_Near_Pillow");
            cameraStart.transform.position = roomCenter + new Vector3(-2.42f, 1.03f, -0.68f);
            cameraStart.transform.LookAt(roomCenter + new Vector3(-2.60f, 0.76f, -1.70f));
            GameObject cameraEnd = NewChild(cameraPitch.transform, "WakeCamera_End_Standing");
            cameraEnd.transform.localPosition = Vector3.zero;
            cameraEnd.transform.localRotation = Quaternion.identity;

            CreatePaper(opening.transform, "Bedside_Note_Parchment_Underlay",
                new Vector3(-2.60f, 0.718f, -1.70f), new Vector2(0.48f, 0.36f), -8f);
            GameObject physicalNote = PlacePoly(Notebook, opening.transform, "BedsideNote",
                new Vector3(-2.60f, 0.73f, -1.70f), -8f, 0.40f, true, 0.73f);
            VestigiaPhysicalNote physicalNoteData = physicalNote.AddComponent<VestigiaPhysicalNote>();
            physicalNoteData.ResetToIntroNote();

            GameObject introUi = CreateScreenCanvas(opening.transform, "IntroTitleUI", 120);
            GameObject blackFade = CreateUIImage(introUi.transform, "Opening_Black_Fade",
                new Color(0.008f, 0.004f, 0.003f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CanvasGroup blackGroup = blackFade.AddComponent<CanvasGroup>();
            blackGroup.alpha = 1f;
            blackGroup.blocksRaycasts = true;

            GameObject titleGroupObject = CreateUIContainer(introUi.transform, "Opening_Title_Group",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CanvasGroup titleGroup = titleGroupObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            TextMeshProUGUI titleText = CreateTMPText(titleGroupObject.transform, "VESTIGIA_Title",
                "VESTIGIA", titleFont, 96f, new Color(0.78f, 0.61f, 0.36f, 1f),
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(1280f, 150f), new Vector2(0f, 45f));
            titleText.characterSpacing = 8f;
            TextMeshProUGUI subtitleText = CreateTMPText(titleGroupObject.transform, "Opening_Subtitle",
                "Every room remembers.", bodyFont, 29f, new Color(0.72f, 0.62f, 0.48f, 1f),
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(900f, 70f), new Vector2(0f, -70f));

            GameObject interaction = NewChild(opening.transform, "BedsideNoteInteraction");
            GameObject noteCanvas = CreateScreenCanvas(interaction.transform, "BedsideNote_UI", 130);
            GameObject promptPanel = CreateUIImage(noteCanvas.transform, "Read_Prompt",
                new Color(0.035f, 0.022f, 0.016f, 0.94f), new Vector2(0.5f, 0.14f), new Vector2(0.5f, 0.14f),
                new Vector2(-270f, -36f), new Vector2(270f, 36f));
            CanvasGroup promptGroup = promptPanel.AddComponent<CanvasGroup>();
            promptGroup.alpha = 0f;
            TextMeshProUGUI promptText = CreateTMPText(promptPanel.transform, "Prompt_Text",
                VestigiaPhysicalNote.DefaultPrompt, bodyFont, 27f, new Color(0.96f, 0.82f, 0.56f, 1f),
                TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject noteGroupObject = CreateUIContainer(noteCanvas.transform, "Readable_Note_Group",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CanvasGroup noteGroup = noteGroupObject.AddComponent<CanvasGroup>();
            noteGroup.alpha = 0f;
            noteGroup.blocksRaycasts = false;
            CreateUIImage(noteGroupObject.transform, "Note_Backdrop", new Color(0.008f, 0.005f, 0.004f, 0.88f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateUIImage(noteGroupObject.transform, "Parchment_Border", new Color(0.20f, 0.105f, 0.045f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-470f, -430f), new Vector2(470f, 430f));
            GameObject parchment = CreateUIImage(noteGroupObject.transform, "Aged_Parchment",
                new Color(0.70f, 0.56f, 0.36f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-450f, -410f), new Vector2(450f, 410f));
            TextMeshProUGUI noteTitle = CreateTMPText(parchment.transform, "Note_Title", "VESTIGIA", titleFont, 54f,
                new Color(0.12f, 0.065f, 0.035f, 1f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(740f, 80f), new Vector2(0f, -72f));
            noteTitle.characterSpacing = 3f;
            TextMeshProUGUI noteBody = CreateTMPText(parchment.transform, "Note_Body", VestigiaNoteData.IntroBody,
                bodyFont, 27f, new Color(0.105f, 0.06f, 0.035f, 1f), TextAlignmentOptions.TopLeft,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(750f, 625f), new Vector2(0f, -45f));
            noteBody.lineSpacing = 7f;
            CreateTMPText(parchment.transform, "Close_Hint", "E / Escape to close", bodyFont, 19f,
                new Color(0.25f, 0.14f, 0.08f, 0.85f), TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(500f, 42f), new Vector2(0f, 34f));

            VestigiaNoteReader reader = interaction.AddComponent<VestigiaNoteReader>();
            reader.Configure(mainCamera, controller, promptGroup, promptText, noteGroup, noteTitle, noteBody, actions);
            VestigiaOpeningSequence sequence = wakeSequence.AddComponent<VestigiaOpeningSequence>();
            sequence.Configure(controller, standingPose.transform, mainCamera, cameraStart.transform, cameraEnd.transform,
                femaleBody.transform, bodyLyingPose.transform, bodyStandingPose.transform, blackGroup, titleGroup,
                titleText, subtitleText, reader);

            // Save the authored scene in its true opening pose. The first-person camera excludes layer 31,
            // so the body cannot obscure the view while still existing visibly for other cameras.
            femaleBody.transform.SetPositionAndRotation(bodyLyingPose.transform.position, bodyLyingPose.transform.rotation);
        }

        private static GameObject CreateScreenCanvas(Transform parent, string name, int sortingOrder)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasObject;
        }

        private static GameObject CreateUIContainer(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject container = new GameObject(name, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            SetRect(container.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
            return container;
        }

        private static GameObject CreateUIImage(Transform parent, string name, Color color, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            SetRect(imageObject.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return imageObject;
        }

        private static TextMeshProUGUI CreateTMPText(Transform parent, string name, string content, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (anchorMin == anchorMax)
            {
                rect.sizeDelta = sizeDelta;
                rect.anchoredPosition = anchoredPosition;
            }
            else
            {
                rect.offsetMin = sizeDelta;
                rect.offsetMax = anchoredPosition;
            }

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static TMP_FontAsset LoadOrCreateTmpFontAsset(string sourcePath, string assetName)
        {
            string path = GeneratedRoot + "/Fonts/" + assetName + ".asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;

            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
                throw new FileNotFoundException("Could not load the source font at " + sourcePath);

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(source);
            fontAsset.name = assetName;
            AssetDatabase.CreateAsset(fontAsset, path);
            if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
            {
                fontAsset.material.name = assetName + "_Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }
            foreach (Texture2D texture in fontAsset.atlasTextures ?? Array.Empty<Texture2D>())
            {
                if (texture != null && !AssetDatabase.Contains(texture))
                {
                    texture.name = assetName + "_Atlas";
                    AssetDatabase.AddObjectToAsset(texture, fontAsset);
                }
            }
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                item.gameObject.layer = layer;
        }

        private static void EnsureVestigiaFirstInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(scene => !string.Equals(scene.path, TargetScenePath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(TargetScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void StaticizeEnvironment(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.GetComponent<Light>() != null)
                    continue;
                if (transform.name == "Door")
                    continue;
                if (transform.GetComponentInParent<Rigidbody>() != null || transform.GetComponent<Animator>() != null)
                    continue;
                GameObjectUtility.SetStaticEditorFlags(transform.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
        }

        private static string Validate(GameObject root, Vector3[] centers)
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            HashSet<string> expectedRootChildren = new HashSet<string>(RoomNames.Concat(new[] { "Shared_Lighting", "Doors_And_Connections" }));
            string[] actualRootChildren = root.transform.Cast<Transform>().Select(t => t.name).ToArray();
            if (actualRootChildren.Length != 6 || !expectedRootChildren.SetEquals(actualRootChildren))
                errors.Add("Root hierarchy must contain exactly the four rooms plus Shared_Lighting and Doors_And_Connections.");

            foreach (string roomName in RoomNames)
            {
                Transform room = root.transform.Find(roomName);
                if (room == null)
                {
                    errors.Add("Missing room " + roomName);
                    continue;
                }
                HashSet<string> required = new HashSet<string> { "Structure", "Furniture", "Props", "Lighting" };
                string[] children = room.Cast<Transform>().Select(t => t.name).ToArray();
                if (children.Length != 4 || !required.SetEquals(children))
                    errors.Add(roomName + " must contain exactly Structure, Furniture, Props, and Lighting.");
            }

            int roomCount = root.transform.Cast<Transform>().Count(t => t.name.StartsWith("Room_", StringComparison.Ordinal));
            if (roomCount != 4)
                errors.Add("Expected exactly four room roots, found " + roomCount + ".");

            Transform doors = root.transform.Find("Doors_And_Connections");
            int doorwayCount = doors != null ? doors.childCount : 0;
            if (doorwayCount != 5)
                errors.Add("Expected five doorway assemblies (entrance, 3 connections, exit), found " + doorwayCount + ".");

            int missingMaterials = 0;
            int errorShaders = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) missingMaterials++;
                    else if (material.shader == null || material.shader.name.Contains("InternalError", StringComparison.OrdinalIgnoreCase)) errorShaders++;
                }
            }
            if (missingMaterials > 0) errors.Add("Renderers with missing material slots: " + missingMaterials);
            if (errorShaders > 0) errors.Add("Material slots using error shaders: " + errorShaders);

            int missingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            if (missingScripts > 0)
                errors.Add("Missing script components under generated hierarchy: " + missingScripts);

            List<string> forbidden = new List<string>();
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(item.gameObject);
                if (source == null) continue;
                string path = AssetDatabase.GetAssetPath(source).Replace('\\', '/');
                string lower = path.ToLowerInvariant();
                bool forbiddenDemoCharacter = lower.Contains("_3dstealthgame") && lower.Contains("/characters/");
                if (lower.Contains("tutorial_demo") || forbiddenDemoCharacter || lower.Contains("/audio/") ||
                    lower.Contains("key&doors/key") || lower.Contains("weapon"))
                    forbidden.Add(path);
            }
            if (forbidden.Count > 0)
                errors.Add("Forbidden asset sources detected: " + string.Join(", ", forbidden.Distinct()));

            int colliderCount = root.GetComponentsInChildren<Collider>(true).Count(c => !c.isTrigger);
            if (colliderCount < 80)
                warnings.Add("Collider count is lower than expected for the modular shell: " + colliderCount);

            Physics.SyncTransforms();
            for (int i = 0; i < 3; i++)
            {
                Vector3 boundary = new Vector3(centers[0].x, 1.25f, centers[i].z + RoomSize * 0.5f);
                Collider[] overlaps = Physics.OverlapCapsule(boundary + Vector3.down * 0.85f,
                    boundary + Vector3.up * 0.42f, 0.26f,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (overlaps.Any(c => c.transform.IsChildOf(root.transform)))
                    warnings.Add($"Connection {i + 1} clearance probe overlaps: " + string.Join(", ", overlaps.Select(c => c.name).Distinct()));
            }

            List<string> blockedPath = ValidateCapsuleWalkthrough(root, centers);
            if (blockedPath.Count > 0)
                errors.Add("First-person capsule clearance path is blocked at: " + string.Join("; ", blockedPath.Take(8)));

            bool hasPlayer = GameObject.Find("Player") != null ||
                             UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0;
            if (!hasPlayer)
                warnings.Add("No player/controller exists in the source scene, so an actual controller walkthrough could not be performed.");

            StringBuilder report = new StringBuilder();
            report.AppendLine("VESTIGIA FOUR ROOMS VALIDATION");
            report.AppendLine("Scene: " + SceneManager.GetActiveScene().path);
            report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("Room roots: " + roomCount);
            report.AppendLine("Doorway assemblies: " + doorwayCount + " (3 open inter-room, 2 closed boundary doors)");
            report.AppendLine("Non-trigger colliders: " + colliderCount);
            report.AppendLine("Renderers: " + root.GetComponentsInChildren<Renderer>(true).Length);
            report.AppendLine("Lights: " + root.GetComponentsInChildren<Light>(true).Length);
            report.AppendLine("Capsule walkthrough proxy: " + (blockedPath.Count == 0 ? "PASS" : "BLOCKED"));
            report.AppendLine("Errors: " + errors.Count);
            foreach (string error in errors) report.AppendLine("ERROR: " + error);
            report.AppendLine("Warnings: " + warnings.Count);
            foreach (string warning in warnings) report.AppendLine("WARNING: " + warning);
            if (errors.Count > 0)
                report.AppendLine("RESULT: VALIDATION FAILED");
            else if (!hasPlayer)
                report.AppendLine("RESULT: STRUCTURAL PASS; ACTUAL PLAYER WALKTHROUGH BLOCKED (NO SOURCE CONTROLLER)");
            else
                report.AppendLine("RESULT: STRUCTURAL AND PLAYER-PRESENCE VALIDATION PASSED");
            return report.ToString();
        }

        private static List<string> ValidateCapsuleWalkthrough(GameObject root, Vector3[] centers)
        {
            float x = centers[0].x;
            const float roomApproach = 3.02f;
            const float bypassX = 1.62f;
            const float bypassZ = 2.18f;
            Vector3[] route =
            {
                new Vector3(x, 0f, centers[0].z - roomApproach),
                new Vector3(x, 0f, centers[0].z + roomApproach),
                new Vector3(x, 0f, centers[1].z - roomApproach),
                new Vector3(x, 0f, centers[1].z + roomApproach),
                new Vector3(x, 0f, centers[2].z - roomApproach),
                new Vector3(x + bypassX, 0f, centers[2].z - bypassZ),
                new Vector3(x + bypassX, 0f, centers[2].z + bypassZ),
                new Vector3(x, 0f, centers[2].z + roomApproach),
                new Vector3(x, 0f, centers[3].z - roomApproach),
                new Vector3(x + bypassX, 0f, centers[3].z - bypassZ),
                new Vector3(x + bypassX, 0f, centers[3].z + bypassZ),
                new Vector3(x + 2.0f, 0f, centers[3].z + roomApproach)
            };

            List<string> blocked = new List<string>();
            for (int segment = 0; segment < route.Length - 1; segment++)
            {
                float distance = Vector3.Distance(route[segment], route[segment + 1]);
                int samples = Mathf.Max(2, Mathf.CeilToInt(distance / 0.35f));
                for (int sample = 0; sample <= samples; sample++)
                {
                    Vector3 point = Vector3.Lerp(route[segment], route[segment + 1], sample / (float)samples);
                    Vector3 bottom = point + Vector3.up * 0.38f;
                    Vector3 top = point + Vector3.up * 1.62f;
                    Collider[] overlaps = Physics.OverlapCapsule(bottom, top, 0.32f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                    Collider blocker = overlaps.FirstOrDefault(c => c.transform.IsChildOf(root.transform) &&
                        c.GetComponent<CharacterController>() == null &&
                        !c.name.Contains("Floor", StringComparison.OrdinalIgnoreCase) &&
                        !c.name.Contains("Rug", StringComparison.OrdinalIgnoreCase));
                    if (blocker != null)
                    {
                        blocked.Add($"segment {segment + 1}, {point.x:0.00}/{point.z:0.00} ({blocker.name})");
                        break;
                    }
                }
            }
            return blocked;
        }

        private static string BuildAssetUsageReport()
        {
            return @"VESTIGIA FOUR ROOMS — ASSET USAGE

STRUCTURE / ALL ROOMS
- Assets/7th Side/Modular Prison Asset Pack/Prefabs/Floors/Floor Tile.prefab
- Assets/7th Side/Modular Prison Asset Pack/Prefabs/Floors/Double Sided Floor Tile.prefab
- Assets/_3DStealthGame/Prefabs/Environment/Walls/Wall_Straight_A.prefab
- Assets/_3DStealthGame/Prefabs/Environment/Walls/Wall_Corner_A.prefab
- Assets/7th Side/Modular Prison Asset Pack/Prefabs/Doors/Doorway.prefab (contains Wood Door)
- Compact 7.5 m rooms are fully over-materialed with generated URP Lit worn wallpaper, dark wood trim, and wood flooring.

ROOM 1 — MAIN / START
- PolyHaven: GothicBed, alarm_clock, brass_pot, fancy_picture_frame
- _3DStealthGame environment only: Bedside_Table, Wardrobe, SmallTable_B, Rug_Diningroom, safe picture frames
- Resources: chair_big_01 red tufted Victorian chair
- Generated details: physical introduction note, book shelf, bedside lamp, wall sconce, warm mixed fill light

ROOM 2 — MEMORY
- PolyHaven: chinese_tea_table, chinese_cabinet, chess_set, alarm_clock, brass_pot, binder_notebook, fancy_picture_frame
- _3DStealthGame environment only: Rug_Diningroom, grandfather Clock, safe picture frames
- Resources: chair_big_01 and matching stool
- Generated details: potted plant, warm table lamp, wall sconce, mixed fill light

ROOM 3 — STUDY
- PolyHaven: WoodenTable_01, WoodenTable_02, chinese_cabinet, binder_notebooks, alarm_clock, treasure_chest, cardboard_box, brass_pot, fancy_picture_frame
- _3DStealthGame environment only: Rug_Diningroom, Candlestick, safe picture frames
- Resources: matching red upholstered stool
- Generated details: papers, antique globe, book shelf, green banker desk lamp, wall sconce, warm mixed fill light

ROOM 4 — FINAL
- PolyHaven: treasure_chest, cardboard_boxes, brass_pot, bull_head, binder_notebook, fancy_picture_frame
- _3DStealthGame environment only: Fireplace, Wardrobe, SmallTable_A/B, Candlestick, Rug_Diningroom, safe/ghost picture frames
- Resources: chair_big_01 red tufted Victorian chair
- Generated details: central haunted portrait display, puzzle pedestal, wall sconce, and darker mixed lighting

OPENING
- Assets/Characters/Ch22_nonPBR.fbx female body, existing Main Camera, existing InputSystem_Actions
- TextMeshPro title, reusable physical-note reader, and camera-only wake fallback because no suitable wake animation existed

EXPLICITLY NOT USED
- No _3DStealthGame Tutorial_Demo, player, controller, audio, animation, character, enemy, monster, key, weapon, or sample gameplay content.
- No General/Light prefab (it contains audio and a missing script reference).
- No doll was added because no appropriate imported doll asset exists.
";
        }

        private static void RenderPreviews(Vector3[] centers)
        {
            EnsureFolder(ValidationFolder);
            Vector3[] localCamera =
            {
                new Vector3(2.82f, 1.52f, -2.92f),
                new Vector3(0f, 1.54f, -3.12f),
                new Vector3(0f, 1.54f, -3.12f),
                new Vector3(0f, 1.52f, -3.12f)
            };
            Vector3[] localTarget =
            {
                new Vector3(-0.72f, 1.04f, 0.38f),
                new Vector3(0f, 1.06f, 1.32f),
                new Vector3(-0.18f, 1.04f, 1.20f),
                new Vector3(0f, 1.04f, 1.38f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject cameraObject = new GameObject("__VestigiaPreviewCamera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.cullingMask &= ~(1 << 31);
                camera.transform.position = centers[i] + localCamera[i];
                camera.transform.LookAt(centers[i] + localTarget[i]);
                camera.fieldOfView = 50f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 60f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.01f, 0.006f, 0.004f, 1f);
                camera.allowHDR = true;

                const int previewWidth = 960;
                const int previewHeight = 720;
                RenderTexture renderTexture = new RenderTexture(previewWidth, previewHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                Texture2D image = new Texture2D(previewWidth, previewHeight, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, previewWidth, previewHeight), 0, 0);
                image.Apply();
                string assetPath = ValidationFolder + $"/Room_{i + 1:00}_Preview.png";
                File.WriteAllBytes(AssetPathToAbsolute(assetPath), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.active = previous;
                camera.targetTexture = null;
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void WriteTextAsset(string assetPath, string content)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            File.WriteAllText(AssetPathToAbsolute(assetPath), content, Encoding.UTF8);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static string AssetPathToAbsolute(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolute = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute) ?? projectRoot);
            return absolute;
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value.Replace(' ', '_').Replace("(Instance)", string.Empty);
        }
    }
}
#endif
