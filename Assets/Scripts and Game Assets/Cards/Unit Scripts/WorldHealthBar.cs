using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image fillImage; // assign in prefab

    [Header("Animation")]
    [SerializeField] private float tweenSeconds = 0.20f;

    private int maxHp = 1;
    private int currentHp = 1;
    private Coroutine anim;

    public void Init(int maxHp, int currentHp)
    {
        this.maxHp = Mathf.Max(1, maxHp);
        this.currentHp = Mathf.Clamp(currentHp, 0, this.maxHp);

        if (anim != null) StopCoroutine(anim);
        anim = null;

        SetFillInstant(this.currentHp);
    }

    public void AnimateTo(int newCurrentHp)
    {
        newCurrentHp = Mathf.Clamp(newCurrentHp, 0, maxHp);
        if (newCurrentHp == currentHp) return;

        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(AnimateRoutine(currentHp, newCurrentHp));
        currentHp = newCurrentHp;
    }

    public void SetFillInstant(int hp)
    {
        currentHp = Mathf.Clamp(hp, 0, maxHp);
        float t = (float)currentHp / maxHp;
        if (fillImage != null) fillImage.fillAmount = t;
    }

    private IEnumerator AnimateRoutine(int fromHp, int toHp)
    {
        float from = (float)fromHp / maxHp;
        float to = (float)toHp / maxHp;

        float elapsed = 0f;
        while (elapsed < tweenSeconds)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Clamp01(elapsed / tweenSeconds);
            if (fillImage != null) fillImage.fillAmount = Mathf.Lerp(from, to, a);
            yield return null;
        }

        if (fillImage != null) fillImage.fillAmount = to;
        anim = null;
    }
}
