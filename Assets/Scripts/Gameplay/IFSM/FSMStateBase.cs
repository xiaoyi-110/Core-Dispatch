namespace Gameplay.IFSM
{
    public abstract class FSMStateBase<T> : IState<T>
    {
        protected FSM<T> Fsm;   // Current FSM reference
        protected T Owner;      // FSM owner (character/procedure manager)

        public virtual void SetStateMachine(FSM<T> fsm)
        {
            this.Fsm = fsm;
            this.Owner = fsm.Owner;
        }
        public virtual void OnInit(FSM<T> fsm) { }
        public abstract void OnEnter(FSM<T> fsm);
        public abstract void OnUpdate(FSM<T> fsm);
        public abstract void OnExit(FSM<T> fsm);
    }
}
