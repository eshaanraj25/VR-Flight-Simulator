using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaneMovement : MonoBehaviour
{
    public float forwardSpeed = 60f, strafeSpeed= 40f, hoverSpeed = 20f; 
    private float activeForwardSpeed, activeStrafeSpeed, activeHoverSpeed;
    private float forwardAcceleration = 2.5f, strafeAcceleration = 2f, hoverAcceleration = 2f;

    public float lookRateSpeed = 1f;
    private Vector2 lookInput, screenCenter, mouseDistance;

    private float rollInput;
    public float rollspeed = 20f, rollAcceleration = 1f;

// Start is called before the first frame update

    void Start()
    {

        screenCenter.x = Screen.width* .5f;
        screenCenter.y = Screen.height* .5f;
      
    }    

// Update is called once per frame

void Update()
    {
    lookInput.x = Input.mousePosition.x;
    lookInput.y = Input.mousePosition.y;
    
    mouseDistance.x = (lookInput.x-screenCenter.x) / screenCenter.y;
    mouseDistance.y = (lookInput.y-screenCenter.y) / screenCenter.y;

    mouseDistance = Vector2.ClampMagnitude(mouseDistance, 1f);

    rollInput = Mathf.Lerp(rollInput, Input.GetAxisRaw("Roll"), rollAcceleration *Time.deltaTime);
    
    transform.Rotate(-mouseDistance.x * lookRateSpeed , mouseDistance.x * lookRateSpeed, 0f, Space.Self);
    
    activeForwardSpeed = Mathf.Lerp(activeForwardSpeed, Input.GetAxisRaw("Vertical") * forwardSpeed, forwardAcceleration * Time.deltaTime);
    activeStrafeSpeed= Mathf.Lerp(activeStrafeSpeed, Input.GetAxisRaw("Horizontal") * strafeSpeed, strafeAcceleration * Time.deltaTime); 
    activeHoverSpeed = Mathf.Lerp(activeHoverSpeed, Input.GetAxisRaw("Hover") * hoverSpeed, hoverAcceleration * Time.deltaTime);

    transform.position += transform.forward * activeForwardSpeed * Time.deltaTime;
    transform.position += (transform.right * activeStrafeSpeed * Time.deltaTime) + (transform.up * activeHoverSpeed * Time.deltaTime);

   }
}
