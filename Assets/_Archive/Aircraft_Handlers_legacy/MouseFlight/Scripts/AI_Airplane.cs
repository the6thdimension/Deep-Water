using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AI_Airplane : MonoBehaviour
{
	[SerializeField]
	AI_Plane	m_Plane		= null;

	[Header("Transforms")]
	[SerializeField]
	Transform			m_Propeller = null;
	[SerializeField]
	Transform			m_FlapL		= null;
	[SerializeField]
	Transform			m_FlapR		= null;
	[SerializeField]
	Transform			m_FlapRearL = null;
	[SerializeField]
	Transform			m_FlapRearR = null;
	[SerializeField]
	Transform			m_Rudder	= null;

	[Header("Audio")]
	[SerializeField]
	AudioSource			m_PropellerSound = null;

	private void Start ()
	{
		m_PropellerSound.volume = 0;
	}

	void Update ()
	{
		m_Propeller.Rotate(Vector3.right, 360 * 2 * Time.deltaTime, Space.Self);

		float leftFlap  = Mathf.Clamp(-m_Plane.Pitch - m_Plane.Roll, -1, 1);
		float rightFlap = Mathf.Clamp(-m_Plane.Pitch + m_Plane.Roll, -1, 1);

		m_FlapL.localRotation		= Quaternion.Lerp(m_FlapL.localRotation,		Quaternion.Euler(0, leftFlap  * 50, 0),		Time.deltaTime * 4);
		m_FlapR.localRotation		= Quaternion.Lerp(m_FlapR.localRotation,		Quaternion.Euler(0, rightFlap * 50, 0),		Time.deltaTime * 4);
		m_FlapRearL.localRotation	= Quaternion.Lerp(m_FlapRearL.localRotation,	Quaternion.Euler(0, leftFlap  * 50, 0),		Time.deltaTime * 4);
		m_FlapRearR.localRotation	= Quaternion.Lerp(m_FlapRearR.localRotation,	Quaternion.Euler(0, rightFlap * 50, 0),		Time.deltaTime * 4);
		m_Rudder.localRotation		= Quaternion.Lerp(m_Rudder.localRotation,		Quaternion.Euler(0, 0, -m_Plane.Yaw * 60),	Time.deltaTime * 4);

		m_PropellerSound.volume = Mathf.Lerp(m_PropellerSound.volume, 1, Time.deltaTime * 0.5f);
		m_PropellerSound.pitch = Mathf.Lerp(m_PropellerSound.pitch, 1 + Mathf.Pow(Mathf.Abs(m_Plane.Pitch), 2) * 0.5f, Time.deltaTime * 0.5f);
	}
}