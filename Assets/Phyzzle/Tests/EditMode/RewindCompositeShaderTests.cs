using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Phyzzle.Tests
{
    public sealed class RewindCompositeShaderTests
    {
        private const int Size = 9;
        private static readonly Color SourceColor = new(0.8f, 0.2f, 0.1f, 0.6f);
        private static readonly Color EligibleColor =
            (Color)new Color32(0xD7, 0xA5, 0x2D, 0xFF);
        private static readonly Color ActiveColor =
            (Color)new Color32(0xFF, 0xD4, 0x5C, 0xFF);

        private Material material;

        [SetUp]
        public void SetUp()
        {
            Shader shader = Shader.Find("Hidden/Phyzzle/RewindComposite");
            Assert.That(shader, Is.Not.Null);
            material = new Material(shader);
        }

        [TearDown]
        public void TearDown()
        {
            if (material != null)
            {
                Object.DestroyImmediate(material);
            }

            Shader.SetGlobalFloat(Shader.PropertyToID("_RewindSelectionBlend"), 0f);
            Shader.SetGlobalTexture(
                Shader.PropertyToID("_RewindMaskTexture"),
                Texture2D.blackTexture);
        }

        [Test]
        public void Composite_ZeroBlendPreservesSourceRgba()
        {
            Texture2D mask = SolidTexture(Color.clear);
            Texture2D output = Render(mask, 0f);

            AssertColor(output.GetPixel(Size / 2, Size / 2), SourceColor, 0.015f);

            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(output);
        }

        [Test]
        public void Composite_UnmaskedWorldUsesApprovedRec709SaturationAndPreservesAlpha()
        {
            Texture2D mask = SolidTexture(Color.clear);
            Texture2D output = Render(mask, 1f);

            Color pixel = output.GetPixel(Size / 2, Size / 2);
            Assert.That(pixel.r, Is.EqualTo(0.3778992f).Within(0.02f));
            Assert.That(pixel.g, Is.EqualTo(0.3058992f).Within(0.02f));
            Assert.That(pixel.b, Is.EqualTo(0.2938992f).Within(0.02f));
            Assert.That(pixel.a, Is.EqualTo(0.6f).Within(0.015f));

            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(output);
        }

        [Test]
        public void Composite_PlayerPreserveChannelWinsOverEligibleAndActiveChannels()
        {
            Texture2D mask = SolidTexture(Color.white);
            Texture2D output = Render(mask, 1f);

            AssertColor(output.GetPixel(Size / 2, Size / 2), SourceColor, 0.015f);

            Object.DestroyImmediate(mask);
            Object.DestroyImmediate(output);
        }

        [Test]
        public void Composite_ActiveTargetIsStrongerAndBrighterThanEligibleTarget()
        {
            Texture2D eligibleMask = SolidTexture(new Color(0f, 1f, 0f, 1f));
            Texture2D activeMask = SolidTexture(new Color(0f, 0f, 1f, 1f));
            Texture2D eligibleOutput = Render(eligibleMask, 1f);
            Texture2D activeOutput = Render(activeMask, 1f);

            Color eligible = eligibleOutput.GetPixel(Size / 2, Size / 2);
            Color active = activeOutput.GetPixel(Size / 2, Size / 2);
            Assert.That(ColorDistance(eligible, EligibleColor), Is.LessThan(0.45f));
            Assert.That(ColorDistance(active, ActiveColor), Is.LessThan(0.32f));
            Assert.That(active.r, Is.GreaterThan(eligible.r + 0.05f));
            Assert.That(active.g, Is.GreaterThan(eligible.g + 0.05f));
            Assert.That(active.a, Is.EqualTo(SourceColor.a).Within(0.015f));

            Object.DestroyImmediate(eligibleMask);
            Object.DestroyImmediate(activeMask);
            Object.DestroyImmediate(eligibleOutput);
            Object.DestroyImmediate(activeOutput);
        }

        [Test]
        public void Composite_ActiveOutlineReachesFartherThanEligibleOutline()
        {
            int center = Size / 2;
            Texture2D emptyMask = SolidTexture(Color.clear);
            Texture2D eligibleMask = SolidTexture(Color.clear);
            Texture2D activeMask = SolidTexture(Color.clear);
            eligibleMask.SetPixel(center, center, new Color(0f, 1f, 0f, 1f));
            activeMask.SetPixel(center, center, new Color(0f, 0f, 1f, 1f));
            eligibleMask.Apply(false, false);
            activeMask.Apply(false, false);
            Texture2D worldOutput = Render(emptyMask, 1f);
            Texture2D eligibleOutput = Render(eligibleMask, 1f);
            Texture2D activeOutput = Render(activeMask, 1f);

            Color worldAtFarPixel = worldOutput.GetPixel(center + 3, center);
            Color eligibleAtFarPixel = eligibleOutput.GetPixel(center + 3, center);
            Color activeAtFarPixel = activeOutput.GetPixel(center + 3, center);
            Assert.That(ColorDistance(eligibleAtFarPixel, worldAtFarPixel), Is.LessThan(0.03f));
            Assert.That(ColorDistance(activeAtFarPixel, worldAtFarPixel), Is.GreaterThan(0.08f));

            Object.DestroyImmediate(emptyMask);
            Object.DestroyImmediate(eligibleMask);
            Object.DestroyImmediate(activeMask);
            Object.DestroyImmediate(worldOutput);
            Object.DestroyImmediate(eligibleOutput);
            Object.DestroyImmediate(activeOutput);
        }

        private Texture2D Render(Texture2D mask, float blend)
        {
            Texture2D source = SolidTexture(SourceColor);
            RenderTexture destination = new(
                Size,
                Size,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            destination.Create();

            Shader.SetGlobalVector(
                Shader.PropertyToID("_BlitTexture_TexelSize"),
                new Vector4(1f / Size, 1f / Size, Size, Size));
            Shader.SetGlobalTexture(Shader.PropertyToID("_RewindMaskTexture"), mask);
            Shader.SetGlobalFloat(Shader.PropertyToID("_RewindSelectionBlend"), blend);
            Shader.SetGlobalFloat(Shader.PropertyToID("_RewindWorldSaturation"), 0.12f);
            Shader.SetGlobalColor(Shader.PropertyToID("_RewindEligibleColor"), EligibleColor);
            Shader.SetGlobalColor(Shader.PropertyToID("_RewindActiveColor"), ActiveColor);
            Shader.SetGlobalFloat(Shader.PropertyToID("_RewindEligibleOutlinePixels"), 1f);
            Shader.SetGlobalFloat(Shader.PropertyToID("_RewindActiveOutlinePixels"), 2.5f);

            RTHandle sourceHandle = RTHandles.Alloc(source);
            CommandBuffer command = CommandBufferPool.Get("Rewind Composite Pixel Test");
            command.SetRenderTarget(destination);
            command.ClearRenderTarget(false, true, Color.clear);
            Blitter.BlitTexture(
                command,
                sourceHandle,
                new Vector4(1f, 1f, 0f, 0f),
                material,
                0);
            Graphics.ExecuteCommandBuffer(command);
            CommandBufferPool.Release(command);
            sourceHandle.Release();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = destination;
            Texture2D output = new(Size, Size, TextureFormat.RGBA32, false, true);
            output.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
            output.Apply(false, false);
            RenderTexture.active = previous;

            Object.DestroyImmediate(source);
            destination.Release();
            Object.DestroyImmediate(destination);
            return output;
        }

        private static Texture2D SolidTexture(Color color)
        {
            Texture2D texture = new(Size, Size, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static float ColorDistance(Color first, Color second) =>
            Vector3.Distance(
                new Vector3(first.r, first.g, first.b),
                new Vector3(second.r, second.g, second.b));

        private static void AssertColor(Color actual, Color expected, float tolerance)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
        }
    }
}
