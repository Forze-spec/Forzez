using UnityEngine;

public class PlayerAnimatorBehaviour : StateMachineBehaviour {
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        GameManager.Instance.player.FightController.AttackEnds();
    }
}
