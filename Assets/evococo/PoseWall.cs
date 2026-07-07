    using UnityEngine;

public class PoseWall : MonoBehaviour
{
        public GameObject brokenWall;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Glove"))
            {
                Instantiate(brokenWall, transform.position, transform.rotation);
                Destroy(gameObject);
            }
        }
    }