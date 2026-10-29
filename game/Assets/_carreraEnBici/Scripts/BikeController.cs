using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "BikeController")]
public class BikeController : MonoBehaviour
{
    public float laneDistance = 3f;
    public float laneChangeSpeed = 10f;

    private int currentLane = 1;

    private BikeInput bikeInput;

    private Quaternion initialRotation;

    private float targetLean;
    public float maxLeanAngle = 15f;
    public float leanReturnSpeed = 100f;

    public float leanSpeed = 360f;

    void Start()
    {
        bikeInput = GetComponent<BikeInput>();
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        int direction = bikeInput.GetLaneDirection();

        if (direction != 0)
        {
            ChangeLane(direction);
        }

        // -------------------------
        // MOVE BIKE
        // -------------------------

        float targetX = (currentLane - 1) * laneDistance;

        Vector3 targetPosition = new Vector3(
            targetX,
            transform.position.y,
            transform.position.z
        );

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            laneChangeSpeed * Time.deltaTime
        );

        // -------------------------
        // LEAN BIKE
        // -------------------------

        targetLean = Mathf.MoveTowards(
            targetLean,
            0f,
            leanReturnSpeed * Time.deltaTime
        );

        Quaternion targetRotation =
            initialRotation * Quaternion.Euler(0f, 0f, targetLean);

        transform.localRotation = Quaternion.Lerp(
            transform.localRotation,
            targetRotation,
            10f * Time.deltaTime
        );
    }
        
    void ChangeLane(int direction)
    {
        currentLane += direction;
        currentLane = Mathf.Clamp(currentLane, 0, 2);

        targetLean = -direction * maxLeanAngle;
    }
}