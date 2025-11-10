using Unity.Netcode.Components;
using UnityEngine;

namespace Netcode
{
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}

