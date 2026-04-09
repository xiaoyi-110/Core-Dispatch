using System;
using System.Collections.Generic;
using Managers;
using NetcodeDiagnostics;
using Unity.Netcode;
using UnityEngine;

namespace StarterAssets
{
    public partial class Character
    {
        [Header("Lag Compensation")]
        [SerializeField] private bool enableLagCompensation = true;
        [SerializeField] private float lagCompensationWindow = 0.2f;
        [SerializeField] private float lagCompensationSnapshotInterval = 0.05f;
        [SerializeField] private float lagCompensationMaxDistance = 120f;
        [SerializeField] private float lagCompensationMinWindow = 0.1f;
        [SerializeField] private float lagCompensationMaxWindow = 0.35f;
        [SerializeField] private float lagCompensationRttWeight = 0.6f;

        private struct LagCompSnapshot
        {
            public double Time;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Height;
            public float Radius;
            public Vector3 Center;
        }

        private struct RewindState
        {
            public Character Character;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Height;
            public float Radius;
            public Vector3 Center;
        }

        private readonly List<LagCompSnapshot> _lagCompSnapshots = new List<LagCompSnapshot>();
        private double _nextLagCompSnapshotTime;
        private float _smoothedRtt;

        private void RecordLagCompSnapshot()
        {
            if (!enableLagCompensation)
            {
                return;
            }
            if (_controller == null)
            {
                return;
            }

            double now = Time.timeAsDouble;
            if (now < _nextLagCompSnapshotTime)
            {
                return;
            }
            _nextLagCompSnapshotTime = now + lagCompensationSnapshotInterval;

            UpdateLagCompensationWindow();

            _lagCompSnapshots.Add(new LagCompSnapshot
            {
                Time = now,
                Position = transform.position,
                Rotation = transform.rotation,
                Height = _controller.height,
                Radius = _controller.radius,
                Center = _controller.center
            });

            double cutoff = now - lagCompensationWindow;
            while (_lagCompSnapshots.Count > 0 && _lagCompSnapshots[0].Time < cutoff)
            {
                _lagCompSnapshots.RemoveAt(0);
            }
        }

        private bool TryGetLagCompSnapshot(double targetTime, out LagCompSnapshot snapshot)
        {
            snapshot = default;
            if (_lagCompSnapshots.Count == 0)
            {
                return false;
            }

            if (targetTime <= _lagCompSnapshots[0].Time)
            {
                snapshot = _lagCompSnapshots[0];
                return true;
            }

            int lastIndex = _lagCompSnapshots.Count - 1;
            if (targetTime >= _lagCompSnapshots[lastIndex].Time)
            {
                snapshot = _lagCompSnapshots[lastIndex];
                return true;
            }

            for (int i = 0; i < lastIndex; i++)
            {
                LagCompSnapshot from = _lagCompSnapshots[i];
                LagCompSnapshot to = _lagCompSnapshots[i + 1];
                if (targetTime >= from.Time && targetTime <= to.Time)
                {
                    double range = to.Time - from.Time;
                    float t = range <= 0.0001 ? 0f : (float)((targetTime - from.Time) / range);
                    snapshot = new LagCompSnapshot
                    {
                        Time = targetTime,
                        Position = Vector3.Lerp(from.Position, to.Position, t),
                        Rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t),
                        Height = from.Height,
                        Radius = from.Radius,
                        Center = from.Center
                    };
                    return true;
                }
            }

            snapshot = _lagCompSnapshots[lastIndex];
            return true;
        }

        private bool TryLagCompensatedHit(Character shooter, Vector3 origin, Vector3 direction, double shotTime, float maxDistance, out Character hitCharacter, out RaycastHit hitInfo)
        {
            hitCharacter = null;
            hitInfo = default;

            if (!enableLagCompensation || !NetworkManager.Singleton.IsServer)
            {
                return false;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            double startTime = Time.realtimeSinceStartupAsDouble;
            List<RewindState> rewound = new List<RewindState>();
            foreach (Character character in WorldRegistry.Characters)
            {
                if (character == null || character == shooter || character.Health <= 0)
                {
                    continue;
                }

                if (character.TryGetLagCompSnapshot(shotTime, out LagCompSnapshot snapshot))
                {
                    RewindState state = new RewindState
                    {
                        Character = character,
                        Position = character.transform.position,
                        Rotation = character.transform.rotation,
                        Height = character._controller != null ? character._controller.height : 0f,
                        Radius = character._controller != null ? character._controller.radius : 0f,
                        Center = character._controller != null ? character._controller.center : Vector3.zero
                    };
                    rewound.Add(state);

                    character.transform.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
                    if (character._controller != null)
                    {
                        character._controller.height = snapshot.Height;
                        character._controller.radius = snapshot.Radius;
                        character._controller.center = snapshot.Center;
                    }
                }
            }

            bool hit = Physics.Raycast(origin, direction.normalized, out hitInfo, maxDistance);
            if (hit)
            {
                hitCharacter = hitInfo.transform.root.GetComponent<Character>();
            }

            for (int i = 0; i < rewound.Count; i++)
            {
                RewindState state = rewound[i];
                if (state.Character == null)
                {
                    continue;
                }
                state.Character.transform.SetPositionAndRotation(state.Position, state.Rotation);
                if (state.Character._controller != null)
                {
                    state.Character._controller.height = state.Height;
                    state.Character._controller.radius = state.Radius;
                    state.Character._controller.center = state.Center;
                }
            }

            double durationMs = (Time.realtimeSinceStartupAsDouble - startTime) * 1000.0;
            NetworkDiagnostics.RecordLagCompDuration(durationMs);
            return hit;
        }

        private float GetClientNetworkTime()
        {
            if (NetworkManager.Singleton == null)
            {
                return Time.time;
            }
            return (float)NetworkManager.Singleton.LocalTime.Time;
        }

        private double ResolveShotTimeSeconds(ulong senderClientId, float clientTime, out bool withinWindow)
        {
            double serverNow = NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : Time.timeAsDouble;
            double estimated = serverNow;
            withinWindow = true;

            if (NetworkManager.Singleton != null)
            {
                double rttSec = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(senderClientId) / 1000.0;
                if (rttSec > 0)
                {
                    estimated = serverNow - rttSec * 0.5;
                }
            }

            if (clientTime > 0)
            {
                double delta = Math.Abs(serverNow - clientTime);
                if (delta <= lagCompensationWindow * 2)
                {
                    estimated = clientTime;
                }
            }

            double minTime = serverNow - lagCompensationWindow;
            if (estimated < minTime)
            {
                estimated = minTime;
                withinWindow = false;
            }

            if (serverNow - estimated > lagCompensationWindow)
            {
                withinWindow = false;
            }

            return estimated;
        }

        private void UpdateLagCompensationWindow()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }
            if (!NetworkManager.Singleton.IsListening || NetworkManager.Singleton.NetworkConfig == null || NetworkManager.Singleton.NetworkConfig.NetworkTransport == null)
            {
                return;
            }
            float rttMs = 0f;
            try
            {
                rttMs = (float)NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            }
            catch (System.ObjectDisposedException)
            {
                return;
            }
            if (rttMs > 0)
            {
                float rttSec = rttMs / 1000f;
                _smoothedRtt = Mathf.Lerp(_smoothedRtt, rttSec, 0.1f);
                float target = Mathf.Clamp(_smoothedRtt * lagCompensationRttWeight + lagCompensationMinWindow, lagCompensationMinWindow, lagCompensationMaxWindow);
                lagCompensationWindow = Mathf.Max(lagCompensationWindow, target);
            }
        }
    }
}

