using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace StarterAssets
{
    public partial class Character
    {
        private struct RemoteSnapshot
        {
            public uint Tick;
            public Vector3 Position;
            public float ReceivedAt;
        }

        [Header("Server Move Validation")]
        [SerializeField] private float serverWalkSpeed = 2.0f;
        [SerializeField] private float serverRunSpeed = 4.0f;
        [SerializeField] private float serverSprintSpeed = 5.335f;
        [SerializeField] private float serverDistanceTolerance = 0.15f;

        [Header("Move Correction")]
        [SerializeField] private float correctionDistanceThreshold = 0.08f;
        [SerializeField] private float correctionSmoothTime = 0.15f;
        [SerializeField] private float rejectCorrectionSmoothTime = 0.08f;

        [Header("Remote Interpolation")]
        [SerializeField] private bool enableRemoteInterpolation = true;
        [SerializeField] private float interpolationBackTime = 0.12f;
        [SerializeField] private float remoteCatchupLerp = 12f;

        private uint _lastMoveCmdTick;
        private bool _hasAuthoritativeMoveState;
        private Vector3 _authoritativePosition;
        private readonly List<RemoteSnapshot> _remoteSnapshots = new List<RemoteSnapshot>();

        [ServerRpc]
        public void SubmitMovementCommandServerRpc(PlayerMovementCommand cmd, ServerRpcParams rpcParams = default)
        {
            if (cmd.Tick <= _lastMoveCmdTick)
            {
                return;
            }
            _lastMoveCmdTick = cmd.Tick;

            if (!_hasAuthoritativeMoveState)
            {
                _authoritativePosition = transform.position;
                _hasAuthoritativeMoveState = true;
            }

            float clampedDelta = Mathf.Clamp(cmd.DeltaTime, 0.001f, 0.1f);
            float maxSpeed = cmd.IsSprinting ? serverSprintSpeed : (cmd.IsRunning ? serverRunSpeed : serverWalkSpeed);
            float maxAllowedDistance = maxSpeed * clampedDelta * 1.35f + serverDistanceTolerance;
            float distanceFromAuthoritative = Vector3.Distance(_authoritativePosition, cmd.ClientPosition);

            bool rejected = distanceFromAuthoritative > maxAllowedDistance;
            if (!rejected)
            {
                _authoritativePosition = cmd.ClientPosition;
            }

            ulong sender = rpcParams.Receive.SenderClientId;
            ClientRpcParams target = default;
            target.Send.TargetClientIds = new[] { sender };
            ReceiveMovementAckClientRpc(cmd.Tick, _authoritativePosition, rejected, target);

            BroadcastAuthoritativeMovementClientRpc(cmd.Tick, _authoritativePosition);
        }

        [ClientRpc]
        private void ReceiveMovementAckClientRpc(uint tick, Vector3 authoritativePosition, bool rejected, ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            if (_tpc == null)
            {
                return;
            }

            _tpc.HandleServerMovementAck(
                tick,
                authoritativePosition,
                rejected,
                correctionSmoothTime,
                rejectCorrectionSmoothTime,
                correctionDistanceThreshold);
        }

        [ClientRpc]
        private void BroadcastAuthoritativeMovementClientRpc(uint tick, Vector3 authoritativePosition)
        {
            if (IsOwner || !enableRemoteInterpolation)
            {
                return;
            }

            _remoteSnapshots.Add(new RemoteSnapshot
            {
                Tick = tick,
                Position = authoritativePosition,
                ReceivedAt = Time.time
            });

            if (_remoteSnapshots.Count > 64)
            {
                _remoteSnapshots.RemoveAt(0);
            }
        }

        private void ApplyMovementCorrection()
        {
            if (IsOwner || !enableRemoteInterpolation)
            {
                return;
            }

            if (_remoteSnapshots.Count == 0)
            {
                return;
            }

            float renderTime = Time.time - interpolationBackTime;
            while (_remoteSnapshots.Count >= 2 && _remoteSnapshots[1].ReceivedAt <= renderTime)
            {
                _remoteSnapshots.RemoveAt(0);
            }

            if (_remoteSnapshots.Count >= 2)
            {
                RemoteSnapshot from = _remoteSnapshots[0];
                RemoteSnapshot to = _remoteSnapshots[1];
                float t = Mathf.InverseLerp(from.ReceivedAt, to.ReceivedAt, renderTime);
                transform.position = Vector3.Lerp(from.Position, to.Position, t);
                return;
            }

            transform.position = Vector3.Lerp(transform.position, _remoteSnapshots[0].Position, Time.deltaTime * remoteCatchupLerp);
        }
    }
}
