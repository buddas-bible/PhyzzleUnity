using Phyzzle.Compatibility;
using UnityEngine;

namespace Phyzzle.Abilities.Attach
{
    internal sealed class AttachHoldPhysics
    {
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
