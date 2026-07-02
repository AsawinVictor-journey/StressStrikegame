using UnityEngine;

/// <summary>
/// Keyboard implementation of HandInputProvider.
/// Replace this with Bno055HandInput when the IMU is connected.
/// </summary>
public class KeyboardHandInput : HandInputProvider
{
    public enum Side { Left, Right }
    public Side side;

    public override Vector3 GetMoveDirection()
    {
        Vector3 dir = Vector3.zero;

        if (side == Side.Left)
        {
            if (Input.GetKey(KeyCode.W)) dir += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) dir -= Vector3.forward;
            if (Input.GetKey(KeyCode.D)) dir += Vector3.right;
            if (Input.GetKey(KeyCode.A)) dir -= Vector3.right;
            if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) dir -= Vector3.up;
        }
        else
        {
            if (Input.GetKey(KeyCode.UpArrow))    dir += Vector3.forward;
            if (Input.GetKey(KeyCode.DownArrow))  dir -= Vector3.forward;
            if (Input.GetKey(KeyCode.RightArrow)) dir += Vector3.right;
            if (Input.GetKey(KeyCode.LeftArrow))  dir -= Vector3.right;
            if (Input.GetKey(KeyCode.Space))       dir += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) dir -= Vector3.up;
        }

        return dir.normalized;
    }
}
