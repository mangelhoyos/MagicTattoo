using UnityEngine;
using UnityEngine.InputSystem;

public class MovementController : MonoBehaviour
{
    [SerializeField] private InputActionReference movementAction;

    public MovementModel Model { get; private set; }

    private void Awake()
    {
        Model = new MovementModel();
    }

    private void OnEnable()
    {
        movementAction.action.performed += OnMovementInput;
        movementAction.action.canceled += OnMovementInput;

        movementAction.action.Enable();
    }

    private void OnDisable()
    {
        movementAction.action.performed -= OnMovementInput;
        movementAction.action.canceled -= OnMovementInput;

        movementAction.action.Disable();
    }

    private void OnMovementInput(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();

        Model.SetInput(input);
    }

    public MovementModel GetMovementModel() => Model;
}