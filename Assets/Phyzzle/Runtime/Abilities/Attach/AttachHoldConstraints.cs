using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    internal static class AttachHoldConstraints
    {
        internal static Vector3 ClampTargetForBounds(
            Vector3 targetLocalPosition,
            float islandMinimumZ,
            bool hasIslandBounds,
            float minTargetY,
            float maxTargetY,
            float minTargetZ,
            float maxTargetZ)
        {
            float minimumZ = hasIslandBounds ? islandMinimumZ : targetLocalPosition.z;
            if (minimumZ < minTargetZ)
            {
                targetLocalPosition.z += minTargetZ - minimumZ;
            }

            if (targetLocalPosition.z > maxTargetZ)
            {
                targetLocalPosition.z = maxTargetZ;
            }

            targetLocalPosition.y = Mathf.Clamp(targetLocalPosition.y, minTargetY, maxTargetY);
            return targetLocalPosition;
        }

        internal static bool TryComputeIslandLocalBounds(
            AttachableObject heldObject,
            System.Collections.Generic.IReadOnlyCollection<AttachableObject> island,
            Transform playerModel,
            Vector3 targetLocalPosition,
            Quaternion targetLocalRotation,
            out Bounds bounds)
        {
            bounds = default;
            if (heldObject == null || heldObject.Body == null || island == null || playerModel == null)
            {
                return false;
            }

            Vector3 scale = heldObject.transform.lossyScale;
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
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Bounds currentBounds = collider.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 sign = new Vector3(
                            (corner & 1) == 0 ? -1f : 1f,
                            (corner & 2) == 0 ? -1f : 1f,
                            (corner & 4) == 0 ? -1f : 1f);
                        Vector3 currentWorld = currentBounds.center + Vector3.Scale(currentBounds.extents, sign);
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
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return currentPlayerRotation;
            }

            if (direction.sqrMagnitude <= targetPositionOffset * targetPositionOffset)
            {
                return desiredPlayerRotation;
            }

            Vector3 offsetTarget = objectPosition + direction.normalized * targetPositionOffset;
            Vector3 forward = offsetTarget - playerPosition;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                : currentPlayerRotation;
        }

        internal static Vector3 LimitLift(
            Vector3 candidateLocalTarget,
            Vector3 playerPosition,
            Quaternion playerRotation,
            Vector3 objectPosition,
            float targetPositionOffset)
        {
            Vector3 worldTarget = playerPosition + playerRotation * candidateLocalTarget;
            float verticalDistance = worldTarget.y - objectPosition.y;
            if (Mathf.Abs(verticalDistance) <= targetPositionOffset)
            {
                return candidateLocalTarget;
            }

            worldTarget = objectPosition + Vector3.up * Mathf.Sign(verticalDistance) * targetPositionOffset;
            Vector3 localTarget = Quaternion.Inverse(playerRotation) * (worldTarget - playerPosition);
            localTarget.x = 0f;
            return localTarget;
        }
    }
}
