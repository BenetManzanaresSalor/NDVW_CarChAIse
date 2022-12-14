using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(GameManager))]
public class PoliceManager : MonoBehaviour
{
	// Editable parameters
	[Header("Spawning")]
	[SerializeField] private uint PoliceCarsToSpawn = 3;
	[SerializeField] private Police PolicePrefab;
	[SerializeField] private bool StartChasing = true;
	[SerializeField] private bool CheckInPlayerView = true;
	[SerializeField] private bool ForceIgnorePlayer = false;

	[Header("Chasing")]
	[SerializeField] private float MinSpeedToCatch = 10;
	public float MinDistanceToCatch = 10;
	[SerializeField] private float CatchPointPerPolice = 5;
	[SerializeField] private float EscapePointPerSecond = 5;
	[SerializeField] private float EscapePointLostPlayerPerSecond = 20;
	[SerializeField] private float TimeForLosingPlayer = 10;
	[SerializeField] private float TimeForRelocatePolice = 30;

	// Auxiliar parameters
	private GameManager GameMang;
	public Player PlayerCar { get => GameMang.PlayerCar; }
	public RoadManager RoadMang { get => GameMang.RoadMang; }
	public List<Police> PoliceCars { get; protected set; }
	public float CatchCounter { get; protected set; }
	public bool PlayerIsLost { get => (Time.time - LastPlayerPosTime) > TimeForLosingPlayer; }
	private Vector3 LastPlayerKnownPos;
	private float LastPlayerPosTime;
	private float RealLastPlayerPosTime;    // It ignores Mathf.NegativeInfinity sentinel
	private float LastRelocateTime;

	#region Initialization

	private void Start()
	{
		// Initialize main variables		
		GameMang = GetComponent<GameManager>();
		PoliceCars = new List<Police>();

		CatchCounter = 0;
		LastRelocateTime = Time.time;

		// Initialize catch parameters
		if (StartChasing && !ForceIgnorePlayer)
		{
			LastPlayerKnownPos = PlayerCar.transform.position;
			LastPlayerPosTime = Time.time;
		}
		else
		{
			LastPlayerKnownPos = Vector3.zero;
			LastPlayerPosTime = Mathf.NegativeInfinity;
		}
		RealLastPlayerPosTime = Time.time;

		UpdateCatchCounter(0);
	}

	#endregion

	#region Update

	private void Update()
	{
		ManagePlayerVisual();
		CheckCatchState();
		CheckSpawnPolice(); // This should be done after ManagePlayerVisual
		CheckRelocatePolice();
	}

	#region Spawning and relocating

	private void CheckSpawnPolice()
	{
		// If too few police cars
		if (PoliceCars.Count < PoliceCarsToSpawn)
		{
			// Spawn remaining police cars			
			List<Marker> availableMarkers = GameMang.GetMarkersForSpawning(out List<GameObject> carsObjs,
				checkInPlayerView: CheckInPlayerView, defaultCar: PolicePrefab);
			GameObject policeObj;
			for (int i = PoliceCars.Count; i < PoliceCarsToSpawn; i++)
			{
				policeObj = PlacePolice(availableMarkers: availableMarkers, carsObjs: carsObjs);
				if (policeObj)  // If was possible to spawn a new police
					carsObjs.Add(policeObj);   // Add new police car to list for avoiding spawn at same position
			}
		}

		// If too many police cars
		else if (PoliceCars.Count > PoliceCarsToSpawn)
		{
			// Spawn remaining police cars
			for (int i = PoliceCars.Count - 1; i > PoliceCarsToSpawn - 1; i--)
			{
				Destroy(PoliceCars[i].gameObject);
				PoliceCars.RemoveAt(i);
			}
		}
	}

	public void IncreasePoliceNumber()
	{
		PoliceCarsToSpawn += 1;
	}

	private GameObject PlacePolice(Marker mkr = null, GameObject policeObj = null,
		List<Marker> availableMarkers = null, List<GameObject> carsObjs = null)
	{
		// Get marker
		if (mkr == null)
		{
			// Get a random marker
			if (availableMarkers.Count > 0)
			{
				int tries = 0;
				bool mkrFound = false;
				while (tries < 100)
				{
					mkr = availableMarkers[UnityEngine.Random.Range(0, availableMarkers.Count)];

					// Check the case that the position is occupied by a car spawned this frame
					mkrFound = RoadMang.IsMarkerAvailable(mkr, otherObjs: carsObjs);
					tries++;
				}

				if (!mkrFound)
					mkr = null;
			}
		}

		if (mkr == null)
			Debug.LogWarning("No marker found for spawning a police car.");
		else
		{
			// Spawn a new car
			if (policeObj == null)
			{
				policeObj = Instantiate(PolicePrefab.gameObject, mkr.transform.position, mkr.transform.rotation);
				policeObj.name = "Police_" + (PoliceCars.Count + 1);

				// Initialize police behaviour
				Police police = policeObj.GetComponent<Police>();
				police.SetPoliceManager(this);
				PoliceCars.Add(police);
			}
			// Replace car
			else
			{
				// Resume movement if was at traffic light
				TrafficLightBehavior trafficLightBehavior = policeObj.GetComponent<TrafficLightBehavior>();
				trafficLightBehavior.ResumeMovement();

				policeObj.transform.position = mkr.transform.position;
				policeObj.transform.rotation = mkr.transform.rotation;
			}

			// Set current waypoint
			MoveToWaypointBehavior moveToWaypointBehavior = policeObj.GetComponent<MoveToWaypointBehavior>();
			moveToWaypointBehavior.SetTargetMarker(mkr.GetNextAdjacentMarker());
		}

		return policeObj;
	}

