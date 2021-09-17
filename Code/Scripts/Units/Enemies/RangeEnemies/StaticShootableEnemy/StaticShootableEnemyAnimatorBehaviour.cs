using UnityEngine;

public class StaticShootableEnemyAnimatorBehaviour : StateMachineBehaviour {
    public NotifyEvent AttackEnded;
    
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        AttackEnded?.Invoke();
    }
}