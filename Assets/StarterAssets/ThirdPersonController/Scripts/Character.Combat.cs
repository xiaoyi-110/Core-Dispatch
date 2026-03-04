using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

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
                    ReloadServerRpc(CurrentWeapon.NetworkId, CurrentAmmo.NetworkId);
                }
                IsReloading = true;
                _playerAnimation.TriggerReload();
                Debug.Log("Reloading...");
            }
        }

        [ServerRpc]
        public void ReloadServerRpc(string weaponId, string ammoId)
        {
            ReloadSync(weaponId, ammoId);
            ReloadClientRpc(weaponId, ammoId);
        }

        [ClientRpc]
        public void ReloadClientRpc(string weaponId, string ammoId)
        {
            if (!IsOwner)
            {
                ReloadSync(weaponId, ammoId);
            }
        }

        private void ReloadSync(string weaponId, string ammoId)
        {
            if (CurrentWeapon != null && CurrentAmmo != null && CurrentAmmo.NetworkId == ammoId && CurrentWeapon.NetworkId == weaponId)
            {
                Reload();
            }
        }

        public void ReloadFinished()
        {
            if (CurrentWeapon != null && CurrentWeapon.AmmoCount < CurrentWeapon.ClipSize && CurrentAmmo != null && CurrentAmmo.Count > 0)
            {
                int count = Mathf.Min(CurrentWeapon.ClipSize - CurrentWeapon.AmmoCount, CurrentAmmo.Count);
                CurrentAmmo.Count -= count;
                CurrentWeapon.AmmoCount += count;
                IsReloading = false;
                _playerAnimation.SetAimLayerWeight(0f);
                Debug.Log("Reload Finished.");
            }
        }

        public void Jump()
        {
            _animator.SetTrigger("Jump");
            JumpServerRpc();
        }

        [ServerRpc]
        public void JumpServerRpc()
        {
            _animator.SetTrigger("Jump");
            JumpClientRpc();
        }

        [ClientRpc]
        public void JumpClientRpc()
        {
            if (!IsOwner)
            {
                _animator.SetTrigger("Jump");
            }
        }

        private List<string> _shots = new List<string>();

        public bool Shoot()
        {
            if (CurrentWeapon != null && IsAiming && !IsReloading && CurrentWeapon.Shoot(this, _aimTarget))
            {
                if (IsOwner)
                {
                    ShootServerRpc(CurrentWeapon.NetworkId);
                }
                _rigManager.ApplyWeaponKick(CurrentWeapon.HandKick, CurrentWeapon.BodyKick);
                _playerAnimation.TriggerShoot();
                Debug.Log("Shoot");
                return true;
            }
            return false;
        }

        [ServerRpc]
        public void ShootServerRpc(string weaponId)
        {
            ShootSync(weaponId);
            ShootClientRpc(weaponId);
        }

        [ClientRpc]
        public void ShootClientRpc(string weaponId)
        {
            if (!IsOwner)
            {
                ShootSync(weaponId);
            }
        }

        public void ShootSync(string weaponId)
        {
            if (CurrentWeapon != null && CurrentWeapon.NetworkId == weaponId)
            {
                Debug.Log("Sync Shoot");
                bool shoot = Shoot();
                if (!shoot)
                {
                    _shots.Add(weaponId);
                }
            }
        }
    }
}
