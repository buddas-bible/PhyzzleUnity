using System;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AttachableObject : MonoBehaviour
    {
        [Serializable]
        private sealed class SelectionSnapshot
        {
            public bool valid;
            public bool isKinematic;
            public bool useGravity;
            public float mass;
            public Vector3 inertiaTensor;
            public PhysicsMaterial[] materials = Array.Empty<PhysicsMaterial>();

            public void Clear()
            {
                valid = false;
                materials = Array.Empty<PhysicsMaterial>();
            }
        }

        [SerializeField] private Rigidbody body;
        [SerializeField] private AttachmentService service;

        private readonly SelectionSnapshot selectionSnapshot = new();
        private Collider[] colliders = Array.Empty<Collider>();
        private AttachableObject contactCandidate;
        private Vector3 contactAnchor;

        public Rigidbody Body => body;
        public bool IsSelected => selectionSnapshot.valid;
        public AttachableObject ContactCandidate => contactCandidate;
        public Vector3 ContactAnchor => contactAnchor;

        private void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            service ??= FindAnyObjectByType<AttachmentService>();
        }

        private void OnEnable()
        {
            service ??= FindAnyObjectByType<AttachmentService>();
            service?.Register(this);
        }

        private void OnDisable()
        {
            service?.Unregister(this);
            contactCandidate = null;
            contactAnchor = Vector3.zero;
        }

        public void Configure(AttachmentService attachmentService)
        {
            if (service != null && service != attachmentService)
            {
                service.Unregister(this);
            }

            service = attachmentService;
            body ??= GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            if (isActiveAndEnabled)
            {
                service?.Register(this);
            }
        }

        internal void ApplySelectionOverride(AttachSettings settings)
        {
            if (selectionSnapshot.valid || body == null || settings == null)
            {
                return;
            }

            selectionSnapshot.isKinematic = body.isKinematic;
            selectionSnapshot.useGravity = body.useGravity;
            selectionSnapshot.mass = body.mass;
            selectionSnapshot.inertiaTensor = body.inertiaTensor;
            selectionSnapshot.materials = new PhysicsMaterial[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                selectionSnapshot.materials[i] = colliders[i] != null ? colliders[i].sharedMaterial : null;
            }

            selectionSnapshot.valid = true;
            body.isKinematic = false;
            body.useGravity = false;
            body.mass = settings.selectedMass;
            body.inertiaTensor = settings.selectedInertiaTensor;

            if (settings.selectedPhysicsMaterial != null)
            {
                foreach (Collider targetCollider in colliders)
                {
                    if (targetCollider != null)
                    {
                        targetCollider.sharedMaterial = settings.selectedPhysicsMaterial;
                    }
                }
            }
        }

        internal void RestoreSelectionOverride()
        {
            if (!selectionSnapshot.valid || body == null)
            {
                return;
            }

            body.isKinematic = selectionSnapshot.isKinematic;
            body.useGravity = selectionSnapshot.useGravity;
            body.mass = selectionSnapshot.mass;
            body.inertiaTensor = selectionSnapshot.inertiaTensor;

            int materialCount = Mathf.Min(colliders.Length, selectionSnapshot.materials.Length);
            for (int i = 0; i < materialCount; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].sharedMaterial = selectionSnapshot.materials[i];
                }
            }

            selectionSnapshot.Clear();
        }

        internal void ClearContact(AttachableObject expected = null)
        {
            if (expected == null || contactCandidate == expected)
            {
                contactCandidate = null;
                contactAnchor = Vector3.zero;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (contactCandidate == null)
            {
                ReadContact(collision);
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            AttachableObject other = collision.collider.GetComponentInParent<AttachableObject>();
            if (other == contactCandidate)
            {
                contactAnchor = AverageContactPoint(collision);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            AttachableObject other = collision.collider.GetComponentInParent<AttachableObject>();
            ClearContact(other);
        }

        private void ReadContact(Collision collision)
        {
            AttachableObject other = collision.collider.GetComponentInParent<AttachableObject>();
            if (other == null || other == this || service == null || service.AreInSameIsland(this, other))
            {
                return;
            }

            contactCandidate = other;
            contactAnchor = AverageContactPoint(collision);
        }

        private static Vector3 AverageContactPoint(Collision collision)
        {
            if (collision.contactCount == 0)
            {
                return collision.transform.position;
            }

            Vector3 total = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                total += collision.GetContact(i).point;
            }

            return total / collision.contactCount;
        }
    }
}
