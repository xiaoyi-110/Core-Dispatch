using Unity.Netcode;
using UnityEngine;

namespace NetcodeDiagnostics
{
    public static class NetworkDiagnostics
    {
        public struct IntervalStats
        {
            public int MoveCorrections;
            public int MoveRejected;
            public float MoveCorrectionAvg;
            public float MoveCorrectionMax;
            public int LocalShots;
            public int ShotHit;
            public int ShotMiss;
            public int LagCompWithin;
            public int LagCompOut;
            public int ShootBatches;
            public int ShootRequests;
            public int ItemSyncRpcs;
            public int ItemSyncItems;
            public int LagCompSamples;
            public double LagCompAvgMs;
            public double LagCompMaxMs;
        }

        public static bool Enabled = true;

        private static int _moveCorrections;
        private static int _moveRejected;
        private static float _moveCorrectionSum;
        private static float _moveCorrectionMax;

        private static int _localShots;
        private static int _shotHit;
        private static int _shotMiss;
        private static int _lagCompWithin;
        private static int _lagCompOut;

        private static int _shootBatches;
        private static int _shootRequests;

        private static int _itemSyncRpcs;
        private static int _itemSyncItems;

        private static int _lagCompSamples;
        private static double _lagCompSumMs;
        private static double _lagCompMaxMs;

        private static int _intervalMoveCorrections;
        private static int _intervalMoveRejected;
        private static float _intervalMoveCorrectionSum;
        private static float _intervalMoveCorrectionMax;

        private static int _intervalLocalShots;
        private static int _intervalShotHit;
        private static int _intervalShotMiss;
        private static int _intervalLagCompWithin;
        private static int _intervalLagCompOut;

        private static int _intervalShootBatches;
        private static int _intervalShootRequests;

        private static int _intervalItemSyncRpcs;
        private static int _intervalItemSyncItems;

        private static int _intervalLagCompSamples;
        private static double _intervalLagCompSumMs;
        private static double _intervalLagCompMaxMs;

        public static void RecordMoveCorrection(float distance, bool rejected)
        {
            if (!Enabled)
            {
                return;
            }
            _moveCorrections++;
            _moveCorrectionSum += distance;
            _moveCorrectionMax = Mathf.Max(_moveCorrectionMax, distance);
            if (rejected)
            {
                _moveRejected++;
            }

            _intervalMoveCorrections++;
            _intervalMoveCorrectionSum += distance;
            _intervalMoveCorrectionMax = Mathf.Max(_intervalMoveCorrectionMax, distance);
            if (rejected)
            {
                _intervalMoveRejected++;
            }
        }

        public static void RecordLocalShot()
        {
            if (!Enabled)
            {
                return;
            }
            _localShots++;
            _intervalLocalShots++;
        }

        public static void RecordShotResult(bool hit)
        {
            if (!Enabled)
            {
                return;
            }
            if (hit)
            {
                _shotHit++;
                _intervalShotHit++;
            }
            else
            {
                _shotMiss++;
                _intervalShotMiss++;
            }
        }

        public static void RecordLagCompWindow(bool withinWindow)
        {
            if (!Enabled)
            {
                return;
            }
            if (withinWindow)
            {
                _lagCompWithin++;
                _intervalLagCompWithin++;
            }
            else
            {
                _lagCompOut++;
                _intervalLagCompOut++;
            }
        }

        public static void RecordShootBatch(int count)
        {
            if (!Enabled)
            {
                return;
            }
            _shootBatches++;
            _shootRequests += count;
            _intervalShootBatches++;
            _intervalShootRequests += count;
        }

        public static void RecordItemSync(int items)
        {
            if (!Enabled)
            {
                return;
            }
            _itemSyncRpcs++;
            _itemSyncItems += items;
            _intervalItemSyncRpcs++;
            _intervalItemSyncItems += items;
        }

        public static void RecordLagCompDuration(double ms)
        {
            if (!Enabled)
            {
                return;
            }
            _lagCompSamples++;
            _lagCompSumMs += ms;
            _lagCompMaxMs = System.Math.Max(_lagCompMaxMs, ms);

            _intervalLagCompSamples++;
            _intervalLagCompSumMs += ms;
            _intervalLagCompMaxMs = System.Math.Max(_intervalLagCompMaxMs, ms);
        }

