using UnityEngine;

public class BreakWall : MonoBehaviour
{
    public GameObject brokenWall;

    private bool broken = false;

    private void OnTriggerEnter(Collider other)
    {
        if (broken) return;

        if (other.CompareTag("Glove"))
        {
            broken = true;

            Instantiate(brokenWall,
                        transform.position,
                        transform.rotation);

            Destroy(gameObject);
        }
    }
}