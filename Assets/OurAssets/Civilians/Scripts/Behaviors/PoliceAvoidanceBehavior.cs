using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoliceAvoidanceBehavior : MonoBehaviour
{
	[SerializeField]
	private bool policeDetected;
	[SerializeField]
	private bool avoidIntersection;
	[SerializeField]
	private Vector3 avoidPosition;
	[SerializeField]
	private float arriveThreshold;
	[SerializeField]
	private bool isPositionComputed;
	[SerializeField]
	private float forwardOffset;
	private Vector3 forwardDirection;

	private Vector3 policeCarPosition;

	private void Start()
	{
		ResetDefault();
	}

	private void OnTriggerEnter(Collider other)
	{
		if (isActiveAndEnabled)
		{
			if (other.CompareTag("PoliceEmitter"))
			{
				if (!Vector3.Equals(other.transform.position, policeCarPosition))
				{
					policeDetected = true;
					forwardDirection = transform.forward;
					policeCarPosition = other.transform.position;
				}
			}
			else if (other.CompareTag("InvisibleBlocker"))
			{
				InvisibleBlocker invisibleBlocker = other.GetComponent<InvisibleBlocker>();
				if (invisibleBlocker.IsAPoliceCar())
				{
					avoidIntersection = true;
					Debug.Log(name + " avoid " + other.name);
				}
			}
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (other.CompareTag("PoliceEmitter"))
		{
			policeDetected = false;
			isPositionComputed = false;
		}
	}

	public void ResumeMovement()
	{
		avoidIntersection = false;
	}

	public bool IsPoliceDetected()
	{
		return policeDetected;
	}

	public bool StopBeforeIntersection()
	{
		return avoidIntersection;
	}

	public bool WaitingPositionReached(Transform carFront)
	{
		return Utilities.DestinationReached(carFront.position, avoidPosition, arriveThreshold);
	}



	public bool ComputePositionToWait(Transform carFront, out Vector3 waitPosition)
	{
		Road road = Utilities.FindRoadUnderCar(transform);
		if (road != null)
		{
			Vector3 position = carFront.position + carFront.forward * forwardOffset;
			bool existsPosition = road.ComputeWaitingPosition(forwardDirection, position, out avoidPosition);
			isPositionComputed = existsPosition && !PositionOccupied(carFront.position, avoidPosition);
		}
		waitPosition = avoidPosition;
		return isPositionComputed;
	}

	private bool PositionOccupied(Vector3 position, Vector3 waitingPosition)
	{
		string[] layers = new string[] { "Police", "Civilian" };
		int layerMask = LayerMask.GetMask(layers);
		Vector3 direction = (waitingPosition - position).normalized;
		return Physics.BoxCast(transform.position, new Vector3(1f, 1f, 1f), direction,
			transform.rotation, Vector3.Distance(waitingPosition, position), layerMask, 
			QueryTriggerInteraction.Ignore);
	}

	public bool IsWaitingPositionComputed()
	{
		return isPositionComputed;
	}

	public void ResetDefault()
	{
		policeDetected = false;
		avoidIntersection = false;
		isPositionComputed = false;
		avoidPosition = Vector3.positiveInfinity;
		policeCarPosition = Vector3.positiveInfinity;
	}
}
