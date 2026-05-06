using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.InputSystem; // Added for New Input System

public class KartController : MonoBehaviour
{
    public Transform kartModel;
    public Transform kartNormal;
    public Rigidbody sphere;

    public List<ParticleSystem> primaryParticles = new List<ParticleSystem>();
    public List<ParticleSystem> secondaryParticles = new List<ParticleSystem>();

    float speed, currentSpeed;
    float rotate, currentRotate;
    int driftDirection;
    float driftPower;
    int driftMode = 0;
    bool first, second, third;
    Color c;

    [Header("Bools")]
    public bool drifting;

    [Header("Parameters")]
    public float acceleration = 30f;
    public float steering = 80f;
    public float gravity = 10f;
    public LayerMask layerMask;

    [Header("Model Parts")]
    public Transform frontWheels;
    public Transform backWheels;
    public Transform steeringWheel;

    [Header("Particles")]
    public Transform wheelParticles;
    public Transform flashParticles;
    public Color[] turboColors;

    void Start()
    {
        for (int i = 0; i < wheelParticles.GetChild(0).childCount; i++)
        {
            primaryParticles.Add(wheelParticles.GetChild(0).GetChild(i).GetComponent<ParticleSystem>());
        }

        for (int i = 0; i < wheelParticles.GetChild(1).childCount; i++)
        {
            primaryParticles.Add(wheelParticles.GetChild(1).GetChild(i).GetComponent<ParticleSystem>());
        }

        foreach (ParticleSystem p in flashParticles.GetComponentsInChildren<ParticleSystem>())
        {
            secondaryParticles.Add(p);
        }
    }

    void Update()
    {
        // 1. Get Inputs from New Input System
        var keyboard = Keyboard.current;
        if (keyboard == null) return; // Safety check

        // Accelerate (Mapping Fire1 to Space/W/UpArrow)
        bool accelerateInput = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed || keyboard.spaceKey.isPressed;

        // Horizontal Axis (A/D or Arrows)
        float horizontalInput = 0;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontalInput -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontalInput += 1f;

        // Drift/Jump (Mapping Jump to LeftShift)
        bool driftPressedThisFrame = keyboard.leftShiftKey.wasPressedThisFrame;
        bool driftReleasedThisFrame = keyboard.leftShiftKey.wasReleasedThisFrame;

        // 2. Follow Collider
        transform.position = sphere.transform.position - new Vector3(0, 0.4f, 0);

        // 3. Accelerate Logic
        if (accelerateInput)
            speed = acceleration;

        // 4. Steer Logic
        if (horizontalInput != 0)
        {
            int dir = horizontalInput > 0 ? 1 : -1;
            float amount = Mathf.Abs(horizontalInput);
            Steer(dir, amount);
        }

        // 5. Drift Logic
        if (driftPressedThisFrame && !drifting && horizontalInput != 0)
        {
            drifting = true;
            driftDirection = horizontalInput > 0 ? 1 : -1;

            foreach (ParticleSystem p in primaryParticles)
            {
                var pmain = p.main;
                pmain.startColor = Color.clear;
                p.Play();
            }

            kartModel.parent.DOComplete();
            kartModel.parent.DOPunchPosition(transform.up * .2f, .3f, 5, 1);
        }

        if (drifting)
        {
            float control = (driftDirection == 1) ? ExtensionMethods.Remap(horizontalInput, -1, 1, 0, 2) : ExtensionMethods.Remap(horizontalInput, -1, 1, 2, 0);
            float powerControl = (driftDirection == 1) ? ExtensionMethods.Remap(horizontalInput, -1, 1, .2f, 1) : ExtensionMethods.Remap(horizontalInput, -1, 1, 1, .2f);
            Steer(driftDirection, control);
            driftPower += powerControl * 100f * Time.deltaTime;

            ColorDrift();
        }

        if (driftReleasedThisFrame && drifting)
        {
            Boost();
        }

        currentSpeed = Mathf.SmoothStep(currentSpeed, speed, Time.deltaTime * 12f); speed = 0f;
        currentRotate = Mathf.Lerp(currentRotate, rotate, Time.deltaTime * 4f); rotate = 0f;

        // 6. Animations
        // a) Kart
        if (!drifting)
        {
            kartModel.localEulerAngles = Vector3.Lerp(kartModel.localEulerAngles, new Vector3(0, 90 + (horizontalInput * 15), kartModel.localEulerAngles.z), .2f);
        }
        else
        {
            float control = (driftDirection == 1) ? ExtensionMethods.Remap(horizontalInput, -1, 1, .5f, 2) : ExtensionMethods.Remap(horizontalInput, -1, 1, 2, .5f);
            kartModel.parent.localRotation = Quaternion.Euler(0, Mathf.LerpAngle(kartModel.parent.localEulerAngles.y, (control * 15) * driftDirection, .2f), 0);
        }

        // b) Wheels
        frontWheels.localEulerAngles = new Vector3(0, (horizontalInput * 15), frontWheels.localEulerAngles.z);
        frontWheels.localEulerAngles += new Vector3(0, 0, sphere.linearVelocity.magnitude / 2);
        backWheels.localEulerAngles += new Vector3(0, 0, sphere.linearVelocity.magnitude / 2);

        // c) Steering Wheel
        steeringWheel.localEulerAngles = new Vector3(-25, 90, (horizontalInput * 45));
    }

