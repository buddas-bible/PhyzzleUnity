using System;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 부착 시스템에 참여하는 리지드바디와 접촉 후보, 선택 중 물리 상태를 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AttachableObject : MonoBehaviour
    {
        /// <summary>
        /// 선택 전 리지드바디와 콜라이더 물리 설정을 임시 보관한다.
        /// </summary>
        [Serializable]
        private sealed class SelectionSnapshot
        {
            public bool valid;
            public bool isKinematic;
            public bool useGravity;
            public float mass;
            public Vector3 inertiaTensor;
            public PhysicsMaterial[] materials = Array.Empty<PhysicsMaterial>();

            /// <summary>
            /// 저장된 선택 전 상태를 무효화하고 재사용 가능한 초기 상태로 되돌린다.
            /// </summary>
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

        /// <summary>
        /// 인스펙터 초기화 시 동일 오브젝트의 Rigidbody 참조를 자동으로 연결한다.
        /// </summary>
        private void Reset()
        {
            body = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// 런타임 시작 시 물리 참조, 콜라이더 목록과 부착 서비스를 준비한다.
        /// </summary>
        private void Awake()
        {
            body ??= GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            service ??= FindAnyObjectByType<AttachmentService>();
        }

        /// <summary>
        /// 활성화될 때 현재 부착 서비스에 자신을 등록한다.
        /// </summary>
        private void OnEnable()
        {
            service ??= FindAnyObjectByType<AttachmentService>();
            service?.Register(this);
        }

        /// <summary>
        /// 비활성화될 때 서비스 등록과 현재 접촉 후보를 정리한다.
        /// </summary>
        private void OnDisable()
        {
            service?.Unregister(this);
            contactCandidate = null;
            contactAnchor = Vector3.zero;
        }

        /// <summary>
        /// 사용할 부착 서비스를 교체하고 활성 상태라면 새 서비스에 다시 등록한다.
        /// </summary>
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

        /// <summary>
        /// 현재 물리 설정을 저장하고 선택 중 사용할 질량, 중력과 재질 설정을 적용한다.
        /// </summary>
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

        /// <summary>
        /// 선택 전 저장해 둔 리지드바디와 콜라이더 물리 설정을 정확히 복원한다.
        /// </summary>
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

        /// <summary>
        /// 현재 접촉 후보가 지정한 대상과 일치하거나 제한이 없으면 접촉 정보를 초기화한다.
        /// </summary>
        internal void ClearContact(AttachableObject expected = null)
        {
            if (expected == null || contactCandidate == expected)
            {
                contactCandidate = null;
                contactAnchor = Vector3.zero;
            }
        }

        /// <summary>
        /// 새 충돌이 시작되면 아직 후보가 없을 때 부착 가능한 접촉 대상을 읽는다.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (contactCandidate == null)
            {
                ReadContact(collision);
            }
        }

        /// <summary>
        /// 현재 접촉 후보와 충돌이 유지되는 동안 접촉 앵커 위치를 갱신한다.
        /// </summary>
        private void OnCollisionStay(Collision collision)
        {
            AttachableObject other = collision.collider.GetComponentInParent<AttachableObject>();
            if (other == contactCandidate)
            {
                contactAnchor = AverageContactPoint(collision);
            }
        }

        /// <summary>
        /// 충돌이 끝난 대상이 현재 접촉 후보라면 접촉 정보를 제거한다.
        /// </summary>
        private void OnCollisionExit(Collision collision)
        {
            AttachableObject other = collision.collider.GetComponentInParent<AttachableObject>();
            ClearContact(other);
        }

        /// <summary>
        /// 충돌 상대가 다른 부착 섬의 유효한 대상이면 접촉 후보와 앵커를 저장한다.
        /// </summary>
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

        /// <summary>
        /// 충돌의 모든 접촉 지점을 평균내 부착 앵커로 사용할 월드 위치를 계산한다.
        /// </summary>
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
