using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    // Movement Speed
    private float moveSpeed;
    public float walkSpeed = 6f;
    // Counteractive force to stop sloppy movement
    public float counterMovementFactor = 5f;
    public float walkHeight = 2f;
    // Calculating differences in speed
    private float desiredMoveSpeed;
    private float lastDesiredMoveSpeed;
    [Header("Running")]
    public bool AbilityToSprint;
    public float sprintSpeed = 10f;

    [Header("Jumping")]
    // Jump Forces
    public float regularJumpForce = 11f;
    public float slopeJumpForce = 12f;
    public float jumpCooldown = 0.2f;
    // How much movement during the air
    public float airMultiplier = 0.4f;
    public float airSpeedMultiplier = 0.5f;
    bool readyToJump;

    [Header("Crouching")]
    public bool AbilityToCrouch;
    public float crouchSpeed = 3f;
    public float crouchYscale = 0.5f;
    private float startYscale;
    public float crouchHeight = 1f;
    bool isCrouching;

    [Header("Sliding")]
    public bool AbilityToSlide;
    public float slideSpeed = 15f;
    public float slideForce = 100f;
    public float slideHeight = 1f;
    // How fast is the increase in speed on slopes whilst sliding
    public float speedIncreaseMultiplier = 4f;
    public float slopeIncreaseMultiplier = 2f;
    public float slideYScale = 0.5f;
    private bool isSliding;
    private float slideOnSlope = 1f;

    [Header("Wallrunning")]
    public bool AbilityToWallRun;
    public float wallRunSpeed = 10f;
    public float wallRunForce = 200f;
    public float wallCheckDistance = 1f;
    public float minJumpHeight = 0.1f;
    public float wallRunUpwardForce = 800f;
    public float wallJumpUpForce = 12f;
    public float wallJumpSideForce = 12f;
    public float exitWallTime = 0.2f;
    private float exitWallTimer;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;
    private bool wallLeft;
    private bool wallRight;
    private bool isWallRunning;
    private bool exitingWall;
    Vector3 wallNormal;
    [Header("Vaulting")]
    public bool AbilityToVault;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.C;
    public KeyCode slideKey = KeyCode.LeftControl;

    [Header("Layer Masks")]
    private float playerHeight;
    public LayerMask GroundLayer;
    public LayerMask WallLayer;
    bool grounded;
    bool cameraOffsetGround;

    [Header("Slope Handling")]
    public float slopeSpeed = 9f;
    public float maxSlopeAngle = 45f;
    public float SlopeForce = 30f;
    private RaycastHit slopeHit;
    private bool exitingSlope;
    [Header("Landing Feedback")]
    public bool turnOnLandingFeedback;
    public float hardLandingThreshold = 10f;  // Threshold speed for hard landing
    public GameObject landingEffect;          // Landing effect prefab or reference
    private bool landingFeedbackTriggered;

    [Header("References")]
    // Transform of player orientation
    public Transform orientation;
    public PlayerCam cam;

    // Inputs from player
    float HorizontalInput;
    float VerticalInput;
    Vector3 moveDirection;

    // player rigidbody
    Rigidbody rb;

    [Header("Different States of Player")]
    public MovementState state;
    public enum MovementState{
        walking,
        sprinting,
        crouching,
        sliding,
        wallrunning, 
        air
    }

    void Awake(){
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        readyToJump = true;
        startYscale = transform.localScale.y;
    }

    void Update()
    {
        if(isCrouching){
            playerHeight = crouchHeight;
        }
        else if(isSliding){
            playerHeight = slideHeight;
        }
        else{
            playerHeight = walkHeight;
        }
        
        // Checking Ground
        GroundCheck();

        // Inputs
        MyInput();

        // Preventing overspeeding
        SpeedControl();

        // Changing States
        StateHandler();

        // Checking for a wall
        CheckForWall();

        if(turnOnLandingFeedback){
            // Check for hard landing
            CheckForHardLanding();
        }
    }

    void FixedUpdate(){
        Movement();
    }

    // Switching between different states
    private void StateHandler(){

        // Crouching
        if(isCrouching && AbilityToCrouch){
            state = MovementState.crouching;
            desiredMoveSpeed = crouchSpeed;
        }

        else if(isSliding && AbilityToSlide){
            state = MovementState.sliding;
            if((OnSlope() || !grounded) && rb.linearVelocity.y < 0.1f){
                desiredMoveSpeed = slideSpeed;
            }
            else{
                desiredMoveSpeed = 13f;
            }
        }
        
        // Running
        else if(AbilityToSprint && grounded && Input.GetKey(sprintKey)) {
            state = MovementState.sprinting;
            desiredMoveSpeed = sprintSpeed;
        }

        // Walking
        else if(grounded){
            state = MovementState.walking;
            desiredMoveSpeed = walkSpeed;
        }

        // Air
        else {
            state = MovementState.air;
        }

        // check if desiredMoveSpeed has changed drastically
        if(Mathf.Abs(desiredMoveSpeed - lastDesiredMoveSpeed) > 4f && moveSpeed != 0){
            StopAllCoroutines();
            StartCoroutine(SmoothlyLerpMoveSpeed());
        }
        else{
            moveSpeed = desiredMoveSpeed;
        }

        lastDesiredMoveSpeed = desiredMoveSpeed;
    }

    private IEnumerator SmoothlyLerpMoveSpeed(){
        float time = 0;
        float difference = Mathf.Abs(desiredMoveSpeed - moveSpeed);
        float startValue = moveSpeed;

        while (time < difference){
            moveSpeed = Mathf.Lerp(startValue, desiredMoveSpeed, time / difference);
            if(OnSlope()){
                float slopeAngle = GetSlopeAngle();
                float slopeAngleIncrease = 1 + (slopeAngle / 90f);
                time += Time.deltaTime * speedIncreaseMultiplier * slopeIncreaseMultiplier * slopeAngleIncrease;
            }
            else {
                time += Time.deltaTime * speedIncreaseMultiplier;
            }
            yield return null;
        }

        moveSpeed = desiredMoveSpeed;
    }

    private void GroundCheck()
    {
        // Use a small sphere at the player's feet for ground detection
        grounded = Physics.CheckSphere(transform.position + Vector3.down * playerHeight * 0.5f, 0.2f, GroundLayer);

        cameraOffsetGround = Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.8f, GroundLayer);

        // Additional raycast for slopes
        if (OnSlope())
        {
            grounded = Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.2f, GroundLayer);
        }
    }

    private void MyInput(){
        // Moving Inputs
        HorizontalInput = Input.GetAxisRaw("Horizontal");
        VerticalInput = Input.GetAxisRaw("Vertical");

        // For Wallrunning
        if(AbilityToWallRun){
            if((wallRight || wallLeft) && VerticalInput > 0 && AboveGround() && !exitingWall){
                state = MovementState.wallrunning;
                desiredMoveSpeed = wallRunSpeed;
                StartWallRunning();
                
                if(Input.GetKeyDown(jumpKey))  
                    WallJump();
            }
            else if (exitingWall){
                StopWallRunning();

                if(exitWallTime > 0)
                    exitWallTimer -= Time.deltaTime;
                if(exitWallTimer <= 0)
                    exitingWall = false;
            }
            else{
                StopWallRunning();
            }
        }

        // For Sliding
        if(AbilityToSlide){
            if(Input.GetKeyDown(slideKey) && (HorizontalInput != 0 || VerticalInput != 0)){
                StartSlide();
            }

            if(Input.GetKeyUp(slideKey) && isSliding){
                StopSlide();
            }
        }

        // For Jumping
        if(Input.GetKey(jumpKey) && readyToJump && grounded && !isWallRunning){
            if(!OnSlope() && isSliding) return;
            readyToJump = false;

            Jump();

            Invoke(nameof(ResetJump), jumpCooldown);
        }

        // For Crouching
        if(AbilityToCrouch){
            if(Input.GetKeyDown(crouchKey)){
                StartCrouch();
            }

            if(Input.GetKeyUp(crouchKey)){
                StopCrouch();
            }
        }
    }

    private void Movement()
    {
        // Disable vertical input when sliding on slopes
        if (isSliding && OnSlope()) slideOnSlope = 0f;
        else slideOnSlope = 1f;

        // Calculate the move direction based on input and orientation
        moveDirection = orientation.forward * VerticalInput * slideOnSlope + orientation.right * HorizontalInput;

        // Multipliers for Sliding
        float multiplierV = 1f;
        if(isSliding) multiplierV = 0f;

        // If the player is on the ground
        if(grounded){
            // Apply movement force when grounded but on slope
            if (OnSlope() && !exitingSlope)
            {
                float slopeAngle = GetSlopeAngle();

                // if the slope angle is less than the max allowed angle
                if (slopeAngle <= maxSlopeAngle)
                {
                    if(!isSliding){
                        rb.AddForce(GetSlopeMoveDirection(moveDirection) * slopeSpeed * 20f, ForceMode.Force);

                        if(GetSlopeMoveDirection(moveDirection).y > 0) {
                            moveSpeed = walkSpeed;
                            desiredMoveSpeed = walkSpeed;
                        }
                    }
                    else {
                        rb.AddForce(Vector3.down * Time.fixedDeltaTime * 1500f);
                        if (HorizontalInput != 0) // Allow horizontal movement during sliding on slopes
                        {
                            rb.AddForce(GetSlopeMoveDirection(orientation.right * HorizontalInput) * slideForce * 0.2f, ForceMode.Force);
                        }
                    }
                    // Apply counter force to prevent sliding down
                    rb.AddForce(-Physics.gravity, ForceMode.Acceleration);
                }
                else
                {
                    // Apply additional downward force to simulate sliding down
                    rb.AddForce(Vector3.down * SlopeForce * 30f, ForceMode.Acceleration);
                }

                // Force to keep player attached to the slope
                rb.AddForce(-slopeHit.normal * SlopeForce * 10f, ForceMode.Force);
            }

            // If we are not on the Slope or we are exiting one
            else {
                // Movement when not on Slope
                rb.AddForce(moveDirection.normalized * moveSpeed * 10f * multiplierV, ForceMode.Force);

                if (isSliding) {
                    float velocityMagnitude = rb.linearVelocity.magnitude;

                    // Calculate a deceleration factor that increases as velocity magnitude decreases
                    float decelerationFactor = Mathf.Lerp(0.2f, 0.25f, Mathf.Clamp01(velocityMagnitude / 30f));

                    // Apply the deceleration factor to the counter movement
                    rb.AddForce(decelerationFactor * counterMovementFactor * Time.deltaTime * -rb.linearVelocity.normalized * 400f);
                    if(rb.linearVelocity.magnitude < 2f) {
                        CounterMovement();
                    }
                    return;
                }
            }

            // If not input and is not sliding, then counter force to prevent sloppy movement. 
            if (HorizontalInput == 0 && VerticalInput == 0 && !isSliding)
            {
                CounterMovement();
            }
        
        }

        // If Wallrunning
        else if(isWallRunning){
            // Wall Forward Force
            rb.AddForce(GetWallDirection() * wallRunForce, ForceMode.Force);
            // Apply a small upward force while wall running
            rb.AddForce(Vector3.up * wallRunUpwardForce * Time.fixedDeltaTime, ForceMode.Force);
            // If not getting away from Wall, apply a bit force to keep player stuck to wall
            if(!(wallLeft && HorizontalInput > 0) || !(wallRight && HorizontalInput < 0)){
                rb.AddForce(-GetWallNormal() * 100, ForceMode.Force);
            }

        }

        // When in the air
        else if (!grounded && !isWallRunning)
        {
            // Slower movement in the air through the airMultipliers
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier * airSpeedMultiplier, ForceMode.Force);

            // Apply Air Counter Movement when not pressing WASD
            if(HorizontalInput == 0 && VerticalInput == 0) {
                CounterMovementInAir();
            }
        }
    }

    private void CounterMovement()
    {
        // Apply counter force in the opposite direction of the current velocity
        Vector3 counterForce = -rb.linearVelocity * counterMovementFactor;

        // Set the counter force only in the horizontal plane
        counterForce.y = 0;

        // Apply the counter force to the Rigidbody
        rb.AddForce(counterForce, ForceMode.Force);

        StopAllCoroutines();
        desiredMoveSpeed = walkSpeed;
    }

    private void CounterMovementInAir()
    {
        // Apply counter force in the opposite direction of the current velocity with air Multiplier
        Vector3 counterForce = -rb.linearVelocity * counterMovementFactor * airMultiplier;

        // We don't want to counter vertical movement in air
        counterForce.y = 0; 
        
        // Apply the counter force to the Rigidbody
        rb.AddForce(counterForce, ForceMode.Force);
    }

    private void SpeedControl(){
        // Limit Speed on Slopes
        if(OnSlope() && !exitingSlope && !isSliding){
            // If current speed is greater than allow limite, slow them down.
            if(rb.linearVelocity.magnitude > moveSpeed){
                rb.linearVelocity = rb.linearVelocity.normalized * moveSpeed * 0.5f;
            }
        }

        else{
            // Limit Speed on X and Z planes
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            // limit velocity if needed
            if(flatVel.magnitude > moveSpeed){
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
            }
        }
    }

    // Jumping
    private void Jump(){
        exitingSlope = true;

        if(OnSlope()){
            if(isSliding && GetSlopeLookDirection().y > 0f) return;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * slopeJumpForce, ForceMode.Impulse); 
        } 
        else{
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * regularJumpForce, ForceMode.Impulse);
        }
    }

    private void ResetJump(){
        readyToJump = true;
        exitingSlope = false;
    }

    // Crouching
    private void StartCrouch(){
        isCrouching = true;

        transform.localScale = new Vector3(transform.localScale.x, crouchYscale, transform.localScale.z);
        if (grounded)
        {
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }
    }

    private void StopCrouch(){
        isCrouching = false;

        transform.localScale = new Vector3(transform.localScale.x, startYscale, transform.localScale.z);
    }

    // On Slope Handling
    public bool OnSlope(){
        if(Physics.Raycast(transform.position, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.2f, GroundLayer)){
            float angle = GetSlopeAngle();
            return angle  < maxSlopeAngle && angle > -maxSlopeAngle && angle != 0;
        }

        return false;
    }

    public Vector3 GetSlopeMoveDirection(Vector3 direction){
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

    private Vector3 GetSlopeLookDirection()
    {
        // Ensure we have valid slope data
        if (!OnSlope())
        {
            return orientation.forward;
        }

        // Get the slope normal
        Vector3 slopeNormal = slopeHit.normal;

        // Get the forward direction of the player's orientation
        Vector3 forwardDirection = orientation.forward;

        // Project the forward direction onto the plane of the slope
        Vector3 slopeLookDirection = Vector3.ProjectOnPlane(forwardDirection, slopeNormal).normalized;

        return slopeLookDirection;
    }

    private float GetSlopeAngle(){
        return Vector3.Angle(Vector3.up, slopeHit.normal);
    }

    // Sliding
    private void StartSlide(){
        isSliding = true;

        transform.localScale = new Vector3(transform.localScale.x, slideYScale, transform.localScale.z);
        
        if (grounded)
        {
            rb.AddForce(Vector3.down * 5f, ForceMode.Impulse);
        }
    }
    
    private void StopSlide(){
        isSliding = false;

        transform.localScale = new Vector3(transform.localScale.x, startYscale, transform.localScale.z);

        // Set the move speed to the desired speed (e.g., walkSpeed or runSpeed)
        moveSpeed = desiredMoveSpeed;
    }

    // Wallrunning
    private void CheckForWall(){
        wallRight = Physics.Raycast(transform.position, orientation.right, out rightWallHit, wallCheckDistance, WallLayer);
        wallLeft = Physics.Raycast(transform.position, -orientation.right, out leftWallHit, wallCheckDistance, WallLayer);
    }

    private bool AboveGround(){
        // Ensure the ray extends beyond the player's height to reliably detect ground
        float raycastDistance = playerHeight + minJumpHeight; // Adjust as necessary

        // Perform the raycast to check if there's ground beneath the player
        return !Physics.Raycast(transform.position, Vector3.down, raycastDistance, GroundLayer);
    }

    private void StartWallRunning(){
        isWallRunning = true;

        // Change Camera settings
        cam.ChangeFOV(90f);
        if(wallLeft) cam.TiltCamera(-5f);
        if(wallRight) cam.TiltCamera(5f); 
    }

    private void StopWallRunning(){
        isWallRunning = false;

        // Reset Camera settings
        cam.ChangeFOV(80f);
        cam.TiltCamera(0f);
    }

    private Vector3 GetWallNormal(){
        return wallNormal = wallRight ? rightWallHit.normal : leftWallHit.normal;
    }

    private Vector3 GetWallDirection(){
        Vector3 wallForward = Vector3.Cross(GetWallNormal(), transform.up);

        if((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude){
            wallForward = -wallForward;
        }

        return wallForward;
    }

    private void WallJump(){
        exitingWall = true;
        exitWallTimer = exitWallTime;

        Vector3 forceToApply = transform.up * wallJumpUpForce + GetWallNormal() * wallJumpSideForce;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(forceToApply, ForceMode.Impulse);
    }

    // Landing Feedback
    private void CheckForHardLanding()
    {
        if (cameraOffsetGround && rb.linearVelocity.y <= 0 && !isWallRunning && !landingFeedbackTriggered)
        {
            TriggerLandingFeedback();
            landingFeedbackTriggered = true;
        }
        else if (!cameraOffsetGround)
        {
            landingFeedbackTriggered = false; // Reset when the player is in the air
        }
    }

    private void TriggerLandingFeedback()
    {
        // Instantiate a landing effect at the player's position
        if (landingEffect != null)
        {
            Instantiate(landingEffect, transform.position, Quaternion.identity);
        }

        if(!isSliding){
            if(moveSpeed < 11f){
                // Trigger camera landing feedback
                cam.ApplyLandingFeedback(0.4f, 0.3f); // Adjust duration and strength as needed
            } else {
                // Trigger camera landing feedback
                cam.ApplyLandingFeedback(0.4f, 0.8f); // Adjust duration and strength as needed
            }
        }
    }
}
