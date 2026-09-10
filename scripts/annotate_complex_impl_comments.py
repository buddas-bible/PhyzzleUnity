#!/usr/bin/env python3
from pathlib import Path

REPLACEMENTS = {
    "Assets/Phyzzle/Runtime/Abilities/Attach/Visuals/AttachProjectionRenderer.cs": [
        (
            "            if (camera == null || settings == null || projectionMaterial == null || currentSurfaces == null ||\n                !HasSourceMesh())\n",
            "            // 카메라, 설정, Material 또는 투영할 Mesh가 하나라도 없으면 Draw를 만들 수 없으므로 종료\n",
        ),
        (
            "            if (!TryGetCombinedBounds(out Bounds combinedBounds))\n",
            "            // 투영 방향의 시작점을 정하려면 섬 전체 Bounds가 필요하므로 유효한 Renderer/Collider가 없으면 종료\n",
        ),
        (
            "            Vector3 fallbackForward = lastPlanarForward.sqrMagnitude > PlanarEpsilon\n",
            "            // 카메라가 위/아래를 거의 수직으로 볼 때 수평 Forward가 0이 되므로 직전 유효 방향을 fallback으로 사용\n",
        ),
        (
            "            float fadeStep = Mathf.Max(0f, deltaTime) / SurfaceFadeSeconds;\n",
            "            // 매 프레임 dt / fadeDuration만큼 0~1 상태를 이동시켜 프레임레이트와 무관한 페이드 속도를 만듦\n",
        ),
        (
            "            if (Mathf.Abs(Vector3.Dot(direction, normal)) < parallelThreshold)\n",
            "            // N·D가 0에 가까우면 투영선과 평면이 거의 평행이라 교점 계산의 분모가 불안정해지므로 제외\n",
        ),
        (
            "            float denominator = Vector3.Dot(normal, direction);\n",
            "            // 평면식 N·P + d = 0, 투영점 P' = P - D*t에서 t = (N·P + d) / (N·D)\n",
        ),
        (
            "            Bounds projected = new(ProjectPoint(new Vector3(min.x, min.y, min.z), direction, plane), Vector3.zero);\n",
            "            // 회전된 투영 결과는 원본 AABB 크기를 그대로 쓸 수 없으므로 8개 꼭짓점을 모두 투영해 새 Bounds를 구성\n",
        ),
        (
            "            Vector3 origin = GetCastOrigin(combinedBounds, direction, settings.projectionSurfaceBias);\n",
            "            // 섬 Bounds의 바깥면에서 해당 방향으로 Ray를 쏴 가장 먼저 만나는 수신 표면을 찾음\n",
        ),
        (
            "            if (hasHit && !current.Matches(hit, settings.projectionDepthTolerance))\n",
            "            // 같은 방향에서 수신 표면이 바뀌면 기존 표면을 즉시 끄지 않고 retiring 슬롯으로 넘겨 교차 페이드\n",
        ),
        (
            "                // Keep the stronger old surface when several receivers change inside one fade.\n",
            "                // 페이드 도중 표면이 연속으로 바뀌면 opacity가 더 높은 기존 표면을 retiring으로 유지해 깜빡임을 줄임\n",
        ),
        (
            "            current.Fade(hasHit, fadeStep);\n            retiring.Fade(false, fadeStep);\n",
            "            // 새 표면은 fade-in, 이전 표면은 fade-out을 동시에 진행한 뒤 두 표면을 모두 Draw\n",
        ),
        (
            "            if (surface.Opacity <= 0f || !surface.TryGetPlane(out Vector4 plane) ||\n",
            "            // 완전히 투명하거나 유효 평면이 없거나 투영 방향과 평면이 평행한 경우 Shader의 평면 투영식을 실행하지 않음\n",
        ),
        (
            "                worldBounds = ProjectBounds(combinedBounds, surface.Direction, plane),\n",
            "                // Graphics.RenderMesh의 frustum culling도 투영된 위치를 기준으로 해야 하므로 projected Bounds를 전달\n",
        ),
    ],
    "Assets/Phyzzle/Runtime/Abilities/Attach/Visuals/AttachVisualController.cs": [
        (
            "            if (ability == null || targeting == null || holdController == null || attachmentService == null ||\n",
            "            // 필요한 시스템이 비활성/누락되거나 다른 RenderPipeline이면 이전 Rendering Layer와 전역 Shader 값까지 즉시 정리\n",
        ),
        (
            "            if (ability.State == AttachAbilityController.AbilityState.Selecting)\n",
            "            // 선택 상태에서는 Eligible/Focused만 사용하므로 들기 전용 Projection/Tether/Contact Preview는 제거\n",
        ),
        (
            "                if (heldRoot == null || !heldRoot.isActiveAndEnabled)\n",
            "                // 들던 Root가 파괴/비활성화되면 캐시된 Renderer 역할이 남지 않도록 전체 시각 상태를 정리\n",
        ),
        (
            "                if (heldRoot != observedHeldRoot || observedTopologyVersion != attachmentService.TopologyVersion)\n",
            "                // Root가 바뀌거나 Attach/Detach로 그래프 구조가 변한 경우에만 섬과 Renderer 목록을 다시 수집\n",
        ),
        (
            "            if (attachable == null ||\n                (desiredObjectRoles.TryGetValue(attachable, out uint current) && RolePriority(current) >= RolePriority(role)))\n",
            "            // 한 오브젝트가 여러 상태에 걸리면 Held > Focused > Eligible 우선순위가 높은 역할 하나만 유지\n",
        ),
        (
            "            if (SameObjectRoles(desiredObjectRoles, appliedObjectRoles))\n",
            "            // 오브젝트 역할이 지난 프레임과 같으면 GetComponentsInChildren과 Rendering Layer 재적용을 생략\n",
        ),
        (
            "                pair.Key.renderingLayerMask = (pair.Key.renderingLayerMask & ~AttachVisualLayers.Owned) | pair.Value;\n",
            "                // 다른 시스템의 Rendering Layer는 보존하고 Attach가 소유한 비트만 지운 뒤 새 역할 비트를 적용\n",
        ),
        (
            "            if (!ReferenceEquals(observedHeldRoot, null) &&\n                (observedHeldRoot == null || !observedHeldRoot.isActiveAndEnabled))\n",
            "            // Unity Object의 == null은 Destroy된 객체도 true가 되므로 ReferenceEquals로 '한 번이라도 들었던 Root'인지 먼저 구분\n",
        ),
        (
            "            if (visualBlend <= 0f)\n",
            "            // 이미 완전히 사라진 상태라면 남은 캐시와 Rendering Layer를 즉시 정리하고 더 이상 Draw하지 않음\n",
        ),
        (
            "            return duration <= 0f\n                ? target\n                : Mathf.MoveTowards(current, target, Mathf.Max(0f, deltaTime) / duration);\n",
            "            // deltaTime / duration을 한 프레임 이동량으로 사용하면 총 duration 동안 0~1 전체 구간을 일정하게 이동\n",
        ),
    ],
    "Assets/Phyzzle/Runtime/Abilities/Rewind/Visuals/RewindVisualController.cs": [
        (
            "            if (ability == null || targeting == null || playerRoot == null || settings == null)\n",
            "            // 필수 참조가 하나라도 없으면 이전 프레임의 Rendering Layer/전역 Shader 상태가 남지 않도록 전체 정리\n",
        ),
        (
            "            if (!SupportsPipeline(supportedPipeline, GraphicsSettings.currentRenderPipeline) ||\n",
            "            // 구성 당시와 다른 RenderPipeline이거나 능력 시스템이 비활성 상태면 전용 RenderFeature를 사용할 수 없음\n",
        ),
        (
            "                if (!ReferenceEquals(cachedTarget, null) &&\n                    (cachedTarget == null || !cachedTarget.isActiveAndEnabled))\n",
            "                // Unity의 Destroy된 Object는 == null로 보이므로 ReferenceEquals로 실제 캐시가 있었는지 구분한 뒤 즉시 정리\n",
        ),
        (
            "            RemoveOwnedLayers();\n            AddRole(playerRoot, RewindVisualLayers.PlayerPreserve, excludePreview: true);\n",
            "            // 매 선택 프레임마다 이전 역할 비트를 걷어낸 뒤 Player Preserve -> Eligible -> Active 순서로 현재 상태를 다시 구성\n",
        ),
        (
            "            if (current == null || !current.isActiveAndEnabled || current.Body == null)\n",
            "            // 선택 대상이 사라지면 이전 대상의 경로/고스트가 화면에 남지 않도록 Preview 캐시를 비움\n",
        ),
        (
            "            if (current == cachedTarget && snapshotCount == cachedSnapshotCount)\n",
            "            // 같은 대상의 Snapshot 수가 그대로면 경로와 Ghost Geometry도 동일하므로 재샘플링을 생략\n",
        ),
        (
            "            current.CopyHistoryNewestFirst(history);\n            RewindPreviewSampler.Build(\n",
            "            // 최신 -> 과거 순서의 전체 기록에서 화면용 Path와 제한된 개수의 Ghost 샘플만 다시 추출\n",
        ),
        (
            "            if (pathSamples.Count < 2 || previewMaterial == null)\n",
            "            // 선을 만들 최소 두 점이 없거나 Material이 없으면 캐시는 유지하되 실제 Preview Geometry만 숨김\n",
        ),
        (
            "                meshSources.Add(new MeshSource(\n",
            "                // 대상 Root가 과거 Pose로 이동해도 자식 Mesh 배치가 유지되도록 위치/회전/스케일을 Root 로컬 기준으로 저장\n",
        ),
        (
            "                ghost.Object.transform.SetPositionAndRotation(sample.Position, sample.Rotation);\n",
            "                // Ghost Root를 과거 Pose에 놓고 자식 Mesh는 저장해둔 로컬 변환을 재사용\n",
        ),
        (
            "                int materialCount = Mathf.Max(1, source.Mesh.subMeshCount);\n",
            "                // MeshRenderer.sharedMaterials 수를 subMesh 수와 맞춰 모든 SubMesh가 Preview Material로 렌더되게 함\n",
        ),
        (
            "            return new Vector3(\n                Mathf.Abs(divisor.x) > 0.000001f ? value.x / divisor.x : value.x,\n",
            "            // 부모 Scale이 0에 가까운 축은 나눗셈이 발산하므로 해당 축의 원래 값을 그대로 사용\n",
        ),
        (
            "            Color ghostColor = settings.activeVisualColor;\n            ghostColor.a = Mathf.Clamp01(settings.previewGhostAlpha) * selectionBlend;\n",
            "            // Ghost 자체 alpha와 전체 선택 fade를 곱해 개별 Ghost 강도와 화면 전환 강도를 독립적으로 조절\n",
        ),
    ],
}


def apply_replacements(path: Path, replacements: list[tuple[str, str]]) -> int:
    text = path.read_text(encoding="utf-8")
    changed = 0
    for needle, comment in replacements:
        if comment in text:
            continue
        count = text.count(needle)
        if count != 1:
            raise RuntimeError(f"{path}: expected one match, found {count}: {needle[:80]!r}")
        text = text.replace(needle, comment + needle, 1)
        changed += comment.count("\n")
    path.write_text(text, encoding="utf-8", newline="")
    return changed


def main() -> None:
    total = 0
    for filename, replacements in REPLACEMENTS.items():
        total += apply_replacements(Path(filename), replacements)
    print(f"inserted {total} implementation comment lines")


if __name__ == "__main__":
    main()
