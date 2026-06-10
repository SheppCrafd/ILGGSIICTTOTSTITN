using UnityEngine;

public class Player : MonoBehaviour
{
    public World world;
    public float markerSize = 0.35f;

    public float x = 16;
    public float y = 16;

    public int z;

    public float speed = 5f;

    Transform marker;

    void Awake()
    {
        CreateMarker();
    }

    public void SpawnAtWorldCenter()
    {
        if (world == null)
        {
            Debug.LogError("[Player] Cannot spawn at world center: world is null.");
            return;
        }

        x = world.size * 0.5f;
        y = world.size * 0.5f;
        UpdatePosition();
    }

    void Update()
    {
        if (world == null)
        {
            Debug.LogWarning("[Player] world missing");
            return;
        }

        float dt = Time.deltaTime;

        if (Input.GetKey(KeyCode.W)) y += speed * dt;
        if (Input.GetKey(KeyCode.S)) y -= speed * dt;
        if (Input.GetKey(KeyCode.A)) x -= speed * dt;
        if (Input.GetKey(KeyCode.D)) x += speed * dt;

        x = Mathf.Clamp(x, 0, world.size - 1);
        y = Mathf.Clamp(y, 0, world.size - 1);

        UpdatePosition();
    }

    void UpdatePosition()
    {
        CreateMarker();

        int ix = Mathf.FloorToInt(x);
        int iy = Mathf.FloorToInt(y);

        var col = world.Get(ix, iy);

        z = 1;

        for (int i = col.Length - 1; i >= 0; i--)
        {
            if (col[i] != BlockType.Air)
            {
                z = i + 1;
                break;
            }
        }

        transform.position = new Vector3(x, z + 0.15f, y);
    }

    void CreateMarker()
    {
        if (marker != null)
            return;

        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "Player Dot";
        dot.transform.SetParent(transform, false);
        dot.transform.localPosition = Vector3.zero;
        dot.transform.localScale = Vector3.one * markerSize;

        var renderer = dot.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("[Player] 'Standard' shader not found. Ensure it is included in Always Included Shaders.");
            return;
        }
        renderer.sharedMaterial = new Material(shader);
        renderer.sharedMaterial.color = Color.red;

        var collider = dot.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        marker = dot.transform;
    }
}
