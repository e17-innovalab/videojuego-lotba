
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "CapybaraThrower")]
public class CapybaraThrower : MonoBehaviour
{
    [Header("References")]
    public GameObject capybaraPrefab;
    public Transform throwPoint;
    public Transform basket;

    [Header("Launch")]
    public float throwForce = 12f;
    public float upwardForce = 4f;
    public float missTimeout = 3f;
    public float returnDelay = 0.5f;

    [Header("Family")]
    public int capybaraCount = 3;

    private GameObject currentCapybara;
    private Rigidbody currentRb;
    private bool inFlight;
    private bool returning;
    private float launchTime;

    void Start()
    {
        if (basket == null)
            basket = transform;

        SpawnCapybara();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame &&
            !inFlight && !returning &&
            currentCapybara != null)
        {
            ThrowCapybara();
        }

        if (inFlight && Time.time - launchTime > missTimeout)
        {
            LoseCapybara();
        }
    }

    void SpawnCapybara()
    {
        if (capybaraCount <= 0 || capybaraPrefab == null)
            return;

        currentCapybara = Instantiate(
            capybaraPrefab,
            basket.position,
            basket.rotation
        );

        currentRb = currentCapybara.GetComponent<Rigidbody>();

        if (currentRb == null)
        {
            Debug.LogError("Capybara prefab needs a Rigidbody!");
            Destroy(currentCapybara);
            currentCapybara = null;
            return;
        }

        currentRb.isKinematic = true;

        CapybaraProjectile projectile =
            currentCapybara.GetComponent<CapybaraProjectile>();

        if (projectile == null)
            projectile = currentCapybara.AddComponent<CapybaraProjectile>();

        projectile.Initialize(this);
    }

    void ThrowCapybara()
    {
        if (throwPoint == null)
        {
            Debug.LogError("Assign ThrowPoint in the Inspector!");
            return;
        }

        inFlight = true;
        launchTime = Time.time;

        currentCapybara.transform.SetParent(null);
        currentRb.isKinematic = false;
        currentRb.linearVelocity = Vector3.zero;
        currentRb.angularVelocity = Vector3.zero;

        Vector3 direction =
            throwPoint.forward * throwForce +
            Vector3.up * upwardForce;

        currentRb.AddForce(direction, ForceMode.Impulse);
        currentRb.AddTorque(
            Random.insideUnitSphere * 10f,
            ForceMode.Impulse
        );
    }

    public void CollectCoin(CapybaraProjectile projectile)
    {
        if (!inFlight || projectile.gameObject != currentCapybara)
            return;

        inFlight = false;
        returning = true;

        StartCoroutine(ReturnToBasket());
    }

    IEnumerator ReturnToBasket()
    {
        yield return new WaitForSeconds(returnDelay);

        if (currentCapybara != null)
        {
            currentRb.isKinematic = true;
            currentRb.linearVelocity = Vector3.zero;
            currentRb.angularVelocity = Vector3.zero;

            currentCapybara.transform.SetParent(basket);
            currentCapybara.transform.localPosition = Vector3.zero;
            currentCapybara.transform.localRotation = Quaternion.identity;
        }

        returning = false;
    }

    void LoseCapybara()
    {
        inFlight = false;
        capybaraCount--;

        if (currentCapybara != null)
            Destroy(currentCapybara);

        currentCapybara = null;
        currentRb = null;

        Debug.Log("Capybaras remaining: " + capybaraCount);

        if (capybaraCount > 0)
            SpawnCapybara();
        else
            Debug.Log("NO CAPYBARAS LEFT!");
    }
}
