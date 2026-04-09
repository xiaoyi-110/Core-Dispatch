using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Utility;

namespace StarterAssets
{
    public partial class Character
    {
        private struct RemoteSnapshot
        {
            public uint Tick;
            public Vector3 Position;
            public Quaternion Rotation;
            public float ReceivedAt;
        }

        [Header("Server Move Validation")]
        [SerializeField] private float serverWalkSpeed = 2.0f;
        [SerializeField] private float serverRunSpeed = 4.0f;
        [SerializeField] private float serverSprintSpeed = 5.335f;
        [SerializeField] private float serverDistanceTolerance = 0.15f;
        [SerializeField] private float serverMaxAcceleration = 200f;
        [SerializeField] private float serverMaxTurnRate = 720f;
        [SerializeField] private float serverMaxClientTimeDrift = 0.5f;

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
        private Quaternion _authoritativeRotation = Quaternion.identity;
        private Vector3 _lastAuthoritativeVelocity = Vector3.zero;
        private float _lastAuthoritativeTime;
        private int _consecutiveRejects;
        private readonly List<RemoteSnapshot> _remoteSnapshots = new List<RemoteSnapshot>();
        private float _remoteArrivalJitter;
        private float _nextMoveRejectLogTime;
        private float _nextMoveAckLogTime;
        private float _serverVerticalVelocity;
        private float _serverJumpTimeoutDelta;
        private float _serverLastCmdTime;
        private float _serverSpeed;

        [ServerRpc]
        public void SubmitMovementCommandServerRpc(PlayerMovementCommand cmd, ServerRpcParams rpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(rpcParams.Receive.SenderClientId, OwnerClientId, "Move")) return;
            if (cmd.Tick <= _lastMoveCmdTick)
            {
                return;
            }
            _lastMoveCmdTick = cmd.Tick;

            if (!_hasAuthoritativeMoveState)
            {
                _authoritativePosition = transform.position;
                _authoritativeRotation = transform.rotation;
                _hasAuthoritativeMoveState = true;
                _lastAuthoritativeTime = Time.time;
                _serverVerticalVelocity = 0f;
                _serverJumpTimeoutDelta = _tpc != null ? _tpc.JumpTimeout : 0.5f;
                _serverLastCmdTime = Time.time;
            }

            if (cmd.ClientTime > 0f)
            {
                float serverNow = NetworkManager.Singleton != null ? (float)NetworkManager.Singleton.ServerTime.Time : Time.time;
                float drift = Mathf.Abs(serverNow - cmd.ClientTime);
                if (drift > serverMaxClientTimeDrift && Time.time >= _nextMoveRejectLogTime)
                {
                    _nextMoveRejectLogTime = Time.time + 1f;
                    string msg = $"[Move] Client time drift={drift:0.00}s (max {serverMaxClientTimeDrift:0.00}s) tick={cmd.Tick} clientTime={cmd.ClientTime:0.000} serverTime={serverNow:0.000} clientId={rpcParams.Receive.SenderClientId}";
                    Debug.LogWarning(msg);
                    NetLog.Write(msg);
                }
            }

            float serverDelta = cmd.DeltaTime > 0f ? Mathf.Clamp(cmd.DeltaTime, 0.001f, 0.05f) : 0.016f;
            SimulateServerMove(cmd, serverDelta);
            bool rejected = false;

            if (Time.time >= _nextMoveAckLogTime)
            {
                _nextMoveAckLogTime = Time.time + 0.25f;
                float serverNow = NetworkManager.Singleton != null ? (float)NetworkManager.Singleton.ServerTime.Time : Time.time;
                float drift = cmd.ClientTime > 0f ? Mathf.Abs(serverNow - cmd.ClientTime) : 0f;
                bool controllerEnabled = _controller != null && _controller.enabled;
                string ackMsg = $"[Move] AckSent tick={cmd.Tick} clientTime={cmd.ClientTime:0.000} serverTime={serverNow:0.000} drift={drift:0.000} jumpPressed={cmd.JumpPressed} input=({cmd.MoveInput.x:0.000},{cmd.MoveInput.y:0.000}) facingYaw={cmd.FacingYaw:0.0} moveYaw={cmd.MoveYaw:0.0} run={cmd.IsRunning} sprint={cmd.IsSprinting} analog={cmd.AnalogMovement} ctrlNull={(_controller == null)} ctrlEnabled={controllerEnabled} pos=({_authoritativePosition.x:0.000},{_authoritativePosition.y:0.000},{_authoritativePosition.z:0.000}) speed={_serverSpeed:0.000} jumpTimeout={_serverJumpTimeoutDelta:0.000}";
                NetLog.Write(ackMsg);
            }

            ulong sender = rpcParams.Receive.SenderClientId;
            ClientRpcParams target = default;
            target.Send.TargetClientIds = new[] { sender };
            ReceiveMovementAckClientRpc(cmd.Tick, _authoritativePosition, _authoritativeRotation, _serverVerticalVelocity, _serverSpeed, _serverJumpTimeoutDelta, rejected, target);

