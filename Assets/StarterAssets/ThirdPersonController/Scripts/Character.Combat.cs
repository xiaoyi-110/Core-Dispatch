using System.Collections.Generic;
using Gameplay.GameplayObjects.Items;
using Unity.Netcode;
using UnityEngine;
using Utility;
using NetcodeDiagnostics;

namespace StarterAssets
{
    public partial class Character
    {
        public void Reload()
        {
            if (CurrentWeapon != null && CurrentWeapon.AmmoCount < CurrentWeapon.ClipSize && CurrentAmmo != null && CurrentAmmo.Count > 0)
            {
                if (IsOwner)
                {
                    NetLog.Write($"[Combat] ReloadRequest owner={OwnerClientId} weapon={CurrentWeapon.NetworkId} ammo={CurrentAmmo.NetworkId} weaponAmmo={CurrentWeapon.AmmoCount} reserve={CurrentAmmo.Count}");
                    ReloadServerRpc(CurrentWeapon.NetworkId, CurrentAmmo.NetworkId);
                }
            }
        }

        [ServerRpc]
        public void ReloadServerRpc(string weaponId, string ammoId, ServerRpcParams rpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(rpcParams.Receive.SenderClientId, OwnerClientId, "Reload"))
            {
                NetLog.Write($"[Combat] ReloadRejected sender={rpcParams.Receive.SenderClientId} owner={OwnerClientId} reason=senderMismatch");
                return;
            }
            Item weapon = InventoryOps.FindItemByNetworkId(_items, weaponId);
            Item ammo = InventoryOps.FindItemByNetworkId(_items, ammoId);
            if (weapon == null || ammo == null || weapon is Weapon == false || ammo is Ammo == false)
            {
                InventoryOps.LogWarning($"Reload invalid items. weaponId={weaponId} ammoId={ammoId}");
                NetLog.Write($"[Combat] ReloadRejected sender={rpcParams.Receive.SenderClientId} owner={OwnerClientId} reason=invalidItems weapon={weaponId} ammo={ammoId}");
                return;
            }
            NetLog.Write($"[Combat] ReloadAccepted sender={rpcParams.Receive.SenderClientId} owner={OwnerClientId} weapon={weaponId} ammo={ammoId}");
            ReloadSync(weaponId, ammoId);
            ReloadClientRpc(weaponId, ammoId);
        }

        [ClientRpc]
        public void ReloadClientRpc(string weaponId, string ammoId)
        {
            ReloadSync(weaponId, ammoId);
        }

        private void ReloadSync(string weaponId, string ammoId)
        {
            if (CurrentWeapon != null && CurrentAmmo != null && CurrentAmmo.NetworkId == ammoId && CurrentWeapon.NetworkId == weaponId)
            {
                if (!IsReloading)
                {
                    IsReloading = true;
                    _playerAnimation.TriggerReload();
                    Debug.Log("Reloading...");
                }
                NetLog.Write($"[Combat] ReloadStart owner={OwnerClientId} weapon={weaponId} ammo={ammoId} localOwner={IsOwner}");
            }
        }

        public void ReloadFinished()
        {
            if (!IsOwner)
            {
                return;
            }
            if (CurrentWeapon != null && CurrentAmmo != null)
            {
                ReloadFinishedServerRpc(CurrentWeapon.NetworkId, CurrentAmmo.NetworkId);
            }
        }

        [ServerRpc]
        private void ReloadFinishedServerRpc(string weaponId, string ammoId, ServerRpcParams rpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(rpcParams.Receive.SenderClientId, OwnerClientId, "ReloadFinished"))
            {
                NetLog.Write($"[Combat] ReloadFinishRejected sender={rpcParams.Receive.SenderClientId} owner={OwnerClientId} reason=senderMismatch");
                return;
            }

            Item weaponItem = InventoryOps.FindItemByNetworkId(_items, weaponId);
            Item ammoItem = InventoryOps.FindItemByNetworkId(_items, ammoId);
            Weapon weapon = weaponItem as Weapon;
            Ammo ammo = ammoItem as Ammo;
            if (weapon == null || ammo == null)
            {
                NetLog.Write($"[Combat] ReloadFinishRejected sender={rpcParams.Receive.SenderClientId} owner={OwnerClientId} reason=invalidItems weapon={weaponId} ammo={ammoId}");
                return;
            }

            int transfer = Mathf.Min(weapon.ClipSize - weapon.AmmoCount, ammo.Count);
            transfer = Mathf.Max(0, transfer);
            weapon.AmmoCount += transfer;
            ammo.Count -= transfer;
            weapon.AmmoCount = Mathf.Clamp(weapon.AmmoCount, 0, weapon.ClipSize);
            ammo.Count = Mathf.Max(0, ammo.Count);
            IsReloading = false;

