
using UnityEngine;

public class CapybaraProjectile : MonoBehaviour
{
    private CapybaraThrower launcher;
    private bool collected;

    public void Initialize(CapybaraThrower owner)
    {
        launcher = owner;
        collected = false;
    }

    public void Collect()
    {
        if (collected || launcher == null)
            return;

        collected = true;
        launcher.CollectCoin(this);
    }
}