        public static IntervalStats ConsumeIntervalStats()
        {
            IntervalStats stats = new IntervalStats
            {
                MoveCorrections = _intervalMoveCorrections,
                MoveRejected = _intervalMoveRejected,
                MoveCorrectionAvg = _intervalMoveCorrections > 0 ? _intervalMoveCorrectionSum / _intervalMoveCorrections : 0f,
                MoveCorrectionMax = _intervalMoveCorrectionMax,
                LocalShots = _intervalLocalShots,
                ShotHit = _intervalShotHit,
                ShotMiss = _intervalShotMiss,
                LagCompWithin = _intervalLagCompWithin,
                LagCompOut = _intervalLagCompOut,
                ShootBatches = _intervalShootBatches,
                ShootRequests = _intervalShootRequests,
                ItemSyncRpcs = _intervalItemSyncRpcs,
                ItemSyncItems = _intervalItemSyncItems,
                LagCompSamples = _intervalLagCompSamples,
                LagCompAvgMs = _intervalLagCompSamples > 0 ? _intervalLagCompSumMs / _intervalLagCompSamples : 0d,
                LagCompMaxMs = _intervalLagCompMaxMs
            };

            _intervalMoveCorrections = 0;
            _intervalMoveRejected = 0;
            _intervalMoveCorrectionSum = 0f;
            _intervalMoveCorrectionMax = 0f;
            _intervalLocalShots = 0;
            _intervalShotHit = 0;
            _intervalShotMiss = 0;
            _intervalLagCompWithin = 0;
            _intervalLagCompOut = 0;
            _intervalShootBatches = 0;
            _intervalShootRequests = 0;
            _intervalItemSyncRpcs = 0;
            _intervalItemSyncItems = 0;
            _intervalLagCompSamples = 0;
            _intervalLagCompSumMs = 0d;
            _intervalLagCompMaxMs = 0d;

            return stats;
        }

        public static string BuildSummary(IntervalStats stats, float intervalSeconds)
        {
            float rttMs = 0f;
            TryGetRttMs(out rttMs);

            return $"[NetDiag {intervalSeconds:0}s] RTT={rttMs:0}ms " +
                   $"MoveCorrections={stats.MoveCorrections} (avg {stats.MoveCorrectionAvg:0.00}m, max {stats.MoveCorrectionMax:0.00}m, rejected {stats.MoveRejected}) " +
                   $"Shots={stats.LocalShots} Hit={stats.ShotHit} Miss={stats.ShotMiss} " +
                   $"LagCompIn={stats.LagCompWithin} Out={stats.LagCompOut} " +
                   $"ShootRPC batches={stats.ShootBatches} req={stats.ShootRequests} " +
                   $"ItemSync RPCs={stats.ItemSyncRpcs} items={stats.ItemSyncItems} " +
                   $"LagCompCost avg={stats.LagCompAvgMs:0.00}ms max={stats.LagCompMaxMs:0.00}ms";
        }

        public static bool TryGetRttMs(out float rttMs)
        {
            rttMs = 0f;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return false;
            }
            if (NetworkManager.Singleton.NetworkConfig == null || NetworkManager.Singleton.NetworkConfig.NetworkTransport == null)
            {
                return false;
            }
            try
            {
                rttMs = (float)NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
                return true;
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
        }
    }

    public class NetworkDiagnosticsHUD : MonoBehaviour
    {
        [SerializeField] private float logInterval = 10f;
        [SerializeField] private bool showHud = true;
        private float _nextLogTime;
        private NetworkDiagnostics.IntervalStats _lastIntervalStats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameObject existing = GameObject.Find("__NetDiagnostics");
            if (existing != null)
            {
                return;
            }
            GameObject go = new GameObject("__NetDiagnostics");
            DontDestroyOnLoad(go);
            go.AddComponent<NetworkDiagnosticsHUD>();
            go.AddComponent<NetworkManagerSafety>();
        }

        private void Update()
        {
            if (!NetworkDiagnostics.Enabled)
            {
                return;
            }
            if (Time.time < _nextLogTime)
            {
                return;
            }
            _nextLogTime = Time.time + logInterval;
            _lastIntervalStats = NetworkDiagnostics.ConsumeIntervalStats();
            Debug.Log(NetworkDiagnostics.BuildSummary(_lastIntervalStats, logInterval));
        }

        private void OnGUI()
        {
            if (!showHud || !NetworkDiagnostics.Enabled)
            {
                return;
            }
            float rttMs = 0f;
            NetworkDiagnostics.TryGetRttMs(out rttMs);

            string text =
                $"RTT: {rttMs:0} ms\n" +
                $"Move: corr {_lastIntervalStats.MoveCorrections} avg {_lastIntervalStats.MoveCorrectionAvg:0.00}m max {_lastIntervalStats.MoveCorrectionMax:0.00}m rej {_lastIntervalStats.MoveRejected}\n" +
                $"Shots: local {_lastIntervalStats.LocalShots} hit {_lastIntervalStats.ShotHit} miss {_lastIntervalStats.ShotMiss}\n" +
                $"LagComp: in {_lastIntervalStats.LagCompWithin} out {_lastIntervalStats.LagCompOut}\n" +
                $"ShootRPC: batches {_lastIntervalStats.ShootBatches} req {_lastIntervalStats.ShootRequests}\n" +
                $"ItemSync: rpcs {_lastIntervalStats.ItemSyncRpcs} items {_lastIntervalStats.ItemSyncItems}\n" +
                $"LagCompCost: avg {_lastIntervalStats.LagCompAvgMs:0.00}ms max {_lastIntervalStats.LagCompMaxMs:0.00}ms";

            GUI.Box(new Rect(10, 10, 430, 140), "Network Diagnostics");
            GUI.Label(new Rect(20, 35, 410, 120), text);
        }
    }
}
