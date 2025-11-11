using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gameplay.GameplayObjects.Items;
using StarterAssets;
public class Consumable : Item
{

    [Header("Consumable")]
    [SerializeField] private Action _action = Action.Heal; public Action action { get { return _action; } }
    [SerializeField] private float _actionValue = 1; public float actionValue { get { return _actionValue; } }

    public enum Action
    {
        Heal
    }

    public void Consume(Character character)
    {
        if (Count > 0 && character != null)
        {

        }
    }

}