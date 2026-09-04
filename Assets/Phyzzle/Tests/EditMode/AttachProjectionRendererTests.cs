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
    }
}
