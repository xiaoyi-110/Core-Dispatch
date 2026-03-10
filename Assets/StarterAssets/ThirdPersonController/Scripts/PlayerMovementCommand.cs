using Unity.Netcode;
using UnityEngine;

namespace StarterAssets
{
    public struct PlayerMovementCommand : INetworkSerializable
    {
        public uint Tick;
        public Vector3 ClientPosition;
        public Vector2 MoveInput;
        public float DeltaTime;
        public bool IsRunning;
        public bool IsSprinting;
        public bool JumpPressed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref ClientPosition);
            serializer.SerializeValue(ref MoveInput);
            serializer.SerializeValue(ref DeltaTime);
            serializer.SerializeValue(ref IsRunning);
            serializer.SerializeValue(ref IsSprinting);
            serializer.SerializeValue(ref JumpPressed);
        }
    }
}
