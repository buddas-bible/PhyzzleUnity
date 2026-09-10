using Phyzzle.Compatibility;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    /// <summary>
    /// 들고 있는 오브젝트가 목표 위치와 회전을 따라가도록 스프링 기반 물리 힘을 계산한다.
    /// </summary>
    internal sealed class AttachHoldPhysics
    {
        /// <summary>
        /// 현재 들기 세션의 목표 자세를 기준으로 선형·각속도 보정 힘을 적용한다.
        /// </summary>
        public void Tick(
            AttachHoldSession session,
            Transform playerModel,
            Rigidbody playerBody,
            AttachSettings settings,
            float fixedDeltaTime)
        {
            // 들기 상태가 아니거나 목표 자세 계산에 필요한 참조가 없으면 물리 힘을 적용하지 않음
            if (session == null || !session.IsHolding || session.HeldObject.Body == null ||
                settings == null || playerModel == null)
            {
                return;
            }

            Rigidbody body = session.HeldObject.Body;
            // 들기 목표는 플레이어 로컬 좌표로 저장하므로 물리 계산 전에 World 좌표로 변환
            Vector3 worldTargetPosition = playerModel.TransformPoint(session.TargetLocalPosition);
            Quaternion worldTargetRotation = playerModel.rotation * session.TargetLocalRotation;

            // 위치 오차를 감쇠 스프링 속도로 변환하고 한 프레임에 줄 수 있는 최대 보정량을 제한
            session.LinearSpringVelocity = LegacySpringMath.UpdatePositionVelocity(
                body.position,
                session.LinearSpringVelocity,
                worldTargetPosition,
                settings.linearDampingRatio,
                settings.linearFrequency,
                fixedDeltaTime);
            session.LinearSpringVelocity = Vector3.ClampMagnitude(
                session.LinearSpringVelocity,
                settings.maxLinearAcceleration);

            // 회전 오차도 동일한 방식으로 각속도 보정값을 계산
            session.AngularSpringVelocity = LegacySpringMath.UpdateAngularVelocity(
                body.rotation,
                session.AngularSpringVelocity,
                worldTargetRotation,
                settings.angularDampingRatio,
                settings.angularFrequency,
                fixedDeltaTime);
            session.AngularSpringVelocity = Vector3.ClampMagnitude(
                session.AngularSpringVelocity,
                settings.maxAngularAcceleration);

            // 플레이어 자체 속도를 더해 이동 중에도 상대적인 들기 위치가 밀리지 않도록 하고 현재 속도와 감쇠분을 제거
            Vector3 playerVelocity = playerBody != null ? playerBody.linearVelocity : Vector3.zero;
            Vector3 linearAcceleration = playerVelocity + session.LinearSpringVelocity - body.linearVelocity;
            linearAcceleration -= settings.velocityDamping * body.linearVelocity;
            body.AddForce(linearAcceleration, LegacyForceModeMap.ToUnity(LegacyForceType.VelocityChange));

            // 각속도는 목표 스프링 속도에서 현재 회전 속도에 대한 감쇠만 빼서 적용
            Vector3 angularAcceleration = session.AngularSpringVelocity;
            angularAcceleration -= settings.velocityDamping * body.angularVelocity;
            body.AddTorque(angularAcceleration, LegacyForceModeMap.ToUnity(LegacyForceType.VelocityChange));
        }
    }
}
