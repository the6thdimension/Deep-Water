using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Obi;

public class ArrestingWireController : MonoBehaviour
{
    public float speed = 5f;
	public ObiRopeCursor cursorR;
   	public ObiRopeCursor cursorL;

    //public float ropeLength = 20f;

	public ObiRope rope;

    // Start is called before the first frame update
    void Start()
    {
        // cursorR.ChangeLength(ropeLength - 1f * Time.deltaTime);
        // cursorL.ChangeLength(ropeLength - 1f * Time.deltaTime);
        
    }

    // Update is called once per frame
    void Update()
    {
        // Debug.Log(rope.restLength);
        if (Input.GetKey(KeyCode.Q)){
			if (rope.restLength > 6.5f)
				cursorR.ChangeLength(rope.restLength - speed * Time.deltaTime);
                cursorL.ChangeLength(rope.restLength - speed * Time.deltaTime);

		}

		if (Input.GetKey(KeyCode.E)){
			cursorR.ChangeLength(rope.restLength + speed * Time.deltaTime);
            cursorL.ChangeLength(rope.restLength + speed * Time.deltaTime);

		}
    }
}
