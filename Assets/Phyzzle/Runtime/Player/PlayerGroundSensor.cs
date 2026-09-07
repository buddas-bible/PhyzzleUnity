using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Player
{
    /// <summary>
    /// 플레이어 발밑 트리거 접촉을 추적해 현재 지면 접촉 여부를 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PlayerGroundSensor : MonoBehaviour
    {
        private readonly HashSet<Collider> contacts = new();
        private Rigidbody ownerBody;

        public bool IsGrounded
        {
            get
            {
                contacts.RemoveWhere(contact => contact == null || ShouldIgnore(contact));
                return contacts.Count > 0;
            }
        }

        /// <summary>
        /// 소유 리지드바디와 센서 콜라이더를 초기화하고 센서를 트리거로 설정한다.
        /// </summary>
        private void Awake()
        {
            ownerBody = GetComponentInParent<Rigidbody>();
            Collider sensor = GetComponent<Collider>();
            sensor.isTrigger = true;
        }

        /// <summary>
        /// 비활성화될 때 기록된 지면 접촉 정보를 모두 초기화한다.
        /// </summary>
        private void OnDisable()
        {
            contacts.Clear();
        }

        /// <summary>
        /// 유효한 콜라이더가 센서에 진입하면 지면 접촉 목록에 추가한다.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!ShouldIgnore(other))
            {
                contacts.Add(other);
            }
        }

        /// <summary>
        /// 센서 안에 머무는 유효한 콜라이더를 지면 접촉 목록에 유지한다.
        /// </summary>
        private void OnTriggerStay(Collider other)
        {
            if (!ShouldIgnore(other))
            {
                contacts.Add(other);
            }
        }

        /// <summary>
        /// 센서에서 나간 콜라이더를 지면 접촉 목록에서 제거한다.
        /// </summary>
        private void OnTriggerExit(Collider other)
        {
            contacts.Remove(other);
        }

        /// <summary>
        /// 자기 자신이거나 유효하지 않은 콜라이더인지 판별한다.
        /// </summary>
        private bool ShouldIgnore(Collider other)
        {
            return other == null || (ownerBody != null && other.attachedRigidbody == ownerBody);
        }
    }
}