            NetLog.Write($"[Combat] ReloadFinishApplied owner={OwnerClientId} weapon={weaponId} ammo={ammoId} transfer={transfer} weaponAmmo={weapon.AmmoCount} reserve={ammo.Count}");
            ReloadFinishedClientRpc(weaponId, ammoId, weapon.AmmoCount, ammo.Count, transfer);
        }

        [ClientRpc]
        private void ReloadFinishedClientRpc(string weaponId, string ammoId, int weaponAmmoAfter, int ammoReserveAfter, int transfer)
        {
            Item weaponItem = InventoryOps.FindItemByNetworkId(_items, weaponId);
            Item ammoItem = InventoryOps.FindItemByNetworkId(_items, ammoId);
            if (weaponItem is Weapon weapon && ammoItem is Ammo ammo)
            {
                weapon.AmmoCount = Mathf.Clamp(weaponAmmoAfter, 0, weapon.ClipSize);
                ammo.Count = Mathf.Max(0, ammoReserveAfter);
            }

            IsReloading = false;
            _playerAnimation.SetAimLayerWeight(0f);
            NetLog.Write($"[Combat] ReloadFinishSync owner={OwnerClientId} weapon={weaponId} ammo={ammoId} transfer={transfer} weaponAmmo={weaponAmmoAfter} reserve={ammoReserveAfter} localOwner={IsOwner}");
            Debug.Log("Reload Finished.");
        }

        public void Jump()
        {
            _animator.SetTrigger("Jump");
            if (IsOwner)
            {
                string jumpMsg = $"[Combat] JumpLocal owner={OwnerClientId} netId={(NetworkObject != null ? NetworkObject.NetworkObjectId : 0UL)}";
                NetLog.Write(jumpMsg);
                JumpServerRpc();
            }
        }

        [ServerRpc]
        public void JumpServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(rpcParams.Receive.SenderClientId, OwnerClientId, "Jump"))
            {
                string rejectMsg = $"[Combat] JumpRejected sender={rpcParams.Receive.SenderClientId} owner={OwnerClientId}";
                NetLog.Write(rejectMsg);
                return;
            }
            string serverMsg = $"[Combat] JumpAccepted sender={rpcParams.Receive.SenderClientId} owner={OwnerClientId}";
            NetLog.Write(serverMsg);
            _animator.SetTrigger("Jump");
            JumpClientRpc();
        }

        [ClientRpc]
        public void JumpClientRpc()
        {
            if (!IsOwner)
            {
                _animator.SetTrigger("Jump");
                string remoteMsg = $"[Combat] JumpRemoteApplied owner={OwnerClientId} netId={(NetworkObject != null ? NetworkObject.NetworkObjectId : 0UL)}";
                NetLog.Write(remoteMsg);
            }
        }

        private struct PendingShot
        {
            public string WeaponId;
            public Vector3 AimTarget;
            public uint ShotSequence;
            public float EnqueuedAt;
            public int RetryCount;
        }

        [System.Serializable]
        public struct ShootRequest : INetworkSerializable
        {
            public string WeaponId;
            public Vector3 Origin;
            public Vector3 AimTarget;
            public float ClientTime;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref WeaponId);
                serializer.SerializeValue(ref Origin);
                serializer.SerializeValue(ref AimTarget);
                serializer.SerializeValue(ref ClientTime);
            }
        }

        private List<PendingShot> _shots = new List<PendingShot>();
        private readonly List<ShootRequest> _pendingShootRequests = new List<ShootRequest>();
        private readonly Dictionary<string, float> _lastShotTimeByWeapon = new Dictionary<string, float>();
        private uint _serverShotSequence = 0;

        [Header("Shooting")]
        [SerializeField] private float shotBatchInterval = 0.02f;
        [SerializeField] private float headshotMultiplier = 2f;
        [SerializeField] private float bodyshotMultiplier = 1f;
        [SerializeField] private float maxClientShotDrift = 1.2f;
        private float _nextShootSendTime;

        public bool Shoot()
        {
            if (CurrentWeapon != null && IsAiming && !IsReloading && CurrentWeapon.Shoot(this, _aimTarget))
            {
                if (IsOwner)
                {
                    Vector3 origin = CurrentWeapon.Muzzle != null ? CurrentWeapon.Muzzle.position : (transform.position + Vector3.up * 1.5f);
                    ShootRequest request = new ShootRequest
                    {
                        WeaponId = CurrentWeapon.NetworkId,
                        Origin = origin,
                        AimTarget = _aimTarget,
                        ClientTime = GetClientNetworkTime()
                    };
                    _pendingShootRequests.Add(request);
                    if (_pendingShootRequests.Count == 1)
                    {
                        _nextShootSendTime = Time.time + shotBatchInterval;
                    }
                    string queuedMsg = $"[Combat] ShootQueued owner={OwnerClientId} weapon={CurrentWeapon.NetworkId} pending={_pendingShootRequests.Count} clientTime={request.ClientTime:0.000}";
                    NetLog.Write(queuedMsg);
                }
                _rigManager.ApplyWeaponKick(CurrentWeapon.HandKick, CurrentWeapon.BodyKick);
                _playerAnimation.TriggerShoot();
                Debug.Log("Shoot");
                NetworkDiagnostics.RecordLocalShot();
                return true;
            }
            return false;
        }

        [ServerRpc]
        public void ShootServerRpc(ShootRequest[] requests, ServerRpcParams rpcParams = default)
        {
            if (!InventoryOps.ValidateOwnerSender(rpcParams.Receive.SenderClientId, OwnerClientId, "Shoot"))
            {
                string rejectMsg = $"[Combat] ShootBatchRejected sender={rpcParams.Receive.SenderClientId} owner={OwnerClientId}";
                NetLog.Write(rejectMsg);
                return;
            }
            if (requests == null || requests.Length == 0)
            {
                return;
            }

            string recvMsg = $"[Combat] ShootBatchRecv sender={rpcParams.Receive.SenderClientId} owner={OwnerClientId} count={requests.Length}";
            NetLog.Write(recvMsg);
            NetworkDiagnostics.RecordShootBatch(requests.Length);
            for (int i = 0; i < requests.Length; i++)
            {
                ProcessShootRequest(requests[i], rpcParams.Receive.SenderClientId);
            }
        }

        [ClientRpc]
        public void ShootClientRpc(string weaponId, Vector3 aimTarget, uint shotSequence)
        {
            if (!IsOwner)
            {
                PendingShot shot = new PendingShot
                {
                    WeaponId = weaponId,
                    AimTarget = aimTarget,
                    ShotSequence = shotSequence,
                    EnqueuedAt = Time.time,
                    RetryCount = 0
                };
                if (!TryPlayRemoteShotFx(shot, "rpc"))
                {
                    _shots.Add(shot);
                    if (_shots.Count > 16)
                    {
                        _shots.RemoveAt(0);
                    }
                    string queueMsg = $"[Combat] RemoteShotQueued seq={shotSequence} weapon={weaponId} queue={_shots.Count}";
                    NetLog.Write(queueMsg);
                }
            }
        }

        public bool ShootSync(string weaponId, Vector3 aimTarget, bool enableServerDamage)
        {
            if (CurrentWeapon != null && CurrentWeapon.NetworkId == weaponId)
            {
                Debug.Log("Sync Shoot");
                Vector3 previousAim = _aimTarget;
                _aimTarget = aimTarget;
                bool shoot = CurrentWeapon.Shoot(this, _aimTarget, enableServerDamage);
                _aimTarget = previousAim;
                return shoot;
            }
            return false;
        }

        private void ProcessShootRequest(ShootRequest request, ulong senderClientId)
        {
            float serverNow = NetworkManager.Singleton != null ? (float)NetworkManager.Singleton.ServerTime.Time : Time.time;
            float driftLimit = Mathf.Max(maxClientShotDrift, 1.0f);
            float drift = request.ClientTime > 0f ? Mathf.Abs(serverNow - request.ClientTime) : 0f;
            if (drift > driftLimit && request.ClientTime > 0f)
            {
                string driftMsg = $"[Combat] ShootRejected drift sender={senderClientId} weapon={request.WeaponId} clientTime={request.ClientTime:0.000} serverTime={serverNow:0.000} drift={drift:0.000} limit={driftLimit:0.000}";
                NetLog.Write(driftMsg);
                return;
            }

            Item weapon = InventoryOps.FindItemByNetworkId(_items, request.WeaponId);
            if (weapon == null || weapon is Weapon == false)
            {
                InventoryOps.LogWarning($"Shoot invalid weapon. weaponId={request.WeaponId}");
                string invalidMsg = $"[Combat] ShootRejected invalidWeapon sender={senderClientId} weapon={request.WeaponId}";
                NetLog.Write(invalidMsg);
                return;
            }
            Weapon currentWeapon = (Weapon)weapon;

            if (!CanFireWeapon(request.WeaponId, currentWeapon))
            {
                string fireRateMsg = $"[Combat] ShootRejected cooldownOrAmmo sender={senderClientId} weapon={request.WeaponId}";
                NetLog.Write(fireRateMsg);
                return;
            }

            bool lagCompApplied = false;
            bool allowServerProjectileDamage = true;

            if (enableLagCompensation)
            {
                bool withinWindow;
                double shotTime = ResolveShotTimeSeconds(senderClientId, request.ClientTime, out withinWindow);
                NetworkDiagnostics.RecordLagCompWindow(withinWindow);
                Vector3 direction = (request.AimTarget - request.Origin).normalized;
                if (withinWindow && TryLagCompensatedHit(this, request.Origin, direction, shotTime, lagCompensationMaxDistance, out Character hitCharacter, out RaycastHit hitInfo))
                {
                    if (hitCharacter != null)
                    {
                        float multiplier = ComputeHitMultiplier(hitInfo);
                        hitCharacter.TakeDamage(this, hitInfo.transform, currentWeapon.Damage * multiplier);
                        lagCompApplied = true;
                        allowServerProjectileDamage = false;
                        SendShotResultToShooter(true, hitCharacter.ClientID);
                    }
                    else
                    {
                        allowServerProjectileDamage = false;
                        SendShotResultToShooter(false, 0);
                    }
                }
            }

            bool serverShootOk = ShootSync(request.WeaponId, request.AimTarget, allowServerProjectileDamage && !lagCompApplied);
            uint shotSequence = ++_serverShotSequence;
            ShootClientRpc(request.WeaponId, request.AimTarget, shotSequence);
            string shotMsg = $"[Combat] ShootAccepted sender={senderClientId} weapon={request.WeaponId} seq={shotSequence} serverShootOk={serverShootOk} lagComp={lagCompApplied}";
            NetLog.Write(shotMsg);
        }

        private bool CanFireWeapon(string weaponId, Weapon weapon)
        {
            if (weapon == null)
            {
                return false;
            }
            if (weapon.AmmoCount <= 0)
            {
                return false;
            }

            float now = Time.time;
            if (_lastShotTimeByWeapon.TryGetValue(weaponId, out float lastTime))
            {
                if (now - lastTime < Mathf.Max(weapon.FireRate, 0.01f))
                {
                    return false;
                }
            }
            _lastShotTimeByWeapon[weaponId] = now;
            return true;
        }

        private float ComputeHitMultiplier(RaycastHit hitInfo)
        {
            if (hitInfo.collider == null)
            {
                return bodyshotMultiplier;
            }

            string name = hitInfo.collider.name;
            if (!string.IsNullOrEmpty(name) && name.ToLower().Contains("head"))
            {
                return headshotMultiplier;
            }
            if (hitInfo.collider.CompareTag("Head"))
            {
                return headshotMultiplier;
            }
            return bodyshotMultiplier;
        }

        private void SendShotResultToShooter(bool hit, ulong targetClientId)
        {
            ulong[] target = new[] { OwnerClientId };
            ClientRpcParams clientRpcParams = default;
            clientRpcParams.Send.TargetClientIds = target;
            ShotResultClientRpc(hit, targetClientId, clientRpcParams);
        }

        [ClientRpc]
        private void ShotResultClientRpc(bool hit, ulong targetClientId, ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }
            // Hook for UI/FX: hit markers, sounds, etc.
            // Debug.Log($"ShotResult hit={hit} target={targetClientId}");
            NetworkDiagnostics.RecordShotResult(hit);
        }

        private void FlushShootRequests()
        {
            if (!IsOwner)
            {
                return;
            }
            if (_pendingShootRequests.Count == 0)
            {
                return;
            }
            if (Time.time < _nextShootSendTime)
            {
                return;
            }

            ShootServerRpc(_pendingShootRequests.ToArray());
            string sendMsg = $"[Combat] ShootBatchSend owner={OwnerClientId} count={_pendingShootRequests.Count}";
            NetLog.Write(sendMsg);
            _pendingShootRequests.Clear();
        }

        private bool TryPlayRemoteShotFx(PendingShot shot, string source)
        {
            if (CurrentWeapon == null || CurrentWeapon.NetworkId != shot.WeaponId)
            {
                return false;
            }

            bool played = CurrentWeapon.PlayRemoteShotFx(shot.AimTarget);
            if (played)
            {
                _playerAnimation.TriggerShoot();
                string fxMsg = $"[Combat] RemoteShotFxPlayed source={source} seq={shot.ShotSequence} weapon={shot.WeaponId}";
                NetLog.Write(fxMsg);
            }

            return played;
        }
    }
}



