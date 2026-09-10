using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 들고 있는 부착 오브젝트의 이동 범위와 회전·높이 제한을 계산한다.
    /// </summary>
    internal static class AttachHoldConstraints
    {
        /// <summary>
        /// 대상 위치가 허용된 높이와 깊이 범위를 벗어나지 않도록 보정한다.
        /// </summary>
        internal static Vector3 ClampTargetForBounds(
            Vector3 targetLocalPosition,
            float islandMinimumZ,
            bool hasIslandBounds,
            float minTargetY,
            float maxTargetY,
            float minTargetZ,
            float maxTargetZ)
        {
            // 섬 전체 Bounds를 계산할 수 있으면 루트 위치가 아니라 가장 가까운 면을 기준으로 깊이를 제한
            float minimumZ = hasIslandBounds ? islandMinimumZ : targetLocalPosition.z;
            if (minimumZ < minTargetZ)
            {
                // Bounds가 최소 거리 안으로 들어온 만큼 루트 위치를 뒤로 밀어 섬 전체가 제한 밖에 남도록 보정
                targetLocalPosition.z += minTargetZ - minimumZ;
            }

            if (targetLocalPosition.z > maxTargetZ)
            {
                targetLocalPosition.z = maxTargetZ;
            }

            targetLocalPosition.y = Mathf.Clamp(targetLocalPosition.y, minTargetY, maxTargetY);
            return targetLocalPosition;
        }

        /// <summary>
        /// 부착 섬 전체가 후보 자세에 있을 때 플레이어 로컬 공간에서 차지하는 경계를 계산한다.
        /// </summary>
        internal static bool TryComputeIslandLocalBounds(
            AttachableObject heldObject,
            System.Collections.Generic.IReadOnlyCollection<AttachableObject> island,
            Transform playerModel,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            out Bounds bounds)
        {
            bounds = default;
            // Bounds 계산에 필요한 기준 오브젝트나 좌표계가 없으면 후보 자세를 만들 수 없음
            if (heldObject == null || heldObject.Body == null || island == null || playerModel == null)
            {
                return false;
            }

            Vector3 scale = heldObject.transform.lossyScale;
            // 현재 Root와 목표 Root의 변환 행렬을 만들어 섬의 모든 콜라이더를 목표 자세로 가상 이동시킴
            Matrix4x4 currentRoot = Matrix4x4.TRS(
                heldObject.Body.position,
                heldObject.Body.rotation,
                scale);
            Matrix4x4 candidateRoot = Matrix4x4.TRS(
                playerModel.TransformPoint(targetLocalPosition),
                playerModel.rotation * targetLocalRotation,
                scale);
            Matrix4x4 currentWorldToRoot = currentRoot.inverse;
            bool found = false;

            foreach (AttachableObject member in island)
            {
                if (member == null)
                {
                    continue;
                }

                foreach (Collider collider in member.GetComponentsInChildren<Collider>(true))
                {
                    // 비활성 콜라이더는 실제 충돌 영역에 포함되지 않으므로 Bounds 계산에서도 제외
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Bounds currentBounds = collider.bounds;
                    // AABB의 8개 꼭짓점을 모두 변환해 회전된 후보 자세에서도 정확한 새 AABB를 계산
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 sign = new Vector3(
                            (corner & 1) == 0 ? -1f : 1f,
                            (corner & 2) == 0 ? -1f : 1f,
                            (corner & 4) == 0 ? -1f : 1f);
                        Vector3 currentWorld = currentBounds.center + Vector3.Scale(currentBounds.extents, sign);

                        // 현재 World -> 현재 Root -> 목표 Root -> Player Local 순서로 같은 점의 목표 자세를 계산
                        Vector3 rootLocal = currentWorldToRoot.MultiplyPoint3x4(currentWorld);
                        Vector3 candidateWorld = candidateRoot.MultiplyPoint3x4(rootLocal);
                        Vector3 playerLocal = playerModel.InverseTransformPoint(candidateWorld);
                        if (!found)
                        {
                            bounds = new Bounds(playerLocal, Vector3.zero);
                            found = true;
                        }
                        else
                        {
                            bounds.Encapsulate(playerLocal);
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// 들고 있는 오브젝트와의 거리 제한을 유지하도록 플레이어의 수평 회전을 제한한다.
        /// </summary>
        internal static Quaternion LimitOrbit(
            Quaternion currentPlayerRotation,
            Quaternion desiredPlayerRotation,
            Vector3 playerPosition,
            Vector3 targetLocalPosition,
            Vector3 objectPosition,
            float targetPositionOffset)
        {
            Vector3 desiredWorldTarget = playerPosition + desiredPlayerRotation * targetLocalPosition;
            Vector3 direction = desiredWorldTarget - objectPosition;
            direction.y = 0f;
            // 수평 방향이 사실상 없으면 LookRotation을 만들 수 없으므로 현재 회전을 유지
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return currentPlayerRotation;
            }

            // 목표점과 오브젝트의 수평 거리가 허용 오프셋 안이면 원하는 회전을 그대로 허용
            if (direction.sqrMagnitude <= targetPositionOffset * targetPositionOffset)
            {
                return desiredPlayerRotation;
            }

            // 제한을 넘었다면 오브젝트에서 offset만큼 떨어진 점을 새 목표로 잡고 플레이어가 그 점까지만 바라보도록 회전을 제한
            Vector3 offsetTarget = objectPosition + direction.normalized * targetPositionOffset;
            Vector3 forward = offsetTarget - playerPosition;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                : currentPlayerRotation;
        }

        /// <summary>
        /// 대상과의 수직 거리 제한을 유지하도록 들기 위치를 보정한다.
        /// </summary>
        internal static Vector3 LimitLift(
            Vector3 candidateLocalTarget,
            Vector3 playerPosition,
            Quaternion playerRotation,
            Vector3 objectPosition,
            float targetPositionOffset)
        {
            Vector3 worldTarget = playerPosition + playerRotation * candidateLocalTarget;
            float verticalDistance = worldTarget.y - objectPosition.y;
            // 오브젝트와 목표점의 높이 차가 허용 범위 안이면 별도 보정 없이 사용
            if (Mathf.Abs(verticalDistance) <= targetPositionOffset)
            {
                return candidateLocalTarget;
            }

            // 오브젝트 기준 위/아래 offset 위치로 목표를 자른 뒤 다시 플레이어 로컬 좌표로 변환
            worldTarget = objectPosition + Vector3.up * Mathf.Sign(verticalDistance) * targetPositionOffset;
            Vector3 localTarget = Quaternion.Inverse(playerRotation) * (worldTarget - playerPosition);
            localTarget.x = 0f;
            return localTarget;
        }
    }
}