	private void CheckRelocatePolice()
	{
		// If too many time since player is lost and since last relocate
		if (PlayerIsLost && (Time.time - RealLastPlayerPosTime) > TimeForRelocatePolice &&
			(Time.time - LastRelocateTime) > TimeForRelocatePolice)
		{
			// Search available markers
			List<Marker> availableMarkers = GameMang.GetMarkersForSpawning(out List<GameObject> carsObjs,
				checkInPlayerView: CheckInPlayerView, defaultCar: PolicePrefab);

			// Sort by distance to target
			availableMarkers = availableMarkers.OrderBy(mkr => (mkr.transform.position - GameMang.PlayerTarget).magnitude).ToList();

			// Remove first 5% of positions in order not to spawn too close to player
			availableMarkers.RemoveRange(0, availableMarkers.Count / 5);

			// Randomize the order of positions
			System.Random rnd = new System.Random();
			for (int i = 0; i < availableMarkers.Count && i < PoliceCars.Count * 4; i++)
			{
				int j = rnd.Next(i, availableMarkers.Count < PoliceCars.Count * 4 ? availableMarkers.Count : PoliceCars.Count * 4);
				Marker temp = availableMarkers[i];
				availableMarkers[i] = availableMarkers[j];
				availableMarkers[j] = temp;
			}


			// Move police cars to available markers. Maybe not all can be relocated
			int policeIdx = 0;
			Marker marker;
			for (int mkrIdx = 0; mkrIdx < availableMarkers.Count && policeIdx < PoliceCars.Count; mkrIdx++)
			{
				marker = availableMarkers[mkrIdx];

				// If police car is visible by player
				if (CheckInPlayerView && GameMang.IsCarVisibleByPlayer(PoliceCars[policeIdx].transform.position, PoliceCars[policeIdx]))
					policeIdx++;
				// If marker NOT occupied by a previously relocated car
				else if (RoadMang.IsMarkerAvailable(marker, otherObjs: carsObjs))
				{
					PlacePolice(marker, PoliceCars[policeIdx].gameObject);
					policeIdx++;
				}
			}

			// Reset relocate timer
			LastRelocateTime = Time.time;

			// Debug
			Debug.Log($"{policeIdx} police cars relocated");
		}
	}

	public Marker GetClosestMarker(Vector3 pos)
	{
		return RoadMang.GetClosestMarker(pos);
	}

	#endregion

	#region Player visual

	public void InformPlayerPos(Vector3 lastPlayerKnownPos, float lastPlayerPosTime)
	{
		// If update is more recent or the player is lost (some car at last player pos but without visual or contact) and not ignoring, update
		if ((lastPlayerPosTime > LastPlayerPosTime || lastPlayerPosTime == Mathf.NegativeInfinity) && !ForceIgnorePlayer)
		{
			LastPlayerKnownPos = lastPlayerKnownPos;
			LastPlayerPosTime = lastPlayerPosTime;
			if (lastPlayerPosTime != Mathf.NegativeInfinity)    // Ignoring patroling sentinel
				RealLastPlayerPosTime = lastPlayerPosTime;
		}
	}

	private void ManagePlayerVisual()
	{
		// If player is lost or to ignore, use Time == -Infinity; the sentinel for patrolling
		if (PlayerIsLost || ForceIgnorePlayer)
			LastPlayerPosTime = Mathf.NegativeInfinity;

		// Set multiple targets at different sides of the player
		Vector3[] targets = { LastPlayerKnownPos + 8 * PlayerCar.transform.forward,	// Extra offset for forward, trying to predict movement
			LastPlayerKnownPos + 4 * -PlayerCar.transform.forward,
			LastPlayerKnownPos + 2 * PlayerCar.transform.right,
			LastPlayerKnownPos + 2 * -PlayerCar.transform.right };

		// Communicate information to police cars, with a different target depending on the index
		for (int i = 0; i < PoliceCars.Count; i++)
			PoliceCars[i].SetPlayerPos(targets[i % targets.Length], LastPlayerPosTime);
	}

	#endregion

	#region Catch

	private void CheckCatchState()
	{
		// Get police cars close
		int nClosePoliceCars = 0;
		foreach (Police police in PoliceCars)
			if (Vector3.Distance(PlayerCar.transform.position, police.transform.position) < MinDistanceToCatch)
				nClosePoliceCars++;

		// If car going slow and police cars close, increment catch counter
		if (Mathf.Abs(PlayerCar.CurrentForwardSpeed) < MinSpeedToCatch && nClosePoliceCars > 0)
			UpdateCatchCounter(nClosePoliceCars * CatchPointPerPolice * Time.deltaTime);
		// Otherwise, decrement it
		else
		{
			if (PlayerIsLost)
				UpdateCatchCounter(-Time.deltaTime * EscapePointLostPlayerPerSecond);
			else
				UpdateCatchCounter(-Time.deltaTime * EscapePointPerSecond);
		}
	}

	private void UpdateCatchCounter(float increment)
	{
		CatchCounter += increment;
		CatchCounter = Mathf.Clamp(CatchCounter, 0, 100);
	}

	#endregion

	#endregion
}
