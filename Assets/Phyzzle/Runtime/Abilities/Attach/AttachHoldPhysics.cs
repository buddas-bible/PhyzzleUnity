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
            if (session == null || !session.IsHolding || session.HeldObject.Body == null ||
                settings == null || playerModel == null)
            {
                return;
            }

            Rigidbody body = session.HeldObject.Body;
            Vector3 worldTargetPosition = playerModel.TransformPoint(session.TargetLocalPosition);
            Quaternion worldTargetRotation = playerModel.rotation * session.TargetLocalRotation;

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

            Vector3 playerVelocity = playerBody != null ? playerBody.linearVelocity : Vector3.zero;
            Vector3 linearAcceleration = playerVelocity + session.LinearSpringVelocity - body.linearVelocity;
            linearAcceleration -= settings.velocityDamping * body.linearVelocity;
            body.AddForce(linearAcceleration, LegacyForceModeMap.ToUnity(LegacyForceType.VelocityChange));

            Vector3 angularAcceleration = session.AngularSpringVelocity;
            angularAcceleration -= settings.velocityDamping * body.angularVelocity;
            body.AddTorque(angularAcceleration, LegacyForceModeMap.ToUnity(LegacyForceType.VelocityChange));
        }
    }
}
