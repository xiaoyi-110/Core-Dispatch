using Unity.Netcode.Components;
using UnityEngine;

namespace Netcode
{
    public class ClientNetworkTransform : NetworkTransform
    {
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // Character movement/rotation replication is handled by custom prediction + server ack
            // + BroadcastAuthoritativeMovementClientRpc interpolation. Disable NetworkTransform to
            // avoid a second replication path fighting over the same transform on any role.
            SyncPositionX = false;
            SyncPositionY = false;
            SyncPositionZ = false;
            SyncRotAngleX = false;
            SyncRotAngleY = false;
            SyncRotAngleZ = false;
            SyncScaleX = false;
            SyncScaleY = false;
            SyncScaleZ = false;
            enabled = false;
        }

        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}

