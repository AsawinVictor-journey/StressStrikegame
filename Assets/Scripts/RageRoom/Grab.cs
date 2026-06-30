using UnityEngine;

public class GrabHand : MonoBehaviour
{
    public Transform holdPoint;
    public Rigidbody handRigidbody;

    GameObject nearby;
    Rigidbody grabbed;
    FixedJoint grabJoint;


    void Update()
    {
        if(Input.GetKeyDown(KeyCode.F))
        {
            if(grabbed == null)
                Grab();
            else
                Release();
        }
    }


    void Grab()
    {
        if(nearby == null)
            return;

        grabbed = nearby.GetComponent<Rigidbody>();

        if(grabbed)
        {
            // Snap object to hold position then freeze its velocity
            grabbed.transform.position = holdPoint.TransformPoint(new Vector3(0f, 0f, .2f));
            grabbed.transform.rotation = holdPoint.rotation;
            grabbed.linearVelocity    = Vector3.zero;
            grabbed.angularVelocity   = Vector3.zero;

            // Joint holds it without making it kinematic so the other hand
            // can still collide and trigger damage on the object
            grabJoint = grabbed.gameObject.AddComponent<FixedJoint>();
            grabJoint.connectedBody = handRigidbody != null
                ? handRigidbody
                : GetComponentInParent<Rigidbody>();
        }
    }


    void Release()
    {
        if (grabbed == null) return;

        if (grabJoint != null)
        {
            Destroy(grabJoint);
            grabJoint = null;
        }

        grabbed.AddForce(
            transform.forward * 8,
            ForceMode.Impulse
        );

        grabbed = null;
    }


    void OnTriggerEnter(Collider other)
    {
        if(other.attachedRigidbody)
            nearby = other.gameObject;
    }


    void OnTriggerExit(Collider other)
    {
        if(other.gameObject == nearby)
            nearby = null;
    }
}
