
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "CamionController")]
public class CamionController : MonoBehaviour
{
    [Header("Truck")]
    [SerializeField] private float speed = 1f;

    [Header("Bicycle Detection")]
    [SerializeField] private Transform bicycleRoot;

    [Header("Collision Effects")]
    [SerializeField] private ScreenShake screenShake;
    [SerializeField] private ScreenColor screenColor;
    [SerializeField] private AudioManager audioManager;

    private void Start()
    {
        Animator animator = GetComponent<Animator>();

        if (animator != null)
            animator.speed = speed;

        Collider truckCollider = GetComponent<Collider>();

        if (truckCollider != null)
            truckCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (bicycleRoot == null)
            return;

        // Is the colliding object the bicycle or one of its children?
        if (other.transform != bicycleRoot &&
            !other.transform.IsChildOf(bicycleRoot))
            return;

        // Visual impact feedback
        if (screenShake != null)
            screenShake.Shake();

        if (screenColor != null)
            screenColor.Flash();

        // Audio impact feedback
        if (audioManager != null)
            audioManager.PlayRandomImpactSFX();
            audioManager.ImpactPitch();
    }

    public void Initialize(
    Transform bicycle,
    ScreenShake shake,
    ScreenColor color,
    AudioManager audio)
    {
        bicycleRoot = bicycle;
        screenShake = shake;
        screenColor = color;
        audioManager = audio;
    }
}
