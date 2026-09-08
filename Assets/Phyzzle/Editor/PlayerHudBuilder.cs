using Phyzzle.Abilities;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Player;
using Phyzzle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Phyzzle.Editor
{
    /// <summary>
    /// 플레이어 HUD 계층을 에디터에서 생성하고 각 상태를 즉시 미리보기할 수 있게 구성한다.
    /// </summary>
    public static class PlayerHudBuilder
    {
        private const string HudName = "PhyzzleHUD";
        private const string AssetRoot = "Assets/Phyzzle/UI/Legacy/";

        /// <summary>
        /// HUD를 기본 비표시 상태로 미리본다.
        /// </summary>
        [MenuItem("Phyzzle/Migration/HUD Preview/Hidden")]
        private static void PreviewHidden() => Preview(new PlayerHudState(PlayerHudMode.Default));

        /// <summary>
        /// 부착 대상 선택 상태 HUD를 미리본다.
        /// </summary>
        [MenuItem("Phyzzle/Migration/HUD Preview/Attach Select")]
        private static void PreviewAttachSelect() =>
            Preview(new PlayerHudState(PlayerHudMode.AttachSelecting));

        /// <summary>
        /// 부착 대상을 조준한 선택 상태 HUD를 미리본다.
        /// </summary>
        [MenuItem("Phyzzle/Migration/HUD Preview/Attach Target")]
        private static void PreviewAttachTarget() =>
            Preview(new PlayerHudState(PlayerHudMode.AttachSelecting, hasTarget: true));

        /// <summary>
        /// 단일 오브젝트를 들고 있는 HUD 상태를 미리본다.
        /// </summary>
        [MenuItem("Phyzzle/Migration/HUD Preview/Hold Single")]
        private static void PreviewHoldSingle() =>
            Preview(new PlayerHudState(PlayerHudMode.AttachHolding));

        /// <summary>
        /// 여러 오브젝트로 이루어진 부착 섬을 들고 있는 HUD 상태를 미리본다.
        /// </summary>
        [MenuItem("Phyzzle/Migration/HUD Preview/Hold Island")]
        private static void PreviewHoldIsland() =>
            Preview(new PlayerHudState(PlayerHudMode.AttachHolding, islandSize: 2));

        /// <summary>
        /// 부착 섬을 회전 조작 중인 HUD 상태를 미리본다.
        /// </summary>
        [MenuItem("Phyzzle/Migration/HUD Preview/Rotate Island")]
        private static void PreviewRotateIsland() =>
            Preview(new PlayerHudState(
                PlayerHudMode.AttachHolding,
                rotateMode: true,
                islandSize: 2));

        /// <summary>
        /// 현재 들고 있는 대상이 다른 부착 대상과 접촉한 HUD 상태를 미리본다.
        /// </summary>
        [MenuItem("Phyzzle/Migration/HUD Preview/Can Attach")]
        private static void PreviewCanAttach() =>
            Preview(new PlayerHudState(
                PlayerHudMode.AttachHolding,
                touchingAttachable: true));

        /// <summary>
        /// 되감기 대상을 조준한 선택 상태 HUD를 미리본다.
        /// </summary>
        [MenuItem("Phyzzle/Migration/HUD Preview/Rewind Target")]
        private static void PreviewRewindTarget() =>
            Preview(new PlayerHudState(PlayerHudMode.RewindSelecting, hasTarget: true));

        /// <summary>
        /// 키보드 입력 기준의 부착 대상 선택 HUD를 미리본다.
        /// </summary>
        [MenuItem("Phyzzle/Migration/HUD Preview/Keyboard Attach Target")]
        private static void PreviewKeyboardTarget() =>
            Preview(
                new PlayerHudState(PlayerHudMode.AttachSelecting, hasTarget: true),
                PlayerInputDeviceKind.Keyboard);

        /// <summary>
        /// 플레이어 입력과 능력 컨트롤러에 연결된 전체 HUD Canvas, 프롬프트와 Presenter를 생성한다.
        /// </summary>
        public static PlayerHudView CreateHud(
            PlayerInputReader input,
            AttachAbilityController attach,
            AttachHoldController hold,
            AttachmentService attachmentService,
            RewindAbilityController rewind)
        {
            GameObject existing = GameObject.Find(HudName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject root = new(
                HudName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Transform crosshairs = CreateContainer("Crosshairs", root.transform);
            GameObject defaultCrosshair = CreateImage(
                "CrossHead_01",
                crosshairs,
                AssetRoot + "CrossHead_01.png",
                Vector2.zero,
                new Vector2(1280f, 720f),
                new Vector2(0.5f, 0.5f));
            GameObject targetCrosshair = CreateImage(
                "CrossHead_02",
                crosshairs,
                AssetRoot + "CrossHead_02.png",
                Vector2.zero,
                new Vector2(1280f, 720f),
                new Vector2(0.5f, 0.5f));
            GameObject rotationArrow = CreateContainer("RotationArrow", crosshairs).gameObject;

            Transform prompts = CreateContainer("Prompts", root.transform);
            prompts.gameObject.AddComponent<PlayerHudSafeArea>();
            PlayerHudPromptSet gamepad = CreateGamepadPrompts(prompts);
            PlayerHudPromptSet keyboard = CreateKeyboardPrompts(prompts);
            PlayerHudAbilitySelector abilitySelector = CreateAbilitySelector(root.transform);

            PlayerHudView view = root.AddComponent<PlayerHudView>();
            view.Configure(
                defaultCrosshair,
                targetCrosshair,
                rotationArrow,
                gamepad,
                keyboard,
                abilitySelector);
            view.SetPreview(
                new PlayerHudState(PlayerHudMode.Default),
                PlayerInputDeviceKind.Gamepad);

            PlayerHudPresenter presenter = root.AddComponent<PlayerHudPresenter>();
            PlayerAbilityController abilities = input != null
                ? input.GetComponent<PlayerAbilityController>()
                : null;
            presenter.Configure(
                input,
                abilities,
                attach,
                hold,
                attachmentService,
                rewind,
                view);
            return view;
        }

        /// <summary>
        /// 이전·현재·다음 능력을 표시하는 HUD 능력 선택기를 생성한다.
        /// </summary>
        private static PlayerHudAbilitySelector CreateAbilitySelector(Transform parent)
        {
            RectTransform root = CreateContainer("AbilitySelector", parent);
            root.gameObject.AddComponent<PlayerHudSafeArea>();
            PlayerHudAbilitySlot previous = CreateAbilitySlot(
                root,
                "Previous",
                58f,
                0.75f,
                0.28f);
            PlayerHudAbilitySlot current = CreateAbilitySlot(
                root,
                "Current",
                160f,
                1f,
                1f);
            PlayerHudAbilitySlot next = CreateAbilitySlot(
                root,
                "Next",
                262f,
                0.75f,
                0.28f);
            return new PlayerHudAbilitySelector(
                root.gameObject,
                previous,
                current,
                next);
        }

        /// <summary>
        /// 지정 위치·크기·투명도로 Attach/Rewind 배지를 담는 하나의 능력 슬롯을 생성한다.
        /// </summary>
        private static PlayerHudAbilitySlot CreateAbilitySlot(
            Transform parent,
            string name,
            float x,
            float scale,
            float alpha)
        {
            RectTransform root = CreateContainer(name, parent);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(x, -88f);
            root.sizeDelta = new Vector2(104f, 118f);
            root.localScale = Vector3.one * scale;
            root.gameObject.AddComponent<CanvasGroup>().alpha = alpha;

            GameObject attach = CreateAbilityBadge(
                root,
                "Attach",
                "A",
                "ATTACH",
                new Color(0.08f, 0.82f, 0.72f, 0.9f));
            GameObject rewind = CreateAbilityBadge(
                root,
                "Rewind",
                "R",
                "REWIND",
                new Color(0.95f, 0.72f, 0.18f, 0.9f));
            return new PlayerHudAbilitySlot(root.gameObject, attach, rewind);
        }

        /// <summary>
        /// 능력 글리프와 이름 텍스트를 포함한 하나의 능력 배지를 생성한다.
        /// </summary>
        private static GameObject CreateAbilityBadge(
            Transform parent,
            string name,
            string glyph,
            string label,
            Color color)
        {
            GameObject badge = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = badge.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image background = badge.GetComponent<Image>();
            background.color = new Color(0.02f, 0.04f, 0.05f, 0.82f);
            background.raycastTarget = false;
            CreateAbilityText(badge.transform, "Glyph", glyph, 48, 18f, color);
            CreateAbilityText(badge.transform, "Label", label, 15, -40f, Color.white);
            return badge;
        }

        /// <summary>
        /// 능력 배지 내부에 지정한 스타일의 가운데 정렬 텍스트를 생성한다.
        /// </summary>
        private static void CreateAbilityText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            float y,
            Color color)
        {
            GameObject label = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(100f, 64f);

            Text text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.text = value;

            Outline outline = label.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>
        /// 레거시 이미지 자산을 이용해 게임패드용 능력 조작 프롬프트 세트를 생성한다.
        /// </summary>
        private static PlayerHudPromptSet CreateGamepadPrompts(Transform parent)
        {
            Transform root = CreateContainer("Gamepad", parent);

            GameObject attachDefault = CreateContainer("Attach_Default", root).gameObject;
            CreateBottomImage(attachDefault.transform, "Stop_A", "Stop_A.png", 0f, 48f, 156f, 64f);

            GameObject catchPrompt = CreateContainer("Catch_B", root).gameObject;
            CreateBottomImage(catchPrompt.transform, "Catch_B_Image", "Catch_B.png", 256f, 266f, 233f, 127f);

            GameObject holdSingle = CreateContainer("Attach_Hold_NoneStick", root).gameObject;
            CreateBottomImage(holdSingle.transform, "Obj_Move", "Obj_Move.png", -246f, 48f, 184f, 52f);
            CreateBottomImage(holdSingle.transform, "Rota_Mode", "Rota_Mode.png", 0f, 48f, 248f, 60f);
            CreateBottomImage(holdSingle.transform, "Stop_A", "Stop_A.png", 232f, 48f, 156f, 64f);

            GameObject holdIsland = CreateContainer("Attach_Hold_Stick", root).gameObject;
            CreateBottomImage(holdIsland.transform, "Obj_Move", "Obj_Move.png", -385f, 48f, 184f, 52f);
            CreateBottomImage(holdIsland.transform, "Rota_Mode", "Rota_Mode.png", -139f, 48f, 248f, 60f);
            CreateBottomImage(holdIsland.transform, "Stick_Off_X", "Stick_Off_X.png", 96f, 48f, 162f, 64f);
            CreateBottomImage(holdIsland.transform, "Stop_A", "Stop_A.png", 285f, 48f, 156f, 64f);

            GameObject rotationSingle = CreateContainer("Rotation_NoneStick", root).gameObject;
            CreateBottomImage(rotationSingle.transform, "Rota_Vertical", "Rota_Vertical.png", -107f, 48f, 184f, 52f);
            CreateBottomImage(rotationSingle.transform, "Rota_Horizental", "Rota_Horizental.png", 107f, 48f, 184f, 52f);

            GameObject rotationIsland = CreateContainer("Rotation_Stick", root).gameObject;
            CreateBottomImage(rotationIsland.transform, "Rota_Vertical", "Rota_Vertical.png", -214f, 48f, 184f, 52f);
            CreateBottomImage(rotationIsland.transform, "Rota_Horizental", "Rota_Horizental.png", 0f, 48f, 184f, 52f);
            CreateBottomImage(rotationIsland.transform, "Stick_Off_X", "Stick_Off_X.png", 203f, 48f, 162f, 64f);

            GameObject stick = CreateContainer("Stick_B", root).gameObject;
            CreateBottomImage(stick.transform, "Stick_B_Image", "Stick_B.png", 256f, 266f, 233f, 127f);

            return new PlayerHudPromptSet(
                root.gameObject,
                attachDefault,
                catchPrompt,
                holdSingle,
                holdIsland,
                rotationSingle,
                rotationIsland,
                stick);
        }

        /// <summary>
        /// 텍스트 라벨을 이용해 키보드용 능력 조작 프롬프트 세트를 생성한다.
        /// </summary>
        private static PlayerHudPromptSet CreateKeyboardPrompts(Transform parent)
        {
            Transform root = CreateContainer("Keyboard", parent);
            GameObject attachDefault = KeyboardGroup(root, "Attach_Default", "[SPACE] 종료", 0f, 48f, 420f);
            GameObject catchPrompt = KeyboardGroup(root, "Catch_B", "[F] 잡기", 256f, 266f, 320f);
            GameObject holdSingle = KeyboardGroup(
                root,
                "Attach_Hold_NoneStick",
                "[ARROWS] 이동    [E] 회전    [SPACE] 종료",
                0f,
                48f,
                900f);
            GameObject holdIsland = KeyboardGroup(
                root,
                "Attach_Hold_Stick",
                "[ARROWS] 이동    [E] 회전    [Z] 연결 해제    [SPACE] 종료",
                0f,
                48f,
                1100f);
            GameObject rotationSingle = KeyboardGroup(
                root,
                "Rotation_NoneStick",
                "[ARROWS] 수직/수평 회전",
                0f,
                48f,
                700f);
            GameObject rotationIsland = KeyboardGroup(
                root,
                "Rotation_Stick",
                "[ARROWS] 수직/수평 회전    [Z] 연결 해제",
                0f,
                48f,
                900f);
            GameObject stick = KeyboardGroup(root, "Stick_B", "[F] 붙이기", 256f, 266f, 320f);

            return new PlayerHudPromptSet(
                root.gameObject,
                attachDefault,
                catchPrompt,
                holdSingle,
                holdIsland,
                rotationSingle,
                rotationIsland,
                stick);
        }

        /// <summary>
        /// 키보드 프롬프트 한 그룹과 외곽선 텍스트 라벨을 생성한다.
        /// </summary>
        private static GameObject KeyboardGroup(
            Transform parent,
            string name,
            string label,
            float x,
            float y,
            float width)
        {
            GameObject group = CreateContainer(name, parent).gameObject;
            GameObject labelObject = new(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Outline));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(group.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 64f);

            Text text = labelObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = label;

            Outline outline = labelObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return group;
        }

        /// <summary>
        /// 화면 하단 기준으로 레거시 프롬프트 이미지를 생성한다.
        /// </summary>
        private static void CreateBottomImage(
            Transform parent,
            string name,
            string file,
            float x,
            float y,
            float width,
            float height)
        {
            CreateImage(
                name,
                parent,
                AssetRoot + file,
                new Vector2(x, y),
                new Vector2(width, height),
                new Vector2(0.5f, 0f));
        }

        /// <summary>
        /// 지정 Sprite 자산과 RectTransform 배치값을 사용하는 HUD Image 오브젝트를 생성한다.
        /// </summary>
        private static GameObject CreateImage(
            string name,
            Transform parent,
            string assetPath,
            Vector2 position,
            Vector2 size,
            Vector2 anchor)
        {
            GameObject imageObject = new(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = imageObject.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            image.preserveAspect = true;
            image.raycastTarget = false;
            return imageObject;
        }

        /// <summary>
        /// 부모 전체 영역을 채우는 RectTransform 컨테이너를 생성한다.
        /// </summary>
        private static RectTransform CreateContainer(string name, Transform parent)
        {
            GameObject container = new(name, typeof(RectTransform));
            RectTransform rect = container.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>
        /// 씬의 HUD View에 지정 상태와 입력 장치 미리보기를 적용하고 씬을 수정 상태로 표시한다.
        /// </summary>
        private static void Preview(
            PlayerHudState state,
            PlayerInputDeviceKind device = PlayerInputDeviceKind.Gamepad)
        {
            PlayerHudView view = Object.FindFirstObjectByType<PlayerHudView>(
                FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogWarning("No PhyzzleHUD found. Create the player sandbox first.");
                return;
            }

            Undo.RecordObject(view, "Preview Phyzzle HUD");
            view.SetPreview(state, device);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            Selection.activeGameObject = view.gameObject;
        }
    }
}
