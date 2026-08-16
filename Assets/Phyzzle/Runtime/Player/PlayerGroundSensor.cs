using System.Collections.Generic;
using UnityEngine;

namespace Phyzzle.Player
{
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

        private void Awake()
        {
            ownerBody = GetComponentInParent<Rigidbody>();
            Collider sensor = GetComponent<Collider>();
            sensor.isTrigger = true;
        }

        private void OnDisable()
        {
            contacts.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!ShouldIgnore(other))
            {
                contacts.Add(other);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!ShouldIgnore(other))
            {
                contacts.Add(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            contacts.Remove(other);
        }

        private bool ShouldIgnore(Collider other)
        {
            return other == null || (ownerBody != null && other.attachedRigidbody == ownerBody);
        }
    }
}
