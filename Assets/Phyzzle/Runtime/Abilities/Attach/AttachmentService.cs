using System;
using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 부착 오브젝트의 등록, 연결 그래프, 물리 조인트와 선택 섬 상태를 관리한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttachmentService : MonoBehaviour
    {
        /// <summary>
        /// 두 부착 오브젝트 사이의 연결을 순서와 무관하게 식별하는 키다.
        /// </summary>
        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly EntityId first;
            private readonly EntityId second;

            /// <summary>
            /// 두 오브젝트의 엔티티 ID를 정렬해 고유한 연결 키를 생성한다.
            /// </summary>
            public EdgeKey(AttachableObject a, AttachableObject b)
            {
                EntityId aId = a.GetEntityId();
                EntityId bId = b.GetEntityId();
                // A-B와 B-A가 같은 연결 키가 되도록 ID가 작은 쪽을 항상 first에 저장
                first = aId < bId ? aId : bId;
                second = aId < bId ? bId : aId;
            }

            /// <summary>
            /// 두 연결 키가 같은 엔티티 쌍을 나타내는지 비교한다.
            /// </summary>
            public bool Equals(EdgeKey other) => first == other.first && second == other.second;

            /// <summary>
            /// 객체가 동일한 연결 키인지 비교한다.
            /// </summary>
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);

            /// <summary>
            /// 정렬된 두 엔티티 ID를 기준으로 해시 코드를 생성한다.
            /// </summary>
            public override int GetHashCode() => HashCode.Combine(first, second);
        }

        [SerializeField] private AttachSettings settings;

        private readonly AttachmentGraph<AttachableObject> graph = new();
        private readonly Dictionary<EdgeKey, FixedJoint> joints = new();

        public AttachSettings Settings => settings;
        internal int TopologyVersion { get; private set; }

        /// <summary>
        /// 부착 처리에 사용할 설정을 구성한다.
        /// </summary>
        public void Configure(AttachSettings attachSettings)
        {
            settings = attachSettings;
        }

        /// <summary>
        /// 부착 가능 오브젝트를 그래프에 등록하고 토폴로지 버전을 갱신한다.
        /// </summary>
        public void Register(AttachableObject attachable)
        {
            if (attachable != null)
            {
                int nodeCount = graph.NodeCount;
                graph.Add(attachable);
                if (graph.NodeCount != nodeCount)
                {
                    TopologyVersion++;
                }
            }
        }

        /// <summary>
        /// 오브젝트의 모든 연결을 해제한 뒤 부착 그래프에서 등록을 제거한다.
        /// </summary>
        public void Unregister(AttachableObject attachable)
        {
            if (attachable == null)
            {
                return;
            }

            // 노드를 그래프에서 지우기 전에 연결된 Joint와 간선을 먼저 해제
            Detach(attachable);
            if (graph.Remove(attachable))
            {
                TopologyVersion++;
            }
        }

        /// <summary>
        /// 두 부착 오브젝트가 같은 연결 섬에 속하는지 확인한다.
        /// </summary>
        public bool AreInSameIsland(AttachableObject first, AttachableObject second)
        {
            return first != null && second != null && graph.AreInSameComponent(first, second);
        }

        /// <summary>
        /// 지정한 오브젝트와 연결된 전체 부착 섬을 반환한다.
        /// </summary>
        public IReadOnlyCollection<AttachableObject> GetIsland(AttachableObject attachable)
        {
            return attachable == null
                ? Array.Empty<AttachableObject>()
                : graph.GetComponent(attachable);
        }

        /// <summary>
        /// 지정한 오브젝트에 직접 연결된 이웃 오브젝트를 반환한다.
        /// </summary>
        public IReadOnlyCollection<AttachableObject> GetDirectConnections(AttachableObject attachable)
        {
            return attachable == null
                ? Array.Empty<AttachableObject>()
                : graph.GetNeighbors(attachable);
        }

        /// <summary>
        /// 지정한 오브젝트가 속한 섬 전체에 선택 중 물리 설정을 적용한다.
        /// </summary>
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

        /// <summary>
        /// 지정한 오브젝트가 속한 섬 전체의 선택 중 물리 설정을 원래 값으로 복원한다.
        /// </summary>
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

        /// <summary>
        /// 현재 섬의 어느 구성원이 다른 부착 가능한 섬과 접촉 중인지 확인한다.
        /// </summary>
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

        /// <summary>
        /// 현재 섬에서 첫 유효 접촉 후보를 찾아 실제 부착 연결을 생성한다.
        /// </summary>
        public bool TryAttach(AttachableObject attachable)
        {
            if (attachable == null)
            {
                return false;
            }

            // 그래프의 섬 순서대로 탐색해 프리뷰에서 보여준 것과 같은 첫 접촉 후보를 실제 연결 대상으로 사용
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
                    // 연결이 만들어진 접촉 정보는 양쪽 모두에서 제거해 다음 프레임에 같은 연결을 다시 시도하지 않음
                    member.ClearContact(other);
                    other.ClearContact(member);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 현재 유효한 부착 연결의 FixedJoint 목록을 지정한 컬렉션에 복사한다.
        /// </summary>
        internal void CopyConnectionJoints(List<FixedJoint> destination)
        {
            destination.Clear();
            foreach (FixedJoint joint in joints.Values)
            {
                if (joint != null && joint.connectedBody != null)
                {
                    destination.Add(joint);
                }
            }
        }

        /// <summary>
        /// 들고 있는 섬에서 실제 부착 순서와 같은 첫 접촉 후보를 프리뷰용으로 찾는다.
        /// </summary>
        internal bool TryGetPreviewContact(
            IReadOnlyList<AttachableObject> heldIsland,
            out AttachableObject member,
            out AttachableObject other,
            out Vector3 worldAnchor)
        {
            member = null;
            other = null;
            worldAnchor = default;

            if (heldIsland == null)
            {
                return false;
            }

            // The caller keeps this BFS island current with TopologyVersion, matching TryAttach order.
            // TryAttach와 결과가 달라지지 않도록 캐시된 BFS 섬의 순서를 그대로 사용
            for (int i = 0; i < heldIsland.Count; i++)
            {
                AttachableObject candidateMember = heldIsland[i];
                if (candidateMember == null || candidateMember.Body == null)
                {
                    continue;
                }

                AttachableObject candidateOther = candidateMember.ContactCandidate;
                if (candidateOther == null || candidateOther == candidateMember || candidateOther.Body == null)
                {
                    continue;
                }

                // ponytail: scan small cached islands; use a reused membership set if large islands need it.
                // 접촉 후보가 이미 들고 있는 같은 섬의 구성원인지 확인해 내부 연결은 프리뷰 대상에서 제외
                bool isInHeldIsland = false;
                for (int j = 0; j < heldIsland.Count; j++)
                {
                    if (heldIsland[j] == candidateOther)
                    {
                        isInHeldIsland = true;
                        break;
                    }
                }

                if (isInHeldIsland)
                {
                    continue;
                }

                // Suppress an inactive first candidate instead of previewing a different TryAttach result.
                // 첫 후보가 비활성이면 뒤 후보를 대신 보여주지 않음. 그래야 실제 TryAttach의 첫 후보 순서와 화면 결과가 일치
                if (!candidateMember.isActiveAndEnabled || !candidateOther.isActiveAndEnabled)
                {
                    return false;
                }

                member = candidateMember;
                other = candidateOther;
                worldAnchor = candidateMember.ContactAnchor;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 두 오브젝트 사이에 FixedJoint와 그래프 간선을 생성해 하나의 부착 섬으로 연결한다.
        /// </summary>
        public bool Attach(AttachableObject first, AttachableObject second, Vector3 worldAnchor)
        {
            // 자기 자신, Rigidbody가 없는 대상, 이미 같은 섬인 대상은 중복 Joint를 만들 수 없으므로 종료
            if (first == null || second == null || first == second ||
                first.Body == null || second.Body == null || AreInSameIsland(first, second))
            {
                return false;
            }

            FixedJoint joint = first.gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = second.Body;
            joint.autoConfigureConnectedAnchor = false;
            // 같은 World 접점을 각 Rigidbody의 로컬 좌표로 변환해야 두 anchor가 정확히 같은 지점을 가리킴
            joint.anchor = first.transform.InverseTransformPoint(worldAnchor);
            joint.connectedAnchor = second.transform.InverseTransformPoint(worldAnchor);
            joint.breakForce = float.PositiveInfinity;
            joint.breakTorque = float.PositiveInfinity;
            joint.enableCollision = false;
            joint.enablePreprocessing = true;

            if (graph.Connect(first, second))
            {
                TopologyVersion++;
            }
            // 그래프 간선과 실제 PhysX Joint를 같은 무방향 EdgeKey로 연결해 분리 시 바로 찾을 수 있게 함
            joints[new EdgeKey(first, second)] = joint;
            return true;
        }

        /// <summary>
        /// 지정한 오브젝트의 직접 연결 조인트와 그래프 간선을 모두 제거하고 선택 상태를 복원한다.
        /// </summary>
        public bool Detach(AttachableObject attachable)
        {
            if (attachable == null)
            {
                return false;
            }

            // Disconnect 중 그래프의 이웃 목록이 바뀌므로 먼저 복사해서 순회 안정성을 보장
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
                    // 플레이 중에는 Unity의 지연 Destroy, EditMode에서는 즉시 제거를 사용
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

            // 분리 뒤에는 하나의 섬이 여러 컴포넌트로 나뉠 수 있으므로 각 former neighbor에서 새 컴포넌트를 다시 순회
            foreach (AttachableObject formerNeighbor in neighbors)
            {
                foreach (AttachableObject member in graph.GetComponent(formerNeighbor))
                {
                    member?.RestoreSelectionOverride();
                }
            }

            TopologyVersion++;

            return true;
        }
    }
}
