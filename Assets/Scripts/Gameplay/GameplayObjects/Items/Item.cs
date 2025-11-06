using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.GameplayObjects.Items
{
    public class Item : MonoBehaviour
    {
        [SerializeField] private string id = "";public string Id { get { return id; } }
        public int Count { get; set; } = 0;
    }
}

