using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.IFSM
{
    public class FSM<T>
    {
        public IState<T> CurrentState;
        private Dictionary<Type, IState<T>> _stateDict = new Dictionary<Type, IState<T>>();
        private Dictionary<string, object> _data = new Dictionary<string, object>();
        public T Owner { get; private set; }

        public FSM(T owner)
        {
            Owner = owner;
        }

        public void AddState(IState<T> state)
        {
            if (state == null) return;
            state.SetStateMachine(this);
            _stateDict[state.GetType()] = state;
        }

        public void ChangeState<S>() where S : IState<T>
        {
            var type = typeof(S);
            if (!_stateDict.TryGetValue(type, out var newState))
            {
                Debug.LogError($"FSM: State {type.Name} not found!");
                return;
            }

            CurrentState?.OnExit(this);
        
            CurrentState = newState;
            CurrentState.OnEnter(this);
        }

        public void Update()
        {
            CurrentState?.OnUpdate(this);
        }

        public void SetData(string key, object value)
        {
            _data[key] = value;
        }

        public TData GetData<TData>(string key)
        {
            if (_data.TryGetValue(key, out object value))
            {
                return (TData)value;
            }
            Debug.LogWarning($"[FSM] No data found for key: {key}");
            return default;
        }
    }
}
