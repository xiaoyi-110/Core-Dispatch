using Gameplay.GameplayObjects.Character.PlayerState;
using UnityEngine;

namespace Gameplay.GameplayObjects.Character
{
    public interface IPlayerControllable
    {
        // 动作/移动能力
        void SetServerMoveInput(Vector3 direction);
        void StopMove();
        
        // 状态控制能力 (用于状态转换)
        void RequestLifeStateChange(LifeState newState);
        
        // 属性访问 (供状态类读取)
        float Speed { get; }
        Vector3 CurrentAuthoritativeDirection { get; }
        
        // ... 其他能力，如 PlayAnimation, PlaySound
    }
}