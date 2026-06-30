using UnityEngine;
using UnityEditor;

public class SetupPhysicsTestScene
{
    [MenuItem("Tools/Setup Physics Test Scene")]
    public static void SetupScene()
    {
        // 1. Clean up all the generic spheres, cubes, and duplicates!
        string[] junkNames = { 
            "PlayerManager", "LeftPhysicsHand", "RightPhysicsHand", 
            "EnemyDummy", "EnemyDummy_Anchor", "Sphere", "Dummy1 (1)_Anchor",
            "LeftHandTarget", "RightHandTarget"
        };
        
        int deletedCount = 0;
        foreach (var go in Object.FindObjectsOfType<GameObject>())
        {
            foreach (var junk in junkNames) 
            {
                if (go.name == junk || go.name == "EnemyDummy") 
                {
                    Undo.DestroyObjectImmediate(go);
                    deletedCount++;
                    break;
                }
            }
        }
        Debug.Log($"Cleaned up {deletedCount} duplicate/junk test objects!");

        // 2. Find your REAL gloves and camera from your scene!
        GameObject leftGlove = GameObject.Find("left glove");
        GameObject rightGlove = GameObject.Find("right glove");
        GameObject cam = GameObject.Find("Camera");

        if (leftGlove == null || rightGlove == null || cam == null)
        {
            Debug.LogError("Could not find 'left glove', 'right glove', or 'Camera'. Make sure they are in the scene exactly like the screenshot!");
            return;
        }

        // 3. Unparent the real gloves from the Camera!
        // Physics objects need to float freely in the world to work perfectly.
        leftGlove.transform.SetParent(null);
        rightGlove.transform.SetParent(null);

        // 4. Create one clean Manager
        GameObject playerManager = new GameObject("PlayerManager");
        HandInputManager inputManager = playerManager.AddComponent<HandInputManager>();

        // 5. Turn your real gloves into Physics hands!
        SetupGlovePhysics(leftGlove);
        SetupGlovePhysics(rightGlove);

        // 6. Create Targets. THESE stay parented to the Camera so your mouse moves them relative to your view!
        GameObject leftTarget = new GameObject("LeftHandTarget");
        leftTarget.transform.SetParent(cam.transform);
        
        GameObject rightTarget = new GameObject("RightHandTarget");
        rightTarget.transform.SetParent(cam.transform);

        inputManager.leftHandTarget = leftTarget.transform;
        inputManager.rightHandTarget = rightTarget.transform;

        leftGlove.GetComponent<PhysicsHandController>().targetTransform = leftTarget.transform;
        rightGlove.GetComponent<PhysicsHandController>().targetTransform = rightTarget.transform;

        Debug.Log("SUCCESS! Your real gloves are now hooked up to the physics system.");
    }

    private static void SetupGlovePhysics(GameObject glove)
    {
        // Add a collider if your glove model doesn't have one yet
        if (glove.GetComponent<Collider>() == null)
        {
            var col = glove.AddComponent<BoxCollider>();
            col.size = new Vector3(0.2f, 0.2f, 0.2f);
        }

        Rigidbody rb = glove.GetComponent<Rigidbody>();
        if (rb == null) rb = glove.AddComponent<Rigidbody>();
        rb.useGravity = false;
        
        if (glove.GetComponent<PunchCollider>() == null)
            glove.AddComponent<PunchCollider>();
            
        if (glove.GetComponent<PhysicsHandController>() == null)
            glove.AddComponent<PhysicsHandController>();
    }
}
