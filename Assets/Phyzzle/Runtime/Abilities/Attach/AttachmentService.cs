using System;
using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    [DisallowMultipleComponent]
    public sealed class AttachmentService : MonoBehaviour
    {
        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly EntityId first;
            private readonly EntityId second;

            public EdgeKey(AttachableObject a, AttachableObject b)
            {
                EntityId aId = a.GetEntityId();
                EntityId bId = b.GetEntityId();
                first = aId < bId ? aId : bId;
                second = aId < bId ? bId : aId;
            }

            public bool Equals(EdgeKey other) => first == other.first && second == other.second;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(first, second);
        }

        [SerializeField] private AttachSettings settings;

        private readonly AttachmentGraph<AttachableObject> graph = new();
        private readonly Dictionary<EdgeKey, FixedJoint> joints = new();

        public AttachSettings Settings => settings;

        public void Configure(AttachSettings attachSettings)
        {
            settings = attachSettings;
        }

        public void Register(AttachableObject attachable)
        {
            if (attachable != null)
            {
                graph.Add(attachable);
            }
        }

        public void Unregister(AttachableObject attachable)
        {
            if (attachable == null)
            {
                return;
            }

            Detach(attachable);
            graph.Remove(attachable);
        }

        public bool AreInSameIsland(AttachableObject first, AttachableObject second)
        {
            return first != null && second != null && graph.AreInSameComponent(first, second);
        }

        public IReadOnlyCollection<AttachableObject> GetIsland(AttachableObject attachable)
        {
            return attachable == null
                ? Array.Empty<AttachableObject>()
                : graph.GetComponent(attachable);
        }

        public IReadOnlyCollection<AttachableObject> GetDirectConnections(AttachableObject attachable)
        {
            return attachable == null
                ? Array.Empty<AttachableObject>()
                : graph.GetNeighbors(attachable);
        }

        public void SelectIsland(AttachableObject attachable)
        {
            if (settings == null || attachable == null)
            {
                return;
            }

            foreach (AttachableObject member in graph.GetComponent(attachable))
            {
                member?.ApplySelectionOverride(settings);
            }
        }

        public void DeselectIsland(AttachableObject attachable)
        {
            if (attachable == null)
            {
                return;
            }

            foreach (AttachableObject member in graph.GetComponent(attachable))
            {
                member?.RestoreSelectionOverride();
            }
        }

        public bool IsTouchingAttachable(AttachableObject attachable)
        {
            if (attachable == null)
            {
                return false;
            }

            foreach (AttachableObject member in graph.GetComponent(attachable))
            {
                if (member != null && member.ContactCandidate != null &&
                    !AreInSameIsland(attachable, member.ContactCandidate))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryAttach(AttachableObject attachable)
        {
            if (attachable == null)
            {
                return false;
            }

            foreach (AttachableObject member in graph.GetComponent(attachable))
            {
                if (member == null || member.ContactCandidate == null)
                {
                    continue;
                }

                AttachableObject other = member.ContactCandidate;
                Vector3 anchor = member.ContactAnchor;
                if (Attach(member, other, anchor))
                {
                    member.ClearContact(other);
                    other.ClearContact(member);
                    return true;
                }
            }

            return false;
        }

        public bool Attach(AttachableObject first, AttachableObject second, Vector3 worldAnchor)
        {
            if (first == null || second == null || first == second ||
                first.Body == null || second.Body == null || AreInSameIsland(first, second))
            {
                return false;
            }

            FixedJoint joint = first.gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = second.Body;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = first.transform.InverseTransformPoint(worldAnchor);
            joint.connectedAnchor = second.transform.InverseTransformPoint(worldAnchor);
            joint.breakForce = float.PositiveInfinity;
            joint.breakTorque = float.PositiveInfinity;
            joint.enableCollision = false;
            joint.enablePreprocessing = true;

            graph.Connect(first, second);
            joints[new EdgeKey(first, second)] = joint;
            return true;
        }

        public bool Detach(AttachableObject attachable)
        {
            if (attachable == null)
            {
                return false;
            }

            List<AttachableObject> neighbors = new(graph.GetNeighbors(attachable));
            if (neighbors.Count == 0)
            {
                return false;
            }

            foreach (AttachableObject neighbor in neighbors)
            {
                EdgeKey key = new(attachable, neighbor);
                if (joints.Remove(key, out FixedJoint joint) && joint != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(joint);
                    }
                    else
                    {
                        DestroyImmediate(joint);
                    }
                }

                graph.Disconnect(attachable, neighbor);
            }

            foreach (AttachableObject formerNeighbor in neighbors)
            {
                foreach (AttachableObject member in graph.GetComponent(formerNeighbor))
                {
                    member?.RestoreSelectionOverride();
                }
            }

            return true;
        }
    }
}
