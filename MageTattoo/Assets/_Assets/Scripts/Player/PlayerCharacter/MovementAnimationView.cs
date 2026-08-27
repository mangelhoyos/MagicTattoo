using UnityEngine;

public class MovementAnimationView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MovementController movementController;
    [SerializeField] private PlayerVisualSelectorView visualSelectorView;
    private Animator animator;

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

    private void Start()
    {
        animator = visualSelectorView.SelectedAnimator;

        movementModel = movementController.GetMovementModel();

        isMoving = movementModel.Direction.sqrMagnitude > 0f;

        SubscribeToModel();
        UpdateAnimation(true);
    }

    private void OnEnable()
    {
        if (movementModel == null)
            return;

        SubscribeToModel();
    }

    private void OnDisable()
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
            animator.Play(stateHash);
            return;
        }

        animator.CrossFade(stateHash, crossFadeDuration);
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