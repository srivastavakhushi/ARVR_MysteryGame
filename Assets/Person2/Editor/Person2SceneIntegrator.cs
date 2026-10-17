using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using CrimeSceneVR.Core;
using CrimeSceneVR.Person2.Evidence;
using CrimeSceneVR.Person2.Interaction;
using CrimeSceneVR.Person3.Session;
using CrimeSceneVR.Person3.Telemetry;
using CrimeSceneVR.Person3.Export;
using CrimeSceneVR.Person3.Analytics;

namespace CrimeSceneVR.Person2.Editor
{
    /// <summary>
    /// Phase 2 automated scene integration and prefab authoring tool for Person 2.
    /// Runs automatically on InitializeOnLoad or via Unity Editor menu.
    /// </summary>
    [InitializeOnLoad]
    public static class Person2SceneIntegrator
    {
        private const string SCENE_PATH = "Assets/Scenes/ZNS3D/Vintage Living Room Game Pack/Demo/Demo_Scene.unity";
        private const string PREFAB_DIR = "Assets/Person2/Prefabs";
        private const string PREFAB_PATH = "Assets/Person2/Prefabs/EvidenceTagMarker.prefab";
        private const string MESH_PATH = "Assets/Person2/Meshes/EvidenceTentMesh.asset";
        private const string MAT_DIR = "Assets/Person2/Materials";
        private const string YELLOW_MAT_PATH = "Assets/Person2/Materials/M_ForensicTentYellow.mat";

        private static bool _hasExecutedThisSession = false;

        static Person2SceneIntegrator()
        {
            EditorApplication.delayCall += () =>
            {
                if (!_hasExecutedThisSession && !Application.isPlaying)
                {
                    _hasExecutedThisSession = true;
                    ExecutePhase2Setup();
                }
            };
        }

        [MenuItem("Crime Scene VR/Person 2/Execute Phase 2 Scene Setup")]
        public static void ExecutePhase2SetupManual()
        {
            ExecutePhase2Setup();
        }

        public static void ExecutePhase2Setup()
        {
            Debug.Log("<color=cyan><b>[Person 2 Phase 2] Starting Scene Setup and Prefab Authoring...</b></color>");

            EnsureDirectoriesExist();
            GameObject markerPrefab = AuthorEvidenceMarkerPrefab();
            SetupDemoScene(markerPrefab);

            Debug.Log("<color=green><b>[Person 2 Phase 2] Setup completed successfully and verified!</b></color>");
        }

