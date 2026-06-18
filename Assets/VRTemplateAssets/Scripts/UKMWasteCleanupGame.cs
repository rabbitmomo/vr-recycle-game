using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace Unity.VRTemplate
{
    [DisallowMultipleComponent]
    public class UKMWasteCleanupGame : MonoBehaviour
    {
        [SerializeField]
        int m_TargetTrashCount = 5;

        [SerializeField]
        float m_SpawnRadius = 4.0f;

        [SerializeField]
        Vector3 m_BinPosition = new Vector3(2.5f, 0.5f, 2.5f);

        [SerializeField]
        Color m_BinColor = new Color(0.18f, 0.55f, 0.22f, 1f);

        [SerializeField]
        Color m_HudColor = new Color(0.04f, 0.10f, 0.06f, 0.88f);

        [SerializeField]
        float m_CollectDistance = 1.0f;

        XROrigin m_XROrigin;
        XRInteractionManager m_InteractionManager;
        TeleportationProvider m_TeleportationProvider;
        TeleportationArea m_TeleportationArea;
        Transform m_BinTransform;
        TextMeshProUGUI m_ObjectiveText;
        TextMeshProUGUI m_ProgressText;
        TextMeshProUGUI m_StatusText;
        readonly List<TrashItem> m_TrashItems = new List<TrashItem>();
        int m_CollectedTrashCount;
        bool m_IsComplete;

        void Start()
        {
            m_XROrigin = FindAnyObjectByType<XROrigin>();
            m_InteractionManager = FindAnyObjectByType<XRInteractionManager>();

            if (m_XROrigin == null || m_InteractionManager == null)
            {
                Debug.LogError("UKMWasteCleanupGame needs both an XROrigin and an XRInteractionManager in the scene.", this);
                enabled = false;
                return;
            }

            SetupTeleportation();
            SetupBin();
            SetupHud();
            SpawnTrashItems();
            RefreshHud();
        }

        void SetupTeleportation()
        {
            m_TeleportationProvider = m_XROrigin.GetComponent<TeleportationProvider>();
            if (m_TeleportationProvider == null)
                m_TeleportationProvider = m_XROrigin.gameObject.AddComponent<TeleportationProvider>();

            var plane = GameObject.Find("Plane");
            if (plane == null)
            {
                Debug.LogWarning("Could not find the ground Plane object, so teleportation was not auto-configured.", this);
                return;
            }

            m_TeleportationArea = plane.GetComponent<TeleportationArea>();
            if (m_TeleportationArea == null)
                m_TeleportationArea = plane.AddComponent<TeleportationArea>();

            m_TeleportationArea.teleportationProvider = m_TeleportationProvider;
            m_TeleportationArea.interactionManager = m_InteractionManager;
        }

        void SetupBin()
        {
            var bin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bin.name = "UKM Trash Bin";
            bin.transform.position = m_BinPosition;
            bin.transform.localScale = new Vector3(1.1f, 1.0f, 1.1f);

            var binCollider = bin.GetComponent<Collider>();
            binCollider.isTrigger = true;

            var binRenderer = bin.GetComponent<Renderer>();
            binRenderer.sharedMaterial = CreateMaterial(m_BinColor);

            CreateBillboardText("Trash Bin", bin.transform, new Vector3(0f, 1.25f, 0f), 0.16f, Color.white);
            m_BinTransform = bin.transform;
        }

        void SetupHud()
        {
            var cameraTransform = m_XROrigin.Camera != null ? m_XROrigin.Camera.transform : m_XROrigin.transform;

            var hudRoot = new GameObject("UKM Mission HUD");
            hudRoot.transform.SetParent(cameraTransform, false);
            hudRoot.transform.localPosition = new Vector3(0f, -0.2f, 1.35f);
            hudRoot.transform.localRotation = Quaternion.identity;
            hudRoot.transform.localScale = Vector3.one * 0.0015f;

            var canvas = hudRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = m_XROrigin.Camera;
            hudRoot.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            hudRoot.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(hudRoot.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(900f, 420f);
            panel.AddComponent<Image>().color = m_HudColor;

            m_ObjectiveText = CreateHudText(panel.transform, "UKM Green Mission", new Vector2(0f, 150f), 36f, TextAlignmentOptions.Center);
            m_ProgressText = CreateHudText(panel.transform, "", new Vector2(0f, 70f), 28f, TextAlignmentOptions.Center);
            m_StatusText = CreateHudText(panel.transform, "", new Vector2(0f, -30f), 24f, TextAlignmentOptions.Center);

            CreateHudText(panel.transform, "Teleport to move. Grab rubbish and drop it at the green bin.", new Vector2(0f, -120f), 20f, TextAlignmentOptions.Center);
        }

        void SpawnTrashItems()
        {
            for (var index = 0; index < m_TargetTrashCount; index++)
            {
                var angle = (Mathf.PI * 2f / m_TargetTrashCount) * index;
                var radius = m_SpawnRadius + Random.Range(-0.4f, 0.6f);
                var position = new Vector3(Mathf.Cos(angle) * radius, 0.35f, Mathf.Sin(angle) * radius);
                position += new Vector3(0f, 0f, Random.Range(-0.5f, 0.5f));

                var trashObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trashObject.name = $"Trash Item {index + 1}";
                trashObject.transform.position = position;
                trashObject.transform.rotation = Random.rotation;
                trashObject.transform.localScale = new Vector3(Random.Range(0.18f, 0.28f), Random.Range(0.10f, 0.16f), Random.Range(0.12f, 0.22f));

                var trashRenderer = trashObject.GetComponent<Renderer>();
                trashRenderer.sharedMaterial = CreateMaterial(GetTrashColor(index));

                var rigidBody = trashObject.AddComponent<Rigidbody>();
                rigidBody.mass = 0.2f;
                rigidBody.angularDamping = 0.5f;
                rigidBody.linearDamping = 0.2f;

                var grabInteractable = trashObject.AddComponent<XRGrabInteractable>();
                grabInteractable.throwOnDetach = false;

                var trashItem = trashObject.AddComponent<TrashItem>();
                trashItem.Configure(this, m_BinTransform, m_CollectDistance, grabInteractable);
                m_TrashItems.Add(trashItem);
            }
        }

        public void CollectTrash(TrashItem trashItem)
        {
            if (m_IsComplete || trashItem == null)
                return;

            m_CollectedTrashCount++;
            m_TrashItems.Remove(trashItem);
            RefreshHud();

            if (m_CollectedTrashCount >= m_TargetTrashCount)
                CompleteMission();
        }

        void CompleteMission()
        {
            m_IsComplete = true;
            RefreshHud();

            if (m_StatusText != null)
                m_StatusText.text = "Mission complete! UKM area is clean again.";

            foreach (var trashItem in m_TrashItems)
            {
                if (trashItem != null)
                    trashItem.DisableAfterCompletion();
            }

            m_TrashItems.Clear();
        }

        void RefreshHud()
        {
            if (m_ObjectiveText != null)
                m_ObjectiveText.text = "UKM Green Mission";

            if (m_ProgressText != null)
                m_ProgressText.text = $"Collected: {m_CollectedTrashCount}/{m_TargetTrashCount}";

            if (m_StatusText != null && !m_IsComplete)
                m_StatusText.text = "Collect trash around UKM and return it to the green bin.";
        }

        Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader);
            material.color = color;
            return material;
        }

        TextMeshProUGUI CreateHudText(Transform parent, string text, Vector2 anchoredPosition, float fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(text);
            textObject.transform.SetParent(parent, false);

            var rectTransform = textObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(840f, 72f);
            rectTransform.anchoredPosition = anchoredPosition;

            var tmp = textObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        TextMeshPro CreateBillboardText(string text, Transform parent, Vector3 localPosition, float fontSize, Color color)
        {
            var textObject = new GameObject(text);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.Euler(30f, 180f, 0f);

            var tmp = textObject.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        Color GetTrashColor(int index)
        {
            var pattern = index % 3;
            if (pattern == 0)
                return new Color(0.55f, 0.35f, 0.18f, 1f);

            if (pattern == 1)
                return new Color(0.72f, 0.72f, 0.72f, 1f);

            return new Color(0.15f, 0.55f, 0.70f, 1f);
        }
    }

    [DisallowMultipleComponent]
    public class TrashItem : MonoBehaviour
    {
        UKMWasteCleanupGame m_Game;
        Transform m_BinTransform;
        XRGrabInteractable m_GrabInteractable;
        float m_CollectDistance;
        bool m_IsCollected;

        public void Configure(UKMWasteCleanupGame game, Transform binTransform, float collectDistance, XRGrabInteractable grabInteractable)
        {
            m_Game = game;
            m_BinTransform = binTransform;
            m_CollectDistance = collectDistance;
            m_GrabInteractable = grabInteractable;
            BindGrabInteractable();
        }

        void OnEnable()
        {
            BindGrabInteractable();
        }

        void OnDisable()
        {
            if (m_GrabInteractable != null)
                m_GrabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        void BindGrabInteractable()
        {
            if (m_GrabInteractable == null)
                return;

            m_GrabInteractable.selectExited.RemoveListener(OnSelectExited);
            m_GrabInteractable.selectExited.AddListener(OnSelectExited);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (m_IsCollected || m_Game == null || m_BinTransform == null)
                return;

            if (Vector3.Distance(transform.position, m_BinTransform.position) <= m_CollectDistance)
            {
                m_IsCollected = true;
                m_Game.CollectTrash(this);
                Destroy(gameObject);
            }
        }

        public void DisableAfterCompletion()
        {
            if (m_GrabInteractable != null)
                m_GrabInteractable.enabled = false;

            if (TryGetComponent(out Rigidbody rigidBody))
                rigidBody.isKinematic = true;
        }
    }
}