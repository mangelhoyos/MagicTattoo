using System;
using UnityEngine;

public class MovementModel
{
    public Vector3 Direction { get; private set; }
    public float Velocity { get; }

    public event Action<Vector3> OnDirectionUpdated;

    public MovementModel()
    {
        Velocity = 2.5f;
        Direction = Vector3.zero;
    }

    public void SetInput(Vector2 input)
    {
        Vector3 newDirection = new Vector3(input.x, 0f, input.y);

        if (newDirection.sqrMagnitude > 1f)
        {
            newDirection.Normalize();
        }

        if (Direction == newDirection)
        {
            return;
        }

        Direction = newDirection;

        OnDirectionUpdated?.Invoke(Direction);
    }
}