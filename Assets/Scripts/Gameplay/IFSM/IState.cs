namespace Gameplay.IFSM
{
    public interface IState<T>
    {
        void OnEnter(FSM<T> fsm);
        void OnUpdate(FSM<T> fsm);
        void OnExit(FSM<T> fsm);
        void SetStateMachine(FSM<T> fsm);
    }
}
