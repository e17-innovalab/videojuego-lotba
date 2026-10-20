using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioSource Soundtrack;

    [Header("Impact SFX")]
    public AudioSource SFXSource;
    public AudioClip[] impactSFX;

    public float normalPitch = 1.5f;
    public float impactPitch = 0.6f;

    private void Start()
    {
        Soundtrack.pitch = normalPitch;
    }

    public void ImpactPitch()
    {
        Soundtrack.pitch = impactPitch;
    }

    public void PlayRandomImpactSFX()
    {
        if (impactSFX == null || impactSFX.Length == 0)
            return;

        int index = Random.Range(0, impactSFX.Length);

        SFXSource.PlayOneShot(impactSFX[index]);
    }

    public void SetPitch(float t)
    {
        Soundtrack.pitch = Mathf.Lerp(impactPitch, normalPitch, t);
    }

    public void SetNormalPitch()
    {
        Soundtrack.pitch = normalPitch;
    }
}