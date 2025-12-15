using UnityEngine;
using UnityEngine.UI;

public class HeathBar : MonoBehaviour
{
    public Slider HealthSlider;
    public float HealthContainer = 100f;
    public float HealthFill;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HealthFill = HealthContainer;
    }

    // Update is called once per frame
    void Update()
    {
        if(HealthSlider.value!= HealthFill)
        {
            HealthSlider.value = HealthFill/100;
        } 
    }
}
