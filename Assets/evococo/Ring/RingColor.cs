using UnityEngine;

public class RingColor : MonoBehaviour
{
    Renderer rd;
    public GameObject hitVFX; // 👈 ลาก prefab VFX มาใส่ตรงนี้

    void Start()
    {
        rd = GetComponent<Renderer>();
        rd.material.color = Color.red;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<MouseHand>() != null)
        {
            Debug.Log("Perfect");
            rd.material.color = Color.green;

            // 💥 สร้าง VFX ตอนชน
            Instantiate(hitVFX, transform.position, Quaternion.identity);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<MouseHand>() != null)
        {
            rd.material.color = Color.red;
        }
    }
}