            BroadcastAuthoritativeMovementClientRpc(cmd.Tick, _authoritativePosition, _authoritativeRotation);
        }

        [ClientRpc]
        private void ReceiveMovementAckClientRpc(uint tick, Vector3 authoritativePosition, Quaternion authoritativeRotation, float authoritativeVerticalVelocity, float authoritativeSpeed, float authoritativeJumpTimeoutDelta, bool rejected, ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            if (_tpc == null)
            {
                return;
            }

            string recvMsg = $"[Move] AckRecv tick={tick} ownerClientId={OwnerClientId} netId={(NetworkObject != null ? NetworkObject.NetworkObjectId : 0UL)} pos=({transform.position.x:0.000},{transform.position.y:0.000},{transform.position.z:0.000}) authPos=({authoritativePosition.x:0.000},{authoritativePosition.y:0.000},{authoritativePosition.z:0.000})";
            NetLog.Write(recvMsg);

            _tpc.HandleServerMovementAck(
                tick,
                authoritativePosition,
                authoritativeRotation,
                authoritativeVerticalVelocity,
                authoritativeSpeed,
                authoritativeJumpTimeoutDelta,
                rejected,
                correctionSmoothTime,
                rejectCorrectionSmoothTime,
                correctionDistanceThreshold);
        }

        [ClientRpc]
        private void BroadcastAuthoritativeMovementClientRpc(uint tick, Vector3 authoritativePosition, Quaternion authoritativeRotation)
        {
            if (IsOwner || !enableRemoteInterpolation)
            {
                return;
            }

            float now = Time.time;
            if (_remoteSnapshots.Count > 0)
            {
                float arrivalDelta = now - _remoteSnapshots[_remoteSnapshots.Count - 1].ReceivedAt;
                float jitter = Mathf.Abs(arrivalDelta - interpolationBackTime);
                _remoteArrivalJitter = Mathf.Lerp(_remoteArrivalJitter, jitter, 0.1f);
            }

            _remoteSnapshots.Add(new RemoteSnapshot
            {
                Tick = tick,
                Position = authoritativePosition,
                Rotation = authoritativeRotation,
                ReceivedAt = now
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
            float dynamicBackTime = interpolationBackTime + _remoteArrivalJitter * 0.5f;
            renderTime = Time.time - dynamicBackTime;
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
                transform.rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t);
                return;
            }

            transform.position = Vector3.Lerp(transform.position, _remoteSnapshots[0].Position, Time.deltaTime * remoteCatchupLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, _remoteSnapshots[0].Rotation, Time.deltaTime * remoteCatchupLerp);
        }

        private void SimulateServerMove(PlayerMovementCommand cmd, float deltaTime)
        {
            if (_controller == null)
            {
                _authoritativePosition = transform.position;
                _authoritativeRotation = transform.rotation;
                return;
            }

            float walkSpeed = _tpc != null ? _tpc.WalkSpeed : serverWalkSpeed;
            float runSpeed = _tpc != null ? _tpc.RunSpeed : serverRunSpeed;
            float sprintSpeed = _tpc != null ? _tpc.SprintSpeed : serverSprintSpeed;
            float gravity = _tpc != null ? _tpc.Gravity : -15.0f;
            float jumpHeight = _tpc != null ? _tpc.JumpHeight : 1.2f;
            float jumpTimeout = _tpc != null ? _tpc.JumpTimeout : 0.5f;
            float speedChangeRate = _tpc != null ? _tpc.SpeedChangeRate : 10.0f;
            float terminalVelocity = 53.0f;

            MovementSimulation.SimulateCharacterControllerMove(
                _controller,
                transform,
                cmd.MoveInput,
                cmd.IsSprinting,
                cmd.IsRunning,
                cmd.JumpPressed,
                cmd.AnalogMovement,
                deltaTime,
                walkSpeed,
                runSpeed,
                sprintSpeed,
                speedChangeRate,
                gravity,
                jumpHeight,
                jumpTimeout,
                GroundedOffset,
                GroundedRadius,
                GroundLayers,
                terminalVelocity,
                ref _serverSpeed,
                ref _serverVerticalVelocity,
                ref _serverJumpTimeoutDelta,
                cmd.FacingYaw,
                cmd.MoveYaw);

            _authoritativePosition = transform.position;
            _authoritativeRotation = transform.rotation;
            Vector3 planarVelocity = Quaternion.Euler(0f, cmd.MoveYaw, 0f) * Vector3.forward;
            _lastAuthoritativeVelocity = (cmd.MoveInput != Vector2.zero ? planarVelocity.normalized : Vector3.zero) * _serverSpeed;
            _lastAuthoritativeTime = Time.time;
        }
    }
}
