using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Player player;

    public Vector3 offset =
        new Vector3(-20f, 20f, -20f);

    bool loggedMissingPlayer;

    void LateUpdate()
    {
        if (player == null)
        {
            if (!loggedMissingPlayer)
            {
                Debug.LogWarning("[CameraFollow] Player reference is not assigned.");
                loggedMissingPlayer = true;
            }
            return;
        }

        transform.position =
            player.transform.position + offset;

        transform.LookAt(player.transform);
    }
}