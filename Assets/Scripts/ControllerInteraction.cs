﻿using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class ControllerInteraction : MonoBehaviour
{
    private FixedJoint attachJoint = null;
    private Rigidbody currentRigidBody = null;
    private List<Rigidbody> contactRigidBodies = new List<Rigidbody> ();

    private Animator anim;
    private GameObject cursor;
    private Renderer cursorRend;

    private Color cursorColor;
    private Color cursorOverColor;

    private float cursorDrawDistance = 100;

    void Awake()
    {
        attachJoint = GetComponent<FixedJoint> ();
    }

    void Start()
    {
        anim = gameObject.GetComponent<Animator>();
        cursor = transform.Find("Cursor").gameObject;
        cursorRend = cursor.GetComponent<Renderer>();
        cursorColor = Color.blue;
        cursorOverColor = Color.red;
    }

    void Update()
    {
        WebVRController controller = gameObject.GetComponent<WebVRController>();

        float normalizedTime = controller.GetButton("Trigger") ? 1 : controller.GetAxis("Grip");

        // Use the controller button or axis position to manipulate the playback time for hand model.
        anim.Play("Take", -1, normalizedTime);

        // Raycast from hand
        // Bit shift the index of the layer (8) to get a bit mask
        int layerMask = 1 << 8;

        // This would cast rays only against colliders in layer 8.
        // But instead we want to collide against everything except layer 8. The ~ operator does this, it inverts a bitmask.
        layerMask = ~layerMask;
        
        RaycastHit hit;
        bool isHit = Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out hit, Mathf.Infinity, layerMask);

        if (isHit)
        {
            cursor.transform.localScale = new Vector3(cursor.transform.localScale.x, cursor.transform.localScale.y, hit.distance);
            cursor.transform.localPosition = new Vector3(cursor.transform.localPosition.x, cursor.transform.localPosition.y, hit.distance / 2);
            cursorRend.material.color = cursorOverColor;
        }
        else
        {
            cursor.transform.localScale = new Vector3(cursor.transform.localScale.x, cursor.transform.localScale.y, cursorDrawDistance);
            cursor.transform.localPosition = new Vector3(cursor.transform.localPosition.x, cursor.transform.localPosition.y, cursorDrawDistance / 2);            
            cursorRend.material.color = cursorColor;
        }

        if (controller.GetButtonDown("Trigger") || controller.GetButtonDown("Grip")) {
        }

        if (controller.GetButtonUp("Trigger") || controller.GetButtonUp("Grip")) {
            if (isHit) {
                string image = hit.collider.gameObject.GetComponent<PanoIcon>().Image;
                RenderSettings.skybox = Resources.Load(image, typeof(Material)) as Material;
            }
        }

    }

    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag != "Interactable")
            return;

        contactRigidBodies.Add(other.gameObject.GetComponent<Rigidbody> ());
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.tag != "Interactable")
            return;

        contactRigidBodies.Remove(other.gameObject.GetComponent<Rigidbody> ());
    }

    public void Pickup() {
        currentRigidBody = GetNearestRigidBody ();

        if (!currentRigidBody)
            return;

        currentRigidBody.MovePosition(transform.position);
        attachJoint.connectedBody = currentRigidBody;
    }

    public void Drop() {
        if (!currentRigidBody)
            return;

        attachJoint.connectedBody = null;
        currentRigidBody = null;
    }

    private Rigidbody GetNearestRigidBody() {
        Rigidbody nearestRigidBody = null;
        float minDistance = float.MaxValue;
        float distance = 0.0f;

        foreach (Rigidbody contactBody in contactRigidBodies) {
            distance = (contactBody.gameObject.transform.position - transform.position).sqrMagnitude;

            if (distance < minDistance) {
                minDistance = distance;
                nearestRigidBody = contactBody;
            }
        }

        return nearestRigidBody;
    }
}
