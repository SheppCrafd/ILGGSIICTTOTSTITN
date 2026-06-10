using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Player player;

    public Vector3 offset =
        new Vector3(-20f, 20f, -20f);

    void LateUpdate()
    {
        if (player == null)
            return;

        transform.position =
            player.transform.position + offset;

        transform.LookAt(player.transform);
    }
}
