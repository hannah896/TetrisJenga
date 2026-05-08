using UnityEngine;
using TMPro;

public class Block : MonoBehaviour
{
    public int Value { get; private set; }

    public void Setup(int value)
    {
        Value = value;
        var label = GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = value.ToString();
    }
}
