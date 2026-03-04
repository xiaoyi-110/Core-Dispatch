using Unity.Netcode;
using UnityEngine;

namespace StarterAssets
{
    public partial class Character
    {
        [ServerRpc]
        public void OnAimTargetChangedServerRpc(Vector3 value)
        {
            _aimTarget = value;
            OnAimTargetChangedClientRpc(value);
        }

        [ClientRpc]
        public void OnAimTargetChangedClientRpc(Vector3 value)
        {
            if (!IsOwner)
            {
                _aimTarget = value;
            }
        }

        [ServerRpc]
        public void OnAimingMoveChangedServerRpc(Vector2 value)
        {
            _aimedMoveSpeed = value;
            OnAimingMoveChangedClientRpc(value);
        }

        [ClientRpc]
        public void OnAimingMoveChangedClientRpc(Vector2 value)
        {
            if (!IsOwner)
            {
                _aimedMoveSpeed = value;
            }
        }

        [ServerRpc]
        public void OnAimingChangedServerRpc(bool value)
        {
            _isAiming = value;
            OnAimingChangedClientRpc(value);
        }

        [ClientRpc]
        public void OnAimingChangedClientRpc(bool value)
        {
            if (!IsOwner)
            {
                _isAiming = value;
            }
        }

        [ServerRpc]
        public void OnMoveSpeedChangedServerRpc(float value)
        {
            _moveSpeed = value;
            OnMoveSpeedChangedClientRpc(value);
        }

        [ClientRpc]
        public void OnMoveSpeedChangedClientRpc(float value)
        {
            if (!IsOwner)
            {
                _moveSpeed = value;
            }
        }
    }
}
