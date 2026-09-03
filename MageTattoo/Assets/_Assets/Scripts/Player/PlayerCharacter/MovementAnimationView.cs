using UnityEngine;
using FishNet.Object;
using FishNet.Component.Animating;

public class MovementAnimationView : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MovementController movementController;
    [SerializeField] private PlayerVisualSelectorView visualSelectorView;
    [SerializeField] private NetworkAnimator networkAnimator;

    [Header("Animation States")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string movementStateName = "Jog";

    [Header("Transition")]
    [SerializeField] private float crossFadeDuration = 0.15f;

    private MovementModel movementModel;

    private int idleStateHash;
    private int movementStateHash;

    private bool isMoving;

    private void Awake()
    {
        idleStateHash = Animator.StringToHash(idleStateName);
        movementStateHash = Animator.StringToHash(movementStateName);
    }

    public override void OnStartNetwork()
    {
        networkAnimator.SetAnimator(visualSelectorView.SelectedAnimator);
    }

    public override void OnStartClient()
    {
        if (!IsOwner)
            return;

        movementModel = movementController.GetMovementModel();

        isMoving = movementModel.Direction.sqrMagnitude > 0f;

        SubscribeToModel();
        UpdateAnimation(true);
    }

    public override void OnStopClient()
    {
        UnsubscribeFromModel();
    }

    private void OnDirectionUpdated(Vector3 direction)
    {
        bool newIsMoving = direction.sqrMagnitude > 0f;

        if (newIsMoving == isMoving)
            return;

        isMoving = newIsMoving;

        UpdateAnimation(false);
    }

    private void UpdateAnimation(bool immediate)
    {
        int stateHash = isMoving
            ? movementStateHash
            : idleStateHash;

        if (immediate)
        {
            networkAnimator.Play(stateHash);
            return;
        }

        networkAnimator.CrossFade(stateHash, crossFadeDuration, 0);
    }

    private void SubscribeToModel()
    {
        movementModel.OnDirectionUpdated -= OnDirectionUpdated;
        movementModel.OnDirectionUpdated += OnDirectionUpdated;
    }

    private void UnsubscribeFromModel()
    {
        if (movementModel == null)
            return;

        movementModel.OnDirectionUpdated -= OnDirectionUpdated;
    }
}