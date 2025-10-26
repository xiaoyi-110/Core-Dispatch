using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.GameplayObjects.Items
{
    public class Ammo : Item
    {
        private int _count = 0;public int Count { get => _count; set => _count = value; }
    }
}
    
