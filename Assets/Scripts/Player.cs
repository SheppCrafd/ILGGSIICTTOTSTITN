using UnityEngine;

public class Player : MonoBehaviour
{
    public World world;
    public float markerSize = 0.35f;

    public float x = 16;
    public float y = 16;

    public int z;

    public float speed = 5f;
    public float jumpHeight = 2f;
    public float gravity = -20f;
    public float terminalVelocity = -50f;

    Transform marker;
    float verticalPosition;
    float verticalVelocity;
    bool hasVerticalPosition;
    bool isGrounded = true;

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
        hasVerticalPosition = false;
        verticalVelocity = 0f;
        isGrounded = true;
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
        float previousX = x;
        float previousY = y;

        if (Input.GetKey(KeyCode.W)) y += speed * dt;
        if (Input.GetKey(KeyCode.S)) y -= speed * dt;
        if (Input.GetKey(KeyCode.A)) x -= speed * dt;
        if (Input.GetKey(KeyCode.D)) x += speed * dt;

        x = Mathf.Clamp(x, 0, world.size - 1);
        y = Mathf.Clamp(y, 0, world.size - 1);

        ResolveBlockedHorizontalMovement(previousX, previousY);
        ApplyGravity(dt, Input.GetKeyDown(KeyCode.Space));
        UpdatePosition();
    }

    void ResolveBlockedHorizontalMovement(float previousX, float previousY)
    {
        if (!hasVerticalPosition)
            return;

        if (GroundHeight() <= verticalPosition)
            return;

        x = previousX;
        y = previousY;
    }

    void ApplyGravity(float dt, bool jumpPressed)
    {
        float groundHeight = GroundHeight();

        if (!hasVerticalPosition)
        {
            verticalPosition = groundHeight;
            verticalVelocity = 0f;
            hasVerticalPosition = true;
        }

        if (isGrounded && verticalPosition > groundHeight)
            isGrounded = false;

        if (isGrounded)
        {
            verticalVelocity = 0f;

            if (jumpPressed && jumpHeight > 0f && gravity < 0f)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                isGrounded = false;
            }
        }

        if (!isGrounded)
        {
            verticalVelocity = Mathf.Max(terminalVelocity, verticalVelocity + gravity * dt);
            verticalPosition += verticalVelocity * dt;

            if (verticalPosition <= groundHeight)
            {
                verticalPosition = groundHeight;
                verticalVelocity = 0f;
                isGrounded = true;
            }
        }

        z = Mathf.FloorToInt(verticalPosition);
    }

    float GroundHeight()
    {
        int ix = Mathf.FloorToInt(x);
        int iy = Mathf.FloorToInt(y);

        var col = world.Get(ix, iy);

        return Mathf.Max(1, WorldUtils.ColumnHeight(col));
    }

    void UpdatePosition()
    {
        CreateMarker();

        if (!hasVerticalPosition)
        {
            verticalPosition = GroundHeight();
            z = Mathf.FloorToInt(verticalPosition);
            hasVerticalPosition = true;
        }

        transform.position = new Vector3(x, verticalPosition + 0.15f, y);
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
        renderer.sharedMaterial = WorldUtils.CreateFallbackMaterial("Player", Color.red);
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