    private void FixedUpdate()
    {
        if (!drifting)
            sphere.AddForce(-kartModel.transform.right * currentSpeed, ForceMode.Acceleration);
        else
            sphere.AddForce(transform.forward * currentSpeed, ForceMode.Acceleration);

        sphere.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        transform.eulerAngles = Vector3.Lerp(transform.eulerAngles, new Vector3(0, transform.eulerAngles.y + currentRotate, 0), Time.deltaTime * 5f);

        RaycastHit hitNear;
        Physics.Raycast(transform.position + (transform.up * .1f), Vector3.down, out hitNear, 2.0f, layerMask);

        kartNormal.up = Vector3.Lerp(kartNormal.up, hitNear.normal, Time.deltaTime * 8.0f);
        kartNormal.Rotate(0, transform.eulerAngles.y, 0);
    }

    public void Boost()
    {
        drifting = false;

        if (driftMode > 0)
        {
            DOVirtual.Float(currentSpeed * 3, currentSpeed, .3f * driftMode, Speed);
            kartModel.Find("Tube001").GetComponentInChildren<ParticleSystem>().Play();
            kartModel.Find("Tube002").GetComponentInChildren<ParticleSystem>().Play();
        }

        driftPower = 0;
        driftMode = 0;
        first = false; second = false; third = false;

        foreach (ParticleSystem p in primaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = Color.clear;
            p.Stop();
        }

        kartModel.parent.DOLocalRotate(Vector3.zero, .5f).SetEase(Ease.OutBack);
    }

    public void Steer(int direction, float amount)
    {
        rotate = (steering * direction) * amount;
    }

    public void ColorDrift()
    {
        if (!first) c = Color.clear;

        if (driftPower > 50 && driftPower < 100 - 1 && !first)
        {
            first = true;
            c = turboColors[0];
            driftMode = 1;
            PlayFlashParticle(c);
        }

        if (driftPower > 100 && driftPower < 150 - 1 && !second)
        {
            second = true;
            c = turboColors[1];
            driftMode = 2;
            PlayFlashParticle(c);
        }

        if (driftPower > 150 && !third)
        {
            third = true;
            c = turboColors[2];
            driftMode = 3;
            PlayFlashParticle(c);
        }

        foreach (ParticleSystem p in primaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = c;
        }

        foreach (ParticleSystem p in secondaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = c;
        }
    }

    void PlayFlashParticle(Color c)
    {
        foreach (ParticleSystem p in secondaryParticles)
        {
            var pmain = p.main;
            pmain.startColor = c;
            p.Play();
        }
    }

    private void Speed(float x)
    {
        currentSpeed = x;
    }
}
