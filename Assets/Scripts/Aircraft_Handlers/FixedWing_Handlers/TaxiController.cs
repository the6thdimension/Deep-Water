using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaxiController : MonoBehaviour
{
    [Header("Contol Properties")]
    public float speed = 10.0f;
    public float rotationSpeed = 100.0f;
    public float steeringAngle = 88.0f;

    public Transform NWS;
    private Rigidbody rb;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        handleNWS();
    }

    void handleNWS()
    {
        float translation = Input.GetAxis("Vertical") * speed;
        float rotation = Input.GetAxis("Horizontal") * rotationSpeed;

        rotation *= Time.deltaTime;
        translation *= Time.deltaTime;

        // Move translation along the object's z-axis
        //transform.Translate(0, 0, translation);
        rb.AddRelativeForce(Vector3.forward * speed * translation);

        // Rotate around our y-axis
        NWS.transform.Rotate(0, rotation, 0);

    }
}
