using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.GameplayObjects.Character.PlayerState
{
    public enum LifeState
    {
        Alive,      // 存活
        Dead,       // 死亡
        Reviving    // 复活中（短暂无敌）
    }
    
    public enum ActionState
    {
        Idle,       // 空闲
        Moving,     // 移动
        Shooting,   // 射击
        Controlled  // 受控（硬直/眩晕）
    }
}
