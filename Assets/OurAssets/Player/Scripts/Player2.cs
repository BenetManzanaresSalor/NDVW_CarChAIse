using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class Player2 : CarController2
{
    [Header("Handbrake")]
    [SerializeField] protected bool EnableHandBrake = true;
    [SerializeField] protected float HardBrakeStifnessMultiplier = 0.5f;

    [Header("Camera")]
    [SerializeField] protected Camera MainCamera;
    [SerializeField] protected float SecondsToRotate = 0.25f;

    [Header("Canvas")]
    [SerializeField] protected Image SpeedBar;
    [SerializeField] protected TextMeshProUGUI SpeedText;
    [SerializeField] protected Image HealthBar;
    [SerializeField] protected TextMeshProUGUI HealthText;

    // Auxiliar variables
    protected float BackWheelsOriginalStiffness;
    protected WheelFrictionCurve BackWheelsFrictionCurve;

	#region Initialization

	protected override void Start()
	{
		base.Start();

        // Get info from backwheels for hand brake        
        BackWheelsFrictionCurve = WheelColliders[2].sidewaysFriction;   // The 2 first wheels are the directional/steering ones
        BackWheelsOriginalStiffness = BackWheelsFrictionCurve.stiffness;

        // Try to get camera if not available
        if (MainCamera == null)
            MainCamera = GetComponentInChildren<Camera>();        

        // Set health text if available
        if (HealthText)
            HealthText.text = ((int)CurrentHealth).ToString();
    }

	#endregion

	#region Controls

	protected override float GetSteeringAngle()
    {
        float steeringValue = Input.GetAxis("Horizontal");
        float steeringAngle = steeringValue * MaxSteeringAngle;
        return steeringAngle;
    }

    protected override float GetMovementDirection()
    {
        return Input.GetAxis("Vertical");
    }

	#endregion

	#region Handbrake

	protected override void FixedUpdate()
	{
        base.FixedUpdate();
        CheckHandBrake();
    }

    protected virtual void CheckHandBrake()
	{
        if (EnableHandBrake)
		{
            float brakeRatio = Mathf.Abs(Input.GetAxis("Jump"));

            // Change stiffness        
            float newStiffness = Mathf.Lerp(BackWheelsOriginalStiffness,
                BackWheelsOriginalStiffness * HardBrakeStifnessMultiplier,
                brakeRatio);
            BackWheelsFrictionCurve.stiffness = newStiffness;
            for (int i = 2; i < WheelColliders.Length; i++) // The 2 first wheels are the directional/steering ones
                WheelColliders[i].sidewaysFriction = BackWheelsFrictionCurve;

            // Apply hand brake
            HandBrake(brakeRatio);
        }
    }
    
    protected virtual void HandBrake(float brakeRatio)
	{
        for (int i = WheelColliders.Length / 2; i < WheelColliders.Length; i++)
            WheelColliders[i].brakeTorque = brakeRatio * BrakeTorque;
    }

	#endregion

	#region Visual

	protected override void Update()
    {
        base.Update();

        // Move camera according to car's velocity
        if (MainCamera && CarRigidBody.velocity.magnitude > 0.5f)
		{
            float angleDiff = Vector3.SignedAngle(MainCamera.transform.forward, CarRigidBody.velocity.normalized, axis:Vector3.up);
            MainCamera.transform.RotateAround(transform.position, Vector3.up, angleDiff * Time.deltaTime / SecondsToRotate);
        }
        

        // Canvas
        if (SpeedText)
            SpeedText.text = (int)CurrentWheelsSpeed + "km/h ";
        if (SpeedBar)
            SpeedBar.fillAmount = Mathf.Abs(CurrentWheelsSpeed) / MaxSpeed;
    }

	protected override void UpdateHealth(float healthModification)
	{
		base.UpdateHealth(healthModification);
        
        // Canvas
        if (HealthBar)
            HealthBar.fillAmount = CurrentHealth / MaxHealth;
        if (HealthText)
            HealthText.text = ((int)CurrentHealth).ToString();
    }

	#endregion
}
