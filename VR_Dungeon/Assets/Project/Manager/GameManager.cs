using UnityEngine;

namespace Project.Manager
{
    public class GameManager : SingletonBehavior<GameManager>
    {
        [SerializeField] private State _state;
        public State State => _state;

        // State Setting
        public void SetState(State targetState)
        {
            _state = targetState;
        }

        private void Start()
        {
            _state = State.Initializing;

            EventManager.On("game_started", obj => SetState(State.Playing));
            EventManager.On("game_ended", obj => SetState(State.GameEnded));
            EventManager.On("game_paused", obj => SetState(State.Paused));
            EventManager.On("game_resumed", obj => SetState(State.Playing));
        }
    } 
}
