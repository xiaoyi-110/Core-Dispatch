using Unity.Netcode;
using UnityEngine;

namespace StarterAssets
{
    public struct PlayerMovementCommand : INetworkSerializable
    {
        public uint Tick;
        public Vector2 MoveInput;
        public float DeltaTime;
        public bool IsRunning;
        public bool IsSprinting;
        public bool JumpPressed;
        public float ClientTime;
        public float FacingYaw;
        public float MoveYaw;
        public bool AnalogMovement;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref MoveInput);
            serializer.SerializeValue(ref DeltaTime);
            serializer.SerializeValue(ref IsRunning);
            serializer.SerializeValue(ref IsSprinting);
            serializer.SerializeValue(ref JumpPressed);
            serializer.SerializeValue(ref ClientTime);
            serializer.SerializeValue(ref FacingYaw);
            serializer.SerializeValue(ref MoveYaw);
            serializer.SerializeValue(ref AnalogMovement);
        }
    }
}
