using UnityEngine;
using FishNet.Object;

public class MovementView : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private MovementController movementController;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform visualTransform;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Gravity")]
    [SerializeField] private float gravityMultiplier = 1f;
    [SerializeField] private float groundedVerticalVelocity = -2f;

    private MovementModel movementModel;

    private Vector3 currentDirection;
    private float movementSpeed;
    private float verticalVelocity;

    public override void OnStartClient()
    {
        if (!base.IsOwner)
        {
            movementController.enabled = false;
            return;
        }

        movementController.enabled = true;

        movementModel = movementController.GetMovementModel();

        currentDirection = movementModel.Direction;
        movementSpeed = movementModel.Velocity;

        SubscribeToModel();
    }

    public override void OnStopClient()
    {
        UnsubscribeFromModel();
    }

    private void Update()
    {
        if (!base.IsOwner || movementModel == null)
            return;

        UpdateGravity();
        Move();
        Rotate();
    }

    private void OnDirectionUpdated(Vector3 direction)
    {
        currentDirection = direction;
    }

    private void Move()
    {
        Vector3 velocity = currentDirection * movementSpeed;
        velocity.y = verticalVelocity;

        characterController.Move(velocity * Time.deltaTime);
    }

    private void Rotate()
    {
        if (currentDirection.sqrMagnitude <= 0f)
            return;

        Vector3 horizontalDirection = currentDirection;
        horizontalDirection.y = 0f;

        if (horizontalDirection.sqrMagnitude <= 0f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection);

        visualTransform.rotation = Quaternion.Slerp(visualTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime
        );
    }

    private void UpdateGravity()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedVerticalVelocity;
            return;
        }

        verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
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