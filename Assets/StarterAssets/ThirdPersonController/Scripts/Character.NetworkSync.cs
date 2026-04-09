using Unity.Netcode;
using UnityEngine;
using Utility;

namespace StarterAssets
{
    public partial class Character
    {
        [System.Serializable]
        public struct CharacterNetState : INetworkSerializable
        {
            public bool IsAiming;
            public Vector3 AimTarget;
            public Vector2 AimedMoveSpeed;
            public float MoveSpeed;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref IsAiming);
                serializer.SerializeValue(ref AimTarget);
                serializer.SerializeValue(ref AimedMoveSpeed);
                serializer.SerializeValue(ref MoveSpeed);
            }
        }

        [ServerRpc]
        public void SubmitNetStateServerRpc(CharacterNetState state, ServerRpcParams rpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(rpcParams.Receive.SenderClientId, OwnerClientId, "NetState", true))
            {
                return;
            }
            _isAiming = state.IsAiming;
            _aimTarget = state.AimTarget;
            _aimedMoveSpeed = state.AimedMoveSpeed;
            _moveSpeed = state.MoveSpeed;
            SubmitNetStateClientRpc(state);
        }

        [ClientRpc]
        private void SubmitNetStateClientRpc(CharacterNetState state)
        {
            if (!IsOwner)
            {
                _isAiming = state.IsAiming;
                _aimTarget = state.AimTarget;
                _aimedMoveSpeed = state.AimedMoveSpeed;
                _moveSpeed = state.MoveSpeed;
            }
        }

        // Legacy per-field RPCs removed in favor of SubmitNetStateServerRpc.
    }
}
