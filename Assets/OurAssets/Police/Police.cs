using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class Police : CarController
{
	// Editable parameters
	[Header("Police Parameters")]
	[SerializeField] protected List<string> NavMeshLayers;
	[SerializeField] protected float BackwardSeconds = 1f;
	[SerializeField] protected float SecondsForBlocked = 0.5f;
	[SerializeField] protected float ReactionSeconds = 0.5f;
	[SerializeField] protected float CurveSpeedFactor = 1000;
	[SerializeField] protected bool CustomDebugger = false;
	[SerializeField] protected bool ShowGizmos = true;

    public AudioSource sirenAudioSource;

	// Auxiliar parameters
	protected PoliceManager PoliceMang;
	protected int NavMeshLayerBite;
	protected List<Vector3> Waypoints;
	protected int CurrentWayPoint;
	protected Vector3 PositionToFollow;
	protected Vector3 CurrentDirection;
	protected float NextActionTime;
	protected float BackwardEndTime;
	protected float BlockCheckStartTime;
	
	public float LastPlayerPosKnownTime { get; protected set; }
	public Vector3 LastPlayerKnownPos { get; protected set; }
	public bool IsBlocked { get => (Time.time - BlockCheckStartTime) > BackwardSeconds; }
	public bool IsGoingBackwards { get => Time.time < BackwardEndTime; }
	protected bool IsPatroling { get => LastPlayerPosKnownTime == Mathf.NegativeInfinity; }


	#region Initialization

	protected override void Start()
	{
		base.Start();

		// Blocked and backward settings
		BackwardEndTime = Mathf.NegativeInfinity;
		BlockCheckStartTime = Mathf.Infinity;

		// Catch settings with an already reached position and invalid/minimum frame index
		//NotifyPlayerPos(transform.position, 0); // This ensures that any new notification will be accepted

		// Path settings
		NextActionTime = 0;
		Waypoints = new List<Vector3>();
		CurrentWayPoint = 0;
		CalculateNavMashLayerBite();
	}

	private void CalculateNavMashLayerBite()
	{
		if (NavMeshLayers == null || NavMeshLayers[0] == "AllAreas")
			NavMeshLayerBite = NavMesh.AllAreas;
		else if (NavMeshLayers.Count == 1)
			NavMeshLayerBite += 1 << NavMesh.GetAreaFromName(NavMeshLayers[0]);
		else
		{
			foreach (string Layer in NavMeshLayers)
			{
				int I = 1 << NavMesh.GetAreaFromName(Layer);
				NavMeshLayerBite += I;
			}
		}
	}

	public void SetPoliceManager(PoliceManager policeMang)
	{
		PoliceMang = policeMang;
	}

	#endregion

	#region Player visual

	protected override void Update()
	{
		base.Update();
		CheckPlayerVisual();
	}

	protected virtual void CheckPlayerVisual()
	{
		Vector3 iniPos = CarFront.position;
		Vector3 direction = (PoliceMang.PlayerCar.transform.position - iniPos).normalized;
		if (Physics.Raycast(iniPos, direction, out RaycastHit hit, Mathf.Infinity, 0xFFFF) &&
			hit.transform.gameObject == PoliceMang.PlayerCar.gameObject)
		{
			//Debug.DrawRay(iniPos, direction * hit.distance, Color.red);
			LastPlayerKnownPos = PoliceMang.PlayerCar.transform.position;
			LastPlayerPosKnownTime = Time.time;
		}
	}

	/// <summary>
	/// Called by the PoliceManager. Time == -Infinity is a sentinel for start patrolling
	/// </summary>
	public void NotifyPlayerPos(Vector3 lastPlayerKnownPos, float lastPlayerPosTime)
	{
		// If notification is more recent or time == -Infinity, update the parameters | >= For time is required for correct initialization by PoliceManager
		if (lastPlayerPosTime >= LastPlayerPosKnownTime || lastPlayerPosTime == Mathf.NegativeInfinity)
		{
			LastPlayerKnownPos = lastPlayerKnownPos;
			LastPlayerPosKnownTime = lastPlayerPosTime;
		}
	}

	#endregion

	#region Driving and path

	protected override void FixedUpdate()
	{
		base.FixedUpdate();
		ProgessPath();
        // UpdatePoliceSound();
	}

    private void UpdatePoliceSound()
    {
        // Calculate the distance between the police car and the player
        float distance = Vector3.Distance(CarFront.position, PoliceMang.PlayerCar.transform.position);

        // Calculate the volume of the siren sound based on the distance
        float volume = Mathf.Lerp(1.0f, 0.0f, distance / 200.0f);

        // Set the volume of the siren audio source
        sirenAudioSource.volume = volume;
    }
    

	#region Path

	/// <summary>
	/// Checks if the agent has reached the currentWayPoint or not. If yes, it will assign the next waypoint as the currentWayPoint depending on the input
	/// </summary>
	protected virtual void ProgessPath()
	{
		// Update path if necessary
		if (IsPatroling)
		{
			Debug.Log(name + " police car patrolling");
			// TODO: Follow the civilian routes
		}
		else
		{
			CreatePathToTarget(LastPlayerKnownPos);

			// If possible, define PositionToFollow
			if (CurrentWayPoint > 0 && CurrentWayPoint < Waypoints.Count)
			{
				PositionToFollow = Waypoints[CurrentWayPoint];

				// While very close to current waypoint, go to the next one
				while (Vector3.Distance(CarFront.position, PositionToFollow) < 1 && CurrentWayPoint < Waypoints.Count)
				{
					CurrentWayPoint++;
					if (CurrentWayPoint < Waypoints.Count)
						PositionToFollow = Waypoints[CurrentWayPoint];
				}
			}

			// If not PositionToFollow possible, go to the target
			if (CurrentWayPoint <= 0 || CurrentWayPoint >= Waypoints.Count)
			{
				CurrentWayPoint = Waypoints.Count; // Set to an impossible value, as sentinel
				PositionToFollow = LastPlayerKnownPos;
			}

			// Reset movement direction and steering (already defined in base.FixedUpdate)
			Steer();
			Move();
		}
	}

	/// <summary>
	/// Creates a path to the Custom destination
	/// </summary>
	public virtual void CreatePathToTarget(Vector3 destination)
	{
		// If next action time, update path
		if (Time.time > NextActionTime)
		{
			NavMeshPath path = new NavMeshPath();
			Vector3 sourcePosition;

			NextActionTime += ReactionSeconds;
			Waypoints.Clear();
			CurrentWayPoint = 1;

			sourcePosition = CarFront.position;
			if (CurrentDirection == null)
				CurrentDirection = CarFront.forward;

			Calculate(destination, sourcePosition, CurrentDirection, NavMeshLayerBite);
			void Calculate(Vector3 destination, Vector3 sourcePosition, Vector3 direction, int NavMeshAreaBite)
			{
				if (NavMesh.SamplePosition(destination, out NavMeshHit hit, Mathf.Infinity, NavMeshAreaBite) &&
					NavMesh.CalculatePath(sourcePosition, hit.position, NavMeshAreaBite, path))
				{
					if (path.corners.ToList().Count() > 1)
					{
						Waypoints.AddRange(path.corners.ToList());
						CustomDebug("Custom Path generated successfully", false);
					}
					else
					{
						if (path.corners.Length > 2)
						{
							Waypoints.AddRange(path.corners.ToList());
							CustomDebug("Custom Path generated successfully", false);
						}
						else
						{
							CustomDebug("Failed to generate a Custom path. Waypoints are outside the AIFOV. Generating a new one", false);
						}
					}
				}
				else
				{
					CustomDebug("Failed to generate a Custom path. Invalid Path. Generating a new one", false);
				}
			}
		}
	}

	#endregion

	#region Steering

	protected override float GetSteeringAngle()
	{
		// Steer to PositionToFollow
		Vector3 relativeVector = transform.InverseTransformPoint(PositionToFollow);
		float steeringAngle = (relativeVector.x / relativeVector.magnitude) * MaxSteeringAngle;

		// If going backwards, inverse steer
		if (IsGoingBackwards)
			steeringAngle = -steeringAngle;

		return steeringAngle;
	}

	#endregion

	#region Movement

	protected override float GetMovementDirection()
	{
		float movementDirection = 1;    // By default, accelerate

		// If going backwards for avoiding blocking
		if (IsGoingBackwards)
		{
			movementDirection = -1;
		}
		// If no backwards
		else
		{
			// If car don't move and target is far, start the blocked timer (consider the car blocked)
			if (Mathf.Abs(CurrentForwardSpeed) < 0.1f && 
				Vector3.Distance(CarFront.position, LastPlayerKnownPos) > PoliceMang.MinDistanceToCatch / 2)
			{
				// If first time blocked
				if (BlockCheckStartTime == Mathf.Infinity)
					BlockCheckStartTime = Time.time;
				// Otherwise, check blocked time threshold
				else if (IsBlocked)
					BackwardEndTime = Time.time + BackwardSeconds;	// Set seconds for backwards
			}				
			else
				BlockCheckStartTime = Mathf.Infinity;

			// Define the movement direction (accelerating or braking) depending on the curves
			Vector3 prev, post, diff;
			float curveAngle, requiredSpeed, brakeDistance, distanceToWaypoint;
			// For each next waypoints in the path, get angle between them. End if already breaking
			for (int i = CurrentWayPoint - 1; i < Waypoints.Count - 1 && movementDirection == 1; i++)
			{
				// First check is the trajectory to target
				prev = (i < CurrentWayPoint) ? CarFront.position : Waypoints[i];
				post = (i < CurrentWayPoint) ? PositionToFollow : Waypoints[i + 1];
				diff = post - prev; // TODO: Consider the distance btw the waypoints and the distance to that path section

				// Get the angle of the curve
				curveAngle = Vector3.Angle(CarRigidBody.velocity.normalized, diff);

				// TODO: Check this
				//if (i < Waypoints.Count - 2)    // If no last point
				//else // If last point
				//	curveAngle = 180; // Force brake

				// Get distance required to achieve the required speed				
				requiredSpeed = CurveSpeedFactor / curveAngle;  // Example with 50 degrees: 1000/50 = 20 Km/h
				brakeDistance = EstimateBrakeDistance(requiredSpeed);
				distanceToWaypoint = Vector3.Distance(CarFront.position, post);

				// If distance is close or lower to the brake distance
				if ((distanceToWaypoint - brakeDistance) < 5)
					movementDirection = -1;
			}

		}

		return movementDirection;
	}

	#endregion

	#endregion

	#region Debug and Gizmos

	protected virtual void CustomDebug(string text, bool isCritical = false)
	{
		if (CustomDebugger)
		{
			if (isCritical)
				Debug.LogError(text);
			else
				Debug.Log(text);
		}
	}

	/// <summary>
	/// Shows a Gizmos representing the waypoints and AI FOV
	/// </summary>
	protected virtual void OnDrawGizmos()
	{
		if (ShowGizmos == true)
		{
			for (int i = 0; i < Waypoints.Count; i++)
			{
				if (i == CurrentWayPoint)
					Gizmos.color = Color.blue;
				else
				{
					if (i > CurrentWayPoint)
						Gizmos.color = Color.red;
					else
						Gizmos.color = Color.green;
				}
				Gizmos.DrawWireSphere(Waypoints[i], 2f);
			}
		}
	}

	#endregion
}