        private static void EnsureDirectoriesExist()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Person2/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Person2", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Person2/Materials"))
                AssetDatabase.CreateFolder("Assets/Person2", "Materials");
            if (!AssetDatabase.IsValidFolder("Assets/Person2/Meshes"))
                AssetDatabase.CreateFolder("Assets/Person2", "Meshes");
        }

        #region 1. Prefab Authoring
        private static GameObject AuthorEvidenceMarkerPrefab()
        {
            // 1. Create or load tent mesh
            Mesh tentMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MESH_PATH);
            if (tentMesh == null)
            {
                tentMesh = GenerateForensicTentMesh();
                AssetDatabase.CreateAsset(tentMesh, MESH_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Person 2] Generated and saved tent mesh to {MESH_PATH}");
            }

            // 2. Create or load forensic yellow material
            Material yellowMat = AssetDatabase.LoadAssetAtPath<Material>(YELLOW_MAT_PATH);
            if (yellowMat == null)
            {
                Material sampleMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/ZNS3D/Vintage Living Room Game Pack/Materials/Mat-Antique_Light.mat");
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") 
                                ?? (sampleMat != null ? sampleMat.shader : null)
                                ?? Shader.Find("Universal Render Pipeline/Unlit") 
                                ?? Shader.Find("Standard");
                yellowMat = new Material(urpShader)
                {
                    color = new Color(1.0f, 0.86f, 0.05f, 1f) // Classic forensic high-vis yellow
                };
                if (yellowMat.HasProperty("_Smoothness")) yellowMat.SetFloat("_Smoothness", 0.35f);
                AssetDatabase.CreateAsset(yellowMat, YELLOW_MAT_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Person 2] Created forensic yellow material at {YELLOW_MAT_PATH}");
            }

            // 3. Build Prefab GameObject Hierarchy
            GameObject rootGo = new GameObject("EvidenceTagMarker");
            BoxCollider col = rootGo.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.04f, 0f);
            col.size = new Vector3(0.08f, 0.08f, 0.10f);

            var tagMarker = rootGo.AddComponent<EvidenceTagMarker>();

            // Child A: Tent Visual Mesh
            GameObject tentGo = new GameObject("TentMesh");
            tentGo.transform.SetParent(rootGo.transform, false);
            var mf = tentGo.AddComponent<MeshFilter>();
            mf.sharedMesh = tentMesh;
            var mr = tentGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = yellowMat;

            // Child B: Number Text (Front Slanted Face)
            GameObject frontTextGo = new GameObject("NumberText_Front");
            frontTextGo.transform.SetParent(rootGo.transform, false);
            frontTextGo.transform.localPosition = new Vector3(0f, 0.038f, 0.026f);
            frontTextGo.transform.localRotation = Quaternion.Euler(32f, 0f, 0f);
            var tmpFront = frontTextGo.AddComponent<TextMeshPro>();
            tmpFront.text = "1";
            tmpFront.fontSize = 3.6f;
            tmpFront.alignment = TextAlignmentOptions.Center;
            tmpFront.color = Color.black;
            tmpFront.rectTransform.sizeDelta = new Vector2(0.07f, 0.06f);

            // Child C: Number Text (Back Slanted Face)
            GameObject backTextGo = new GameObject("NumberText_Back");
            backTextGo.transform.SetParent(rootGo.transform, false);
            backTextGo.transform.localPosition = new Vector3(0f, 0.038f, -0.026f);
            backTextGo.transform.localRotation = Quaternion.Euler(32f, 180f, 0f);
            var tmpBack = backTextGo.AddComponent<TextMeshPro>();
            tmpBack.text = "1";
            tmpBack.fontSize = 3.6f;
            tmpBack.alignment = TextAlignmentOptions.Center;
            tmpBack.color = Color.black;
            tmpBack.rectTransform.sizeDelta = new Vector2(0.07f, 0.06f);

            // Child D: Error Readout Text (Top Ridge)
            GameObject errorTextGo = new GameObject("ErrorText");
            errorTextGo.transform.SetParent(rootGo.transform, false);
            errorTextGo.transform.localPosition = new Vector3(0f, 0.088f, 0f);
            errorTextGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tmpError = errorTextGo.AddComponent<TextMeshPro>();
            tmpError.text = "0.0cm ✓";
            tmpError.fontSize = 2.0f;
            tmpError.alignment = TextAlignmentOptions.Center;
            tmpError.color = new Color(0.1f, 0.85f, 0.2f);
            tmpError.rectTransform.sizeDelta = new Vector2(0.12f, 0.04f);

            // 4. Wire Serialized Fields on EvidenceTagMarker
            SerializedObject so = new SerializedObject(tagMarker);
            so.FindProperty("_numberText").objectReferenceValue = tmpFront;
            so.FindProperty("_errorText").objectReferenceValue = tmpError;
            so.FindProperty("_tentRenderer").objectReferenceValue = mr;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 5. Save as Prefab Asset
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(rootGo, PREFAB_PATH);
            UnityEngine.Object.DestroyImmediate(rootGo);

            Debug.Log($"[Person 2] Successfully authored prefab: {PREFAB_PATH}");
            return prefabAsset;
        }

        private static Mesh GenerateForensicTentMesh()
        {
            Mesh mesh = new Mesh { name = "EvidenceMarkerTent" };

            // Width along X: 0.07m, Length along Z: 0.09m, Ridge Peak Y: 0.075m
            float halfW = 0.035f;
            float halfL = 0.045f;
            float height = 0.075f;

            Vector3[] vertices = new Vector3[]
            {
                // Front Slant Face (indices 0..3)
                new Vector3(-halfW, 0f, halfL),
                new Vector3( halfW, 0f, halfL),
                new Vector3( halfW, height, 0f),
                new Vector3(-halfW, height, 0f),

                // Back Slant Face (indices 4..7)
                new Vector3( halfW, 0f, -halfL),
                new Vector3(-halfW, 0f, -halfL),
                new Vector3(-halfW, height, 0f),
                new Vector3( halfW, height, 0f),

                // Left Gable Face (indices 8..10)
                new Vector3(-halfW, 0f, -halfL),
                new Vector3(-halfW, 0f,  halfL),
                new Vector3(-halfW, height, 0f),

                // Right Gable Face (indices 11..13)
                new Vector3(halfW, 0f,  halfL),
                new Vector3(halfW, 0f, -halfL),
                new Vector3(halfW, height, 0f),

                // Bottom Face (indices 14..17)
                new Vector3(-halfW, 0f, -halfL),
                new Vector3( halfW, 0f, -halfL),
                new Vector3( halfW, 0f,  halfL),
                new Vector3(-halfW, 0f,  halfL)
            };

            int[] triangles = new int[]
            {
                0, 1, 2,  0, 2, 3,       // Front
                4, 5, 6,  4, 6, 7,       // Back
                8, 9, 10,                 // Left
                11, 12, 13,               // Right
                14, 15, 16, 14, 16, 17   // Bottom
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
                new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,1),
                new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,1),
                new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        #endregion

        #region 2. Scene Setup
        private static void SetupDemoScene(GameObject markerPrefab)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != SCENE_PATH)
            {
                activeScene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            // A. Ensure Person 3 Systems Root
            EnsurePerson3Systems();

            // B. Ensure Person 1 VR Rig References
            Transform rightHand = EnsureVRRigReferences();

            // C. Configure [Person2] Evidence Manager
            GameObject mgrGo = GameObject.Find("[Person2] Evidence Manager");
            if (mgrGo == null)
            {
                mgrGo = new GameObject("[Person2] Evidence Manager");
                Undo.RegisterCreatedObjectUndo(mgrGo, "Create Evidence Manager");
            }
            var evidenceManager = mgrGo.GetComponent<EvidenceManager>();
            if (evidenceManager == null) evidenceManager = mgrGo.AddComponent<EvidenceManager>();

            // D. Configure Canonical 8 Evidence Items
            GameObject itemsRoot = GameObject.Find("[Person2] Canonical Evidence Items");
            if (itemsRoot == null)
            {
                itemsRoot = new GameObject("[Person2] Canonical Evidence Items");
                itemsRoot.transform.SetParent(mgrGo.transform, false);
                Undo.RegisterCreatedObjectUndo(itemsRoot, "Create Canonical Evidence Root");
            }

            SetupCanonicalItems(itemsRoot.transform);

            // E. Configure VR & Desktop Interactor
            SetupEvidenceInteractor(rightHand, markerPrefab);

            // F. Setup Phase 3 Exit Trigger & Clipboard UI
            SetupInvestigationExitTrigger();
            SetupInvestigationClipboard(rightHand);

            // G. Refresh and link evidence list
            evidenceManager.RefreshRegisteredEvidence();
            Debug.Log($"[Person 2] EvidenceManager registered {evidenceManager.TotalEvidenceCount}/8 items.");

            // H. Save Scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Person 2] Saved scene cleanly: {SCENE_PATH}");
        }

        private static void EnsurePerson3Systems()
        {
            GameObject p3 = GameObject.Find("[Person3] Systems");
            if (p3 == null)
            {
                p3 = new GameObject("[Person3] Systems");
                Undo.RegisterCreatedObjectUndo(p3, "Create Person3 Systems");
            }

            if (p3.GetComponent<SessionManager>() == null) p3.AddComponent<SessionManager>();
            if (p3.GetComponent<TelemetryRecorder>() == null) p3.AddComponent<TelemetryRecorder>();
            if (p3.GetComponent<SessionExporter>() == null) p3.AddComponent<SessionExporter>();
            if (p3.GetComponent<TimelineManager>() == null) p3.AddComponent<TimelineManager>();

            Debug.Log("[Person 2] Verified [Person3] Systems components.");
        }

        private static Transform EnsureVRRigReferences()
        {
            GameObject rigGo = GameObject.Find("[BuildingBlock] Camera Rig");
            if (rigGo == null)
            {
                Debug.LogWarning("[Person 2] '[BuildingBlock] Camera Rig' not found in scene. Using fallback interactor anchor.");
                return null;
            }

            var rigRef = rigGo.GetComponent<VRRigReferences>();
            if (rigRef == null) rigRef = rigGo.AddComponent<VRRigReferences>();
            rigRef.ResolveReferences();

            Transform rightHand = rigRef.RightHandAnchor;
            if (rightHand == null)
            {
                rightHand = rigGo.transform.Find("TrackingSpace/RightHandAnchor");
            }

            Debug.Log($"[Person 2] Linked VRRigReferences on {rigGo.name}. RightHandAnchor: {rightHand?.name ?? "null"}");
            return rightHand;
        }

        private static void SetupEvidenceInteractor(Transform rightHand, GameObject markerPrefab)
        {
            GameObject interactorHost = (rightHand != null) ? rightHand.gameObject : GameObject.Find("[BuildingBlock] Camera Rig");
            if (interactorHost == null)
            {
                interactorHost = GameObject.Find("[Person2] Evidence Manager");
            }

            var interactor = interactorHost.GetComponent<VREvidenceInteractor>();
            if (interactor == null)
            {
                interactor = interactorHost.AddComponent<VREvidenceInteractor>();
            }

            SerializedObject so = new SerializedObject(interactor);
            so.FindProperty("_markerPrefab").objectReferenceValue = markerPrefab;
            so.FindProperty("_maxRayDistance").floatValue = 15.0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[Person 2] Configured VREvidenceInteractor on {interactorHost.name} with marker prefab assigned.");
        }

        private static void SetupCanonicalItems(Transform parent)
        {
            foreach (var def in EvidenceConstants.CanonicalItems)
            {
                string objName = $"{def.Id}_{def.Name.Replace(" ", "_").Replace("/", "_")}";
                Transform existing = parent.Find(objName);
                GameObject itemGo = existing != null ? existing.gameObject : new GameObject(objName);
                itemGo.transform.SetParent(parent, false);
                itemGo.transform.position = def.CanonicalPosition;

                var evidence = itemGo.GetComponent<EvidenceItem>();
                if (evidence == null) evidence = itemGo.AddComponent<EvidenceItem>();
                evidence.Configure(def.Id, def.Name, def.Description, def.CanonicalPosition);

                // Configure visual mesh and collider tailored for each canonical prop
                ConfigureEvidenceVisualsAndCollider(def.Id, itemGo);
            }
        }

        private static void ConfigureEvidenceVisualsAndCollider(string id, GameObject go)
        {
            switch (id)
            {
                case "EV-001": // Footwear Impression: (4.00, 0.02, 0.50)
                {
                    var col = go.GetComponent<BoxCollider>();
                    if (col == null) col = go.AddComponent<BoxCollider>();
                    col.center = Vector3.zero;
                    col.size = new Vector3(0.45f, 0.25f, 0.35f);
                    AttachForensicBaseRing(go, new Vector3(0.42f, 0.005f, 0.42f));
                    AttachPrimitiveVisual(go, PrimitiveType.Cube, new Vector3(0.35f, 0.035f, 0.18f), new Color(0.32f, 0.22f, 0.15f)); // Muddy boot tread
                    break;
                }
                case "EV-002": // Broken Glass: (-0.02, 0.68, 7.85)
                {
                    var col = go.GetComponent<BoxCollider>();
                    if (col == null) col = go.AddComponent<BoxCollider>();
                    col.center = Vector3.zero;
                    col.size = new Vector3(0.45f, 0.30f, 0.45f);
                    AttachForensicBaseRing(go, new Vector3(0.40f, 0.005f, 0.40f));
                    AttachPrimitiveVisual(go, PrimitiveType.Cylinder, new Vector3(0.30f, 0.08f, 0.30f), new Color(0.65f, 0.90f, 1.0f, 0.85f)); // Fractured crystal glass
                    break;
                }
                case "EV-003": // Bloodied Cloth: (1.45, 0.05, 3.20)
                {
                    var col = go.GetComponent<BoxCollider>();
                    if (col == null) col = go.AddComponent<BoxCollider>();
                    col.center = Vector3.zero;
                    col.size = new Vector3(0.50f, 0.25f, 0.45f);
                    AttachForensicBaseRing(go, new Vector3(0.45f, 0.005f, 0.45f));
                    AttachPrimitiveVisual(go, PrimitiveType.Cube, new Vector3(0.36f, 0.06f, 0.28f), new Color(0.68f, 0.06f, 0.06f)); // Bloodstained textile fragment
                    break;
                }
                case "EV-004": // Weapon / Firearm: (3.96, 0.15, 8.89)
                {
                    var col = go.GetComponent<BoxCollider>();
                    if (col == null) col = go.AddComponent<BoxCollider>();
                    col.center = Vector3.zero;
                    col.size = new Vector3(0.45f, 0.30f, 0.35f);
                    AttachForensicBaseRing(go, new Vector3(0.42f, 0.005f, 0.42f));
                    AttachPrimitiveVisual(go, PrimitiveType.Cube, new Vector3(0.28f, 0.14f, 0.08f), new Color(0.12f, 0.12f, 0.15f)); // Gunmetal firearm
                    break;
                }
                case "EV-005": // Ballistic Casing: (0.80, 0.02, 7.20)
                {
                    var col = go.GetComponent<CapsuleCollider>();
                    if (col == null) col = go.AddComponent<CapsuleCollider>();
                    col.center = Vector3.zero;
                    col.radius = 0.18f;
                    col.height = 0.30f;
                    col.direction = 1; // Y-axis
                    AttachForensicBaseRing(go, new Vector3(0.38f, 0.005f, 0.38f));
                    AttachPrimitiveVisual(go, PrimitiveType.Cylinder, new Vector3(0.08f, 0.14f, 0.08f), new Color(0.95f, 0.78f, 0.18f)); // Polished brass cartridge
                    break;
                }
                case "EV-006": // Blood-Spatter Pattern: (4.10, 1.50, 5.19)
                {
                    var col = go.GetComponent<BoxCollider>();
                    if (col == null) col = go.AddComponent<BoxCollider>();
                    col.center = Vector3.zero;
                    col.size = new Vector3(0.65f, 0.65f, 0.15f);
                    AttachPrimitiveVisual(go, PrimitiveType.Quad, new Vector3(0.55f, 0.55f, 1f), new Color(0.65f, 0.03f, 0.03f)); // Wall arterial spatter
                    break;
                }
                case "EV-007": // Mobile Phone: (4.16, 0.92, 3.43)
                {
                    var col = go.GetComponent<BoxCollider>();
                    if (col == null) col = go.AddComponent<BoxCollider>();
                    col.center = Vector3.zero;
                    col.size = new Vector3(0.35f, 0.20f, 0.25f);
                    AttachForensicBaseRing(go, new Vector3(0.35f, 0.005f, 0.35f));
                    AttachPrimitiveVisual(go, PrimitiveType.Cube, new Vector3(0.18f, 0.025f, 0.10f), new Color(0.08f, 0.08f, 0.10f)); // Dropped smartphone
                    break;
                }
                case "EV-008": // Small Trace Evidence: (0.98, 1.05, 12.17)
                {
                    var col = go.GetComponent<SphereCollider>();
                    if (col == null) col = go.AddComponent<SphereCollider>();
                    col.center = Vector3.zero;
                    col.radius = 0.20f;
                    AttachPrimitiveVisual(go, PrimitiveType.Sphere, new Vector3(0.12f, 0.12f, 0.12f), new Color(0.95f, 0.95f, 0.75f)); // Synthetic fiber swab
                    break;
                }
            }
        }

        private static void AttachForensicBaseRing(GameObject parent, Vector3 ringScale)
        {
            Transform existingRing = parent.transform.Find("ForensicRing");
            GameObject ringGo;
            if (existingRing == null)
            {
                ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ringGo.name = "ForensicRing";
                ringGo.transform.SetParent(parent.transform, false);
                var c = ringGo.GetComponent<Collider>();
                if (c != null) UnityEngine.Object.DestroyImmediate(c);
            }
            else
            {
                ringGo = existingRing.gameObject;
            }

            ringGo.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            ringGo.transform.localScale = ringScale;

            var mr = ringGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                string matPath = $"{MAT_DIR}/M_ForensicTargetRing.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
                    mat = new Material(urpShader);
                    Color ringColor = new Color(1.0f, 0.85f, 0.05f, 0.85f);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ringColor);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", ringColor);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mr.sharedMaterial = mat;
            }
        }

        private static void AttachPrimitiveVisual(GameObject parent, PrimitiveType type, Vector3 localScale, Color color)
        {
            Transform existingVisual = parent.transform.Find("Visual");
            GameObject visualGo;
            if (existingVisual == null)
            {
                visualGo = GameObject.CreatePrimitive(type);
                visualGo.name = "Visual";
                visualGo.transform.SetParent(parent.transform, false);

                var defaultCol = visualGo.GetComponent<Collider>();
                if (defaultCol != null) UnityEngine.Object.DestroyImmediate(defaultCol);
            }
            else
            {
                visualGo = existingVisual.gameObject;
            }

            visualGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            visualGo.transform.localScale = localScale;

            var mr = visualGo.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                string matName = $"M_Prop_{parent.name}";
                string matPath = $"{MAT_DIR}/{matName}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") 
                                    ?? Shader.Find("Universal Render Pipeline/Unlit") 
                                    ?? Shader.Find("Standard");
                    mat = new Material(urpShader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
                EditorUtility.SetDirty(mat);
                mr.sharedMaterial = mat;
            }
        }
        #endregion

        #region 3. Phase 3 UI & Exit Trigger Setup
        private static void SetupInvestigationExitTrigger()
        {
            GameObject exitTriggerGo = GameObject.Find("[Person2] Exit Doorway Trigger");
            if (exitTriggerGo == null)
            {
                exitTriggerGo = new GameObject("[Person2] Exit Doorway Trigger");
                Undo.RegisterCreatedObjectUndo(exitTriggerGo, "Create Exit Doorway Trigger");
            }

            exitTriggerGo.transform.position = new Vector3(4.00f, 1.10f, 0.30f);

            var col = exitTriggerGo.GetComponent<BoxCollider>();
            if (col == null) col = exitTriggerGo.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = Vector3.zero;
            col.size = new Vector3(1.2f, 2.2f, 0.6f);

            var trigger = exitTriggerGo.GetComponent<InvestigationExitTrigger>();
            if (trigger == null) trigger = exitTriggerGo.AddComponent<InvestigationExitTrigger>();

            // Visual Sign board above doorway
            Transform existingSign = exitTriggerGo.transform.Find("ExitSign");
            GameObject signGo;
            if (existingSign == null)
            {
                signGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                signGo.name = "ExitSign";
                signGo.transform.SetParent(exitTriggerGo.transform, false);
                signGo.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                signGo.transform.localScale = new Vector3(0.9f, 0.25f, 0.05f);
                var sCol = signGo.GetComponent<Collider>();
                if (sCol != null) UnityEngine.Object.DestroyImmediate(sCol);
            }
            else
            {
                signGo = existingSign.gameObject;
            }

            // Sign Text
            Transform existingText = signGo.transform.Find("SignText");
            GameObject textGo;
            if (existingText == null)
            {
                textGo = new GameObject("SignText");
                textGo.transform.SetParent(signGo.transform, false);
                textGo.transform.localPosition = new Vector3(0f, 0f, 0.55f);
                textGo.transform.localRotation = Quaternion.identity;
            }
            else
            {
                textGo = existingText.gameObject;
            }

            var tmp = textGo.GetComponent<TextMeshPro>();
            if (tmp == null) tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = "EXIT & SUBMIT INVESTIGATION";
            tmp.fontSize = 2.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            SerializedObject so = new SerializedObject(trigger);
            so.FindProperty("_signStatusText").objectReferenceValue = tmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[Person 2] Configured [Person2] Exit Doorway Trigger at (4.00, 1.10, 0.30).");
        }

        private static void SetupInvestigationClipboard(Transform rightHand)
        {
            GameObject clipboardGo = GameObject.Find("[Person2] Investigation Clipboard");
            if (clipboardGo == null)
            {
                clipboardGo = new GameObject("[Person2] Investigation Clipboard");
                Undo.RegisterCreatedObjectUndo(clipboardGo, "Create Investigation Clipboard");
            }

            clipboardGo.transform.position = new Vector3(3.80f, 1.05f, 1.20f);
            clipboardGo.transform.rotation = Quaternion.Euler(20f, -30f, 0f);

            var col = clipboardGo.GetComponent<BoxCollider>();
            if (col == null) col = clipboardGo.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(0.38f, 0.52f, 0.04f);

            // Clipboard backing board
            Transform existingBoard = clipboardGo.transform.Find("BoardVisual");
            GameObject boardGo;
            if (existingBoard == null)
            {
                boardGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boardGo.name = "BoardVisual";
                boardGo.transform.SetParent(clipboardGo.transform, false);
                boardGo.transform.localPosition = Vector3.zero;
                boardGo.transform.localScale = new Vector3(0.36f, 0.50f, 0.02f);
                var bCol = boardGo.GetComponent<Collider>();
                if (bCol != null) UnityEngine.Object.DestroyImmediate(bCol);
            }
            else
            {
                boardGo = existingBoard.gameObject;
            }

            var clipboardComp = clipboardGo.GetComponent<CrimeSceneVR.Person2.UI.ForensicInvestigationClipboard>();
            if (clipboardComp == null) clipboardComp = clipboardGo.AddComponent<CrimeSceneVR.Person2.UI.ForensicInvestigationClipboard>();

            // Canvas / Text elements
            Transform existingCanvas = clipboardGo.transform.Find("ClipboardCanvas");
            GameObject canvasGo = existingCanvas != null ? existingCanvas.gameObject : new GameObject("ClipboardCanvas");
            canvasGo.transform.SetParent(clipboardGo.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 0f, 0.015f);
            canvasGo.transform.localRotation = Quaternion.identity;

            // Header Text
            var headerTmp = EnsureTMPChild(canvasGo.transform, "HeaderText", new Vector3(0f, 0.19f, 0f), 0.35f, "<b>FORENSIC CASE FILE</b>");
            // Summary Text
            var summaryTmp = EnsureTMPChild(canvasGo.transform, "SummaryText", new Vector3(0f, 0.14f, 0f), 0.24f, "Discovered: 0/8 | Tagged: 0/8 | Accurate: 0/8");
            // Evidence List Text
            var listTmp = EnsureTMPChild(canvasGo.transform, "ListText", new Vector3(0f, -0.01f, 0f), 0.18f, "Loading evidence items...");
            // Timeline Result Text
            var timelineTmp = EnsureTMPChild(canvasGo.transform, "TimelineText", new Vector3(0f, -0.16f, 0f), 0.20f, "Timeline: Tag items to build chronological sequence.");
            // Status Banner Text
            var bannerTmp = EnsureTMPChild(canvasGo.transform, "BannerText", new Vector3(0f, -0.21f, 0f), 0.22f, "<color=#FFD700>[CLICK OR PRESS ENTER TO SUBMIT]</color>");

            SerializedObject so = new SerializedObject(clipboardComp);
            so.FindProperty("_headerText").objectReferenceValue = headerTmp;
            so.FindProperty("_metricsSummaryText").objectReferenceValue = summaryTmp;
            so.FindProperty("_evidenceListText").objectReferenceValue = listTmp;
            so.FindProperty("_timelineResultText").objectReferenceValue = timelineTmp;
            so.FindProperty("_statusBannerText").objectReferenceValue = bannerTmp;
            so.FindProperty("_visualRoot").objectReferenceValue = boardGo;
            so.FindProperty("_isVisible").boolValue = false;
            so.FindProperty("_followHand").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Set board and canvas initially inactive so it never obstructs scene view
            boardGo.SetActive(false);
            canvasGo.SetActive(false);

            Debug.Log("[Person 2] Configured [Person2] Investigation Clipboard UI.");
        }

        private static TextMeshPro EnsureTMPChild(Transform parent, string name, Vector3 localPos, float fontSize, string defaultText)
        {
            Transform t = parent.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            var tmp = go.GetComponent<TextMeshPro>();
            if (tmp == null) tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = fontSize;
            tmp.text = defaultText;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(0.34f, 0.10f);
            return tmp;
        }
        #endregion
    }
}
