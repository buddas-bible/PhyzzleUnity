using System.Reflection;
using NUnit.Framework;
using Phyzzle.Abilities;
using Phyzzle.Abilities.Attach;
using Phyzzle.Abilities.Rewind;
using Phyzzle.Editor;
using Phyzzle.Player;
using Phyzzle.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Phyzzle.Tests
{
    /// <summary>
    /// <c>PlayerHudBuilderTests</c> 대상 동작을 검증하는 테스트 모음이다.
    /// </summary>
    public sealed class PlayerHudBuilderTests
    {
        /// <summary>
        /// <c>SetUp</c> 테스트 지원 동작을 수행한다.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>
        /// <c>CreateHud_BuildsResponsiveCanvasAndPresenter</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void CreateHud_BuildsResponsiveCanvasAndPresenter()
        {
            GameObject player = new("Player");
            PlayerInputReader input = player.AddComponent<PlayerInputReader>();
            AttachHoldController hold = player.AddComponent<AttachHoldController>();
            AttachAbilityController attach = player.AddComponent<AttachAbilityController>();
            RewindAbilityController rewind = player.AddComponent<RewindAbilityController>();
            PlayerAbilityController abilities = player.AddComponent<PlayerAbilityController>();
            AttachmentService service = new GameObject("AttachmentService")
                .AddComponent<AttachmentService>();

            PlayerHudView view = PlayerHudBuilder.CreateHud(
                input,
                attach,
                hold,
                service,
                rewind);

            Canvas canvas = view.GetComponent<Canvas>();
            CanvasScaler scaler = view.GetComponent<CanvasScaler>();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f));
            Assert.That(view.GetComponent<PlayerHudPresenter>(), Is.Not.Null);
            Assert.That(view.transform.Find("Crosshairs/CrossHead_01"), Is.Not.Null);
            Assert.That(view.transform.Find("Crosshairs/CrossHead_02"), Is.Not.Null);
            Assert.That(view.transform.Find("Prompts/Gamepad"), Is.Not.Null);
            Assert.That(view.transform.Find("Prompts/Keyboard"), Is.Not.Null);
            Assert.That(view.transform.Find("AbilitySelector/Previous/Attach"), Is.Not.Null);
            Assert.That(view.transform.Find("AbilitySelector/Previous/Rewind"), Is.Not.Null);
            Assert.That(view.transform.Find("AbilitySelector/Current/Attach"), Is.Not.Null);
            Assert.That(view.transform.Find("AbilitySelector/Current/Rewind"), Is.Not.Null);
            Assert.That(view.transform.Find("AbilitySelector/Next/Attach"), Is.Not.Null);
            Assert.That(view.transform.Find("AbilitySelector/Next/Rewind"), Is.Not.Null);
            Assert.That(
                view.transform.Find("AbilitySelector/Current/Attach").gameObject.activeSelf,
                Is.True);
            Assert.That(
                view.transform.Find("AbilitySelector/Current/Rewind").gameObject.activeSelf,
                Is.False);
            Assert.That(view.transform.Find("AbilitySelector/Previous").gameObject.activeSelf,
                Is.False);
            Assert.That(view.transform.Find("AbilitySelector/Next").gameObject.activeSelf,
                Is.False);
            CanvasGroup previousGroup = view.transform
                .Find("AbilitySelector/Previous")
                .GetComponent<CanvasGroup>();
            CanvasGroup currentGroup = view.transform
                .Find("AbilitySelector/Current")
                .GetComponent<CanvasGroup>();
            CanvasGroup nextGroup = view.transform
                .Find("AbilitySelector/Next")
                .GetComponent<CanvasGroup>();
            Assert.That(previousGroup.alpha, Is.LessThan(currentGroup.alpha));
            Assert.That(nextGroup.alpha, Is.LessThan(currentGroup.alpha));
            Assert.That(previousGroup.transform.localScale.x,
                Is.LessThan(currentGroup.transform.localScale.x));
            Assert.That(nextGroup.transform.localScale.x,
                Is.LessThan(currentGroup.transform.localScale.x));
            Assert.That(view.transform.Find("Prompts").GetComponent<PlayerHudSafeArea>(),
                Is.Not.Null);

            PlayerHudPresenter presenter = view.GetComponent<PlayerHudPresenter>();
            FieldInfo controllerField = typeof(PlayerHudPresenter).GetField(
                "abilityController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(controllerField, Is.Not.Null);
            Assert.That(controllerField.GetValue(presenter), Is.SameAs(abilities));
        }

        /// <summary>
        /// <c>CreateHud_CalledTwice_ReplacesExistingHud</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void CreateHud_CalledTwice_ReplacesExistingHud()
        {
            GameObject player = new("Player");
            PlayerInputReader input = player.AddComponent<PlayerInputReader>();
            AttachHoldController hold = player.AddComponent<AttachHoldController>();
            AttachAbilityController attach = player.AddComponent<AttachAbilityController>();
            RewindAbilityController rewind = player.AddComponent<RewindAbilityController>();
            AttachmentService service = new GameObject("AttachmentService")
                .AddComponent<AttachmentService>();

            PlayerHudBuilder.CreateHud(input, attach, hold, service, rewind);
            PlayerHudBuilder.CreateHud(input, attach, hold, service, rewind);

            Assert.That(Object.FindObjectsByType<PlayerHudView>(FindObjectsSortMode.None).Length,
                Is.EqualTo(1));
        }

        /// <summary>
        /// <c>SetPreview_ShowsRequestedStateOutsidePlayMode</c> 테스트 시나리오를 검증한다.
        /// </summary>
        [Test]
        public void SetPreview_ShowsRequestedStateOutsidePlayMode()
        {
            GameObject player = new("Player");
            PlayerInputReader input = player.AddComponent<PlayerInputReader>();
            AttachHoldController hold = player.AddComponent<AttachHoldController>();
            AttachAbilityController attach = player.AddComponent<AttachAbilityController>();
            RewindAbilityController rewind = player.AddComponent<RewindAbilityController>();
            AttachmentService service = new GameObject("AttachmentService")
                .AddComponent<AttachmentService>();
            PlayerHudView view = PlayerHudBuilder.CreateHud(
                input,
                attach,
                hold,
                service,
                rewind);

            view.SetPreview(
                new PlayerHudState(PlayerHudMode.AttachSelecting, hasTarget: true),
                PlayerInputDeviceKind.Gamepad);

            Assert.That(view.transform.Find("Crosshairs/CrossHead_01").gameObject.activeSelf, Is.True);
            Assert.That(view.transform.Find("Crosshairs/CrossHead_02").gameObject.activeSelf, Is.True);
            Assert.That(view.transform.Find("Prompts/Gamepad/Catch_B").gameObject.activeSelf, Is.True);
        }
    }
}
