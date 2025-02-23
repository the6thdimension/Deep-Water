using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class DestroyerController : MonoBehaviour
{
    //public UnityEngine.Rendering.HighDefinition.WaterSurface Ocean;



    public float SwayHeightIntensity = .2f;
    public float SwayRollIntensity = .2f;
    public float SwayPitchIntensity = .0f;



    public float ShipSpeed = 0.0f;
    public float ShipTurn = 0.0f;





    [Header("Ship Elevators")]

    public float ElevatorRange = 11f;

    [Range(0.0f, 1.0f)]
    public float Elevator1Height = 0f;
    public Transform Elevator1;

    [Space(50)]

    [Range(0.0f, 1.0f)]
    public float Elevator2Height = 0f;
    public Transform Elevator2;

    [Space(50)]

    [Range(0.0f, 1.0f)]
    public float Elevator3Height = 0f;
    public Transform Elevator3;

    [Space(50)]
    


    [Header("Ship Catapults")]
    public float CatapultRange = 100f;
    [Range(0.0f, 1.0f)]
    public float Catapult1Distance = 0f;
    public GameObject BS1;
    public GameObject Shuttle1;
    public GameObject Aircraft1;


    [Space(50)]

    [Range(0.0f, 1.0f)]
    public float Catapult2Distance = 0f;
    public GameObject BS2;
    public GameObject Shuttle2;


    [Space(50)]


    [Range(0.0f, 1.0f)]
    public float Catapult3Distance = 0f;
    public GameObject BS3;
    public GameObject Shuttle3;


    [Space(50)]

    [Range(0.0f, 1.0f)]
    public float Catapult4Distance = 0f;
    public GameObject BS4;
    public GameObject Shuttle4;



    private Vector3 _startPosition;
    private Vector3 _startRotation;


    private List<Vector3> ElevatorPositions;
    private Vector3 Elevator1Start;
    private Vector3 Elevator2Start;
    private Vector3 Elevator3Start;


    
    private Vector3 Catapult1Start;
    private Vector3 Catapult2Start;
    private Vector3 Catapult3Start;    
    private Vector3 Catapult4Start;

    private Vector3 Cat1Vel;

    //Hangar
    public GameObject FA18E;


    // Start is called before the first frame update
    void Start()
    {
        _startPosition = transform.position;
        _startRotation = transform.rotation.eulerAngles;

        // Ocean.largeCurrentOrientationValue = transform.rotation.z - 90;

        Elevator1Start = Elevator1.localPosition;
        Elevator2Start = Elevator2.localPosition;
        Elevator3Start = Elevator3.localPosition;

        Catapult1Start = Shuttle1.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        handleSway();
        handleMovement();
        handleElevators();
        handleCatapults();
    }

    void handleSway()
    {
        transform.position = _startPosition + new Vector3(0.0f, Mathf.Sin(Time.time)*SwayHeightIntensity, 0.0f);
        transform.rotation = Quaternion.Euler(_startRotation.x + Mathf.Sin(Time.time)*SwayPitchIntensity, _startRotation.y, _startRotation.z + Mathf.Sin(Time.time)*SwayRollIntensity);
    }

    void handleMovement()
    {
        //Ship Tilting as if Turning
        // TODO, Leaving in basic tilt for now.
        if(ShipTurn > 0.0f || ShipTurn < 0.0f)
        {
            transform.rotation = Quaternion.Euler(transform.rotation.x, transform.rotation.y, transform.rotation.z + ShipTurn);
            
        }

        // Ship speed. Just scrolls Ocean.
        // if(Ocean)
        // {
        //     Ocean.largeCurrentSpeedValue = ShipSpeed * 2;

            
        // }
    }

    public void handleCatapults()
    {

    }

    public void handleElevators()
    {
        Elevator1.localPosition = Elevator1Start - new Vector3(0f, 0f , 0f);//ElevatorRange*Elevator1Height);
        Elevator2.localPosition = Elevator2Start - new Vector3(0f, 0f , 0f);//ElevatorRange*Elevator2Height);
        Elevator3.localPosition = Elevator3Start - new Vector3(0f, 0f , 0f);//ElevatorRange*Elevator3Height);
        
        // ElevatorPositions[0] =  Elevator1.localPosition;
        // ElevatorPositions[1] = Elevator2.localPosition;
        // ElevatorPositions[2] = Elevator3.localPosition;
    }

    public void RaiseBackBlast(GameObject cat)
    {
        Animator catAnim = cat.GetComponent<Animator>();

        catAnim.SetTrigger("Raise");
    }

    public void LowerBackBlast(GameObject cat)
    {
        Animator catAnim = cat.GetComponent<Animator>();

        catAnim.SetTrigger("Lower");
    }

    public void LaunchCat(GameObject shuttle)
    {
        Animator shutAnim = shuttle.GetComponent<Animator>();


        shutAnim.SetTrigger("Launch");


        StartCoroutine(shuttleRelease(Aircraft1));
       
        
    }
    
    IEnumerator shuttleRelease(GameObject Aircraft1)
    {
        ConfigurableJoint ShuttleJoint = Aircraft1.GetComponent<ConfigurableJoint>();
        Rigidbody rb = Aircraft1.GetComponent<Rigidbody>();
        
        yield return new WaitForSeconds(2);

        ShuttleJoint.xMotion = UnityEngine.ConfigurableJointMotion.Free;
        ShuttleJoint.yMotion = UnityEngine.ConfigurableJointMotion.Free;
        ShuttleJoint.zMotion = UnityEngine.ConfigurableJointMotion.Free;
        ShuttleJoint.angularXMotion = UnityEngine.ConfigurableJointMotion.Free;
        ShuttleJoint.angularYMotion = UnityEngine.ConfigurableJointMotion.Free;
        ShuttleJoint.angularZMotion = UnityEngine.ConfigurableJointMotion.Free;

        rb.linearVelocity = new Vector3(0f, 0f, 50f);

        







        // 
        // Vector3 v3Velocity = rb.velocity;
        // rb.constraints = RigidbodyConstraints.None;


        // Debug.Log(Cat1Vel);
        // ConfigurableJoint ShuttleJoint = Aircraft1.GetComponent<ConfigurableJoint>();

        
        // ShuttleJoint.connectedBody = null;

        
    }


    public void spawnFromHangar(int elevator, int spot, GameObject aircraft)
    {
        GameObject airplane = Instantiate(aircraft, Elevator2.position + new Vector3(0,4f,0), Quaternion.Euler(0,90f,0));
        airplane.transform.parent = null;
        AIAirplaneController APAI = airplane.GetComponent<AIAirplaneController>();

    }

}


