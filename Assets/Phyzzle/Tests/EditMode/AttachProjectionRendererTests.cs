using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Phyzzle.Abilities.Attach;
using UnityEngine;

namespace Phyzzle.Tests
{
    public sealed class AttachProjectionRendererTests
    {
        [Test]
        public void GetCastOrigin_StartsBeyondBoundsInDirection()
        {
            Bounds bounds = new(Vector3.zero, new Vector3(4f, 2f, 6f));
            Vector3 direction = new Vector3(1f, 0f, 1f).normalized;

            Vector3 origin = AttachProjectionRenderer.GetCastOrigin(bounds, direction, 0.01f);
            float support = Vector3.Dot(bounds.extents, new Vector3(
                Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z)));

            Assert.That(Vector3.Dot(origin - bounds.center, direction), Is.GreaterThan(support));
        }

        [Test]
        public void ResolvePlanarForward_VerticalCameraUsesFallback()
        {
            Vector3 result = AttachProjectionRenderer.ResolvePlanarForward(Vector3.down, Vector3.forward);

            Assert.That(result, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void ProjectPoint_PlacesPointOnReceiverPlane()
        {
            Vector4 plane = new(0f, 1f, 0f, 0f);

            Vector3 projected = AttachProjectionRenderer.ProjectPoint(
                new Vector3(2f, 4f, 3f), Vector3.down, plane);

            Assert.That(projected.y, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void TryGetProjectionPlane_RejectsParallelReceiver()
        {
            RaycastHit hit = new()
            {
                point = Vector3.zero,
                normal = Vector3.right
            };

            bool result = AttachProjectionRenderer.TryGetProjectionPlane(
                hit, Vector3.forward, 0.15f, out _);

            Assert.That(result, Is.False);
        }

        [Test]
        public void SetIsland_RetainsMeshFiltersFromEveryMember()
        {
            GameObject projectionObject = new("Projection");
            GameObject firstObject = new("First");
            GameObject secondObject = new("Second");
            try
            {
                AttachProjectionRenderer projection = projectionObject.AddComponent<AttachProjectionRenderer>();
                MeshFilter firstFilter = firstObject.AddComponent<MeshFilter>();
                MeshFilter secondFilter = secondObject.AddComponent<MeshFilter>();
                AttachableObject first = firstObject.AddComponent<AttachableObject>();
                AttachableObject second = secondObject.AddComponent<AttachableObject>();

                projection.SetIsland(new List<AttachableObject> { first, second });

                FieldInfo field = typeof(AttachProjectionRenderer).GetField(
                    "meshFilters", BindingFlags.Instance | BindingFlags.NonPublic);
                List<MeshFilter> cached = (List<MeshFilter>)field.GetValue(projection);
                Assert.That(cached, Is.EquivalentTo(new[] { firstFilter, secondFilter }));
            }
            finally
            {
                Object.DestroyImmediate(projectionObject);
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void ProjectBounds_ContainsEveryProjectedSourceCorner()
        {
            Bounds source = new(new Vector3(1f, 4f, -2f), new Vector3(2f, 4f, 6f));
            Vector3 direction = Vector3.down;
            Vector4 plane = new(0f, 1f, 0f, 0f);

            Bounds projectedBounds = AttachProjectionRenderer.ProjectBounds(source, direction, plane);
            Vector3 min = source.min;
            Vector3 max = source.max;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 corner = new(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 point = AttachProjectionRenderer.ProjectPoint(corner, direction, plane);

                        Assert.That(projectedBounds.Contains(point), Is.True,
                            $"Projected corner {corner} must lie inside the projected bounds.");
                    }
                }
            }
        }

        [Test]
        public void ReceiverSurface_FadesInAndOutWhenTheRayLosesItsReceiver()
        {
            GameObject receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Physics.SyncTransforms();
                AttachProjectionRenderer.ReceiverSurface surface = new();
                surface.Capture(HitTop(receiver.GetComponent<Collider>()), Vector3.down);

                surface.Fade(true, 0.5f);
                Assert.That(surface.Opacity, Is.EqualTo(0.5f));
                surface.Fade(false, 0.25f);
                Assert.That(surface.Opacity, Is.EqualTo(0.25f),
                    "One missed ray must fade the retained receiver instead of removing the projection.");
                Assert.That(surface.TryGetPlane(out _), Is.True);
                surface.Fade(false, 0.25f);
                Assert.That(surface.Opacity, Is.Zero);
                Assert.That(surface.TryGetPlane(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(receiver);
            }
        }

        [Test]
        public void ReceiverSurface_FollowsMovingReceiverAndImmediatelyClearsDestroyedReceiver()
        {
            GameObject receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                receiver.transform.localScale = new Vector3(2f, 1f, 3f);
                Physics.SyncTransforms();
                AttachProjectionRenderer.ReceiverSurface surface = new();
                surface.Capture(HitTop(receiver.GetComponent<Collider>()), Vector3.down);
                surface.Fade(true, 1f);
                receiver.transform.SetPositionAndRotation(new Vector3(3f, 2f, 1f), Quaternion.Euler(20f, 0f, 30f));

                Assert.That(surface.TryGetPlane(out Vector4 plane), Is.True);
                Vector3 normal = new(plane.x, plane.y, plane.z);
                Vector3 expectedPoint = receiver.transform.TransformPoint(new Vector3(0f, 0.5f, 0f));
                Assert.That(Vector3.Dot(normal, expectedPoint) + plane.w, Is.Zero.Within(0.0001f));
                Assert.That(Vector3.Dot(normal, receiver.transform.up), Is.GreaterThan(0.999f));

                Object.DestroyImmediate(receiver);
                surface.Fade(false, 0.01f);
                Assert.That(surface.Opacity, Is.Zero, "Destroyed receivers must not leave a fading ghost plane.");
                Assert.That(surface.TryGetPlane(out _), Is.False);
            }
            finally
            {
                if (receiver != null)
                {
                    Object.DestroyImmediate(receiver);
                }
            }
        }

        [Test]
        public void Submit_CrossfadesDifferentReceiversAndProjectsOntoTheCeiling()
        {
            GameObject owner = new("Projection Test");
            GameObject cameraObject = new("Projection Camera");
            GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject replacement = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            AttachSettings settings = ScriptableObject.CreateInstance<AttachSettings>();
            Material material = new(Shader.Find("Phyzzle/AttachProjection"));
            try
            {
                source.transform.position = new Vector3(0f, 2f, 0f);
                floor.transform.position = Vector3.zero;
                replacement.transform.position = new Vector3(10f, 0.5f, 0f);
                ceiling.transform.position = new Vector3(0f, 4f, 0f);
                floor.transform.localScale = replacement.transform.localScale = ceiling.transform.localScale =
                    new Vector3(4f, 0.2f, 4f);
                settings.projectionMaxDistance = 5f;
                source.layer = floor.layer = replacement.layer = ceiling.layer = 30;
                settings.targetMask = 1 << 30;
                AttachProjectionRenderer projection = owner.AddComponent<AttachProjectionRenderer>();
                projection.Configure(cameraObject.AddComponent<Camera>(), settings, material);
                projection.SetIsland(new[] { source.AddComponent<AttachableObject>() });
                Physics.SyncTransforms();

                projection.Submit(0.08f);
                Assert.That(projection.LastSubmittedDrawCount, Is.EqualTo(2),
                    "Both the floor and the previously missing ceiling must receive a projection.");
                AttachProjectionRenderer.ReceiverSurface[] current = GetSurfaces(projection, "currentSurfaces");
                AttachProjectionRenderer.ReceiverSurface[] retiring = GetSurfaces(projection, "retiringSurfaces");
                Assert.That(current[0].Opacity, Is.EqualTo(0.5f));
                Assert.That(current[5].Opacity, Is.EqualTo(0.5f));

                floor.transform.position += Vector3.right * 10f;
                replacement.transform.position = new Vector3(0f, 0.5f, 0f);
                Physics.SyncTransforms();
                projection.Submit(0.04f);
                Assert.That(projection.LastSubmittedDrawCount, Is.EqualTo(3),
                    "Old and new receiver planes must coexist during the transition, without interpolating them.");
                Assert.That(current[0].Opacity, Is.EqualTo(0.25f));
                Assert.That(retiring[0].Opacity, Is.EqualTo(0.25f));
                Assert.That(current[0].TryGetPlane(out Vector4 newPlane), Is.True);
                Assert.That(retiring[0].TryGetPlane(out Vector4 oldPlane), Is.True);
                Assert.That(newPlane.w, Is.EqualTo(-0.6f).Within(0.001f));
                Assert.That(oldPlane.w, Is.EqualTo(-0.1f).Within(0.001f));

                projection.Submit(0.16f);
                Assert.That(projection.LastSubmittedDrawCount, Is.EqualTo(2));
                projection.Clear();
                Assert.That(current[0].Opacity, Is.Zero);
                Assert.That(current[5].Opacity, Is.Zero);
                Assert.That(retiring[0].Opacity, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(replacement);
                Object.DestroyImmediate(ceiling);
                Object.DestroyImmediate(settings);
                Object.DestroyImmediate(material);
            }
        }

        private static RaycastHit HitTop(Collider receiver)
        {
            Ray ray = new(receiver.bounds.center + Vector3.up * 4f, Vector3.down);
            Assert.That(receiver.Raycast(ray, out RaycastHit hit, 8f), Is.True);
            return hit;
        }

        private static AttachProjectionRenderer.ReceiverSurface[] GetSurfaces(
            AttachProjectionRenderer projection, string fieldName)
        {
            return (AttachProjectionRenderer.ReceiverSurface[])typeof(AttachProjectionRenderer)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(projection);
        }
    }
}
