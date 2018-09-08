using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

public enum WebVRState { ENABLED, NORMAL }

public class WebVRManager : MonoBehaviour
{
    [Tooltip("Name of the key used to alternate between VR and normal mode. Leave blank to disable.")]
    public string toggleVRKeyName;

    [HideInInspector]
    public WebVRState vrState = WebVRState.NORMAL;
    
    private static WebVRManager instance;

    [Tooltip("Preserve the manager across scenes changes.")]
    public bool dontDestroyOnLoad = true;
    
    [Header("Tracking")]

    [Tooltip("Default height of camera if no room-scale transform is present.")]
    public float DefaultHeight = 1.2f;

    [Tooltip("Represents the size of physical space available for XR.")]
    public UnityEngine.XR.TrackingSpaceType TrackingSpace = UnityEngine.XR.TrackingSpaceType.RoomScale;

    public delegate void VRCapabilitiesUpdate(WebVRDisplayCapabilities capabilities);
    public event VRCapabilitiesUpdate OnVRCapabilitiesUpdate;
    
    public delegate void VRChange(WebVRState state);
    public event VRChange OnVRChange;
    
    public delegate void HeadsetUpdate(
        Matrix4x4 leftProjectionMatrix,
        Matrix4x4 leftViewMatrix,
        Matrix4x4 rightProjectionMatrix,
        Matrix4x4 rightViewMatrix,
        Matrix4x4 sitStandMatrix);
    public event HeadsetUpdate OnHeadsetUpdate;
   
    public delegate void ControllerUpdate(int index, 
        string hand, 
        Vector3 position, 
        Quaternion rotation, 
        Matrix4x4 sitStand, 
        WebVRControllerButton[] buttons,
        float[] axes);
    public event ControllerUpdate OnControllerUpdate;

    public static WebVRManager Instance {
        get
        {
            if (instance == null)
            {
                var managerInScene = FindObjectOfType<WebVRManager>();
                var name = "WebVRManager";

                if (managerInScene != null)
                {
                    instance = managerInScene;
                    instance.name = name;
                }
                else
                {
                    GameObject go = new GameObject(name);
                    go.AddComponent<WebVRManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        Debug.Log("Active Graphics Tier: " + Graphics.activeTier);
        instance = this;
                
        if (instance.dontDestroyOnLoad)
        {
            DontDestroyOnLoad(instance);
        }
    }

    private void SetTrackingSpaceType()
    {
        if (UnityEngine.XR.XRDevice.isPresent)
        {
            UnityEngine.XR.XRDevice.SetTrackingSpaceType(WebVRManager.Instance.TrackingSpace);
            Debug.Log("Tracking Space: " + UnityEngine.XR.XRDevice.GetTrackingSpaceType());
        }
    }

    // Handles WebVR data from browser
    public void OnWebVRData (string jsonString)
    {
        WebVRData webVRData = WebVRData.CreateFromJSON (jsonString);

        // Reset RoomScale matrix if we are using Stationary tracking space.
        if (TrackingSpace == UnityEngine.XR.TrackingSpaceType.Stationary)
            sitStand = Matrix4x4.identity;

        // Update controllers
        if (webVRData.controllers.Length > 0)
        {
            foreach (WebVRControllerData controllerData in webVRData.controllers)
            {
                Vector3 position = new Vector3 (controllerData.position [0], controllerData.position [1], controllerData.position [2]);
                Quaternion rotation = new Quaternion (controllerData.orientation [0], controllerData.orientation [1], controllerData.orientation [2], controllerData.orientation [3]);

                if (OnControllerUpdate != null)
                    OnControllerUpdate(controllerData.index, controllerData.hand, position, rotation, sitStand, controllerData.buttons, controllerData.axes);
            }
        }
    }

    // Handles WebVR capabilities from browser
    public void OnVRCapabilities(string json) {
        OnVRCapabilities(JsonUtility.FromJson<WebVRDisplayCapabilities>(json));
    }

    public void OnVRCapabilities(WebVRDisplayCapabilities capabilities) {
        #if !UNITY_EDITOR && UNITY_WEBGL
        if (!capabilities.canPresent)
            WebVRUI.displayElementId("novr");
        #endif

        if (OnVRCapabilitiesUpdate != null)
            OnVRCapabilitiesUpdate(capabilities);
    }

    public void toggleVrState()
    {
        #if !UNITY_EDITOR && UNITY_WEBGL
        if (this.vrState == WebVRState.ENABLED)
            setVrState(WebVRState.NORMAL);
        else
            setVrState(WebVRState.ENABLED);
        #endif
    }

    public void setVrState(WebVRState state)
    {
        this.vrState = state;
        if (OnVRChange != null)
            OnVRChange(state);
    }

    // received start VR from WebVR browser
    public void OnStartVR()
    {
        Instance.setVrState(WebVRState.ENABLED);        
    }

    // receive end VR from WebVR browser
    public void OnEndVR()
    {
        Instance.setVrState(WebVRState.NORMAL);
    }

    // Toggles performance HUD
    public void TogglePerf()
    {
        showPerf = showPerf == false ? true : false;
    }

    // link WebGL plugin for interacting with browser scripts.
    [DllImport("__Internal")]
    private static extern void ConfigureToggleVRKeyName(string keyName);

    [DllImport("__Internal")]
    private static extern void InitSharedArray(float[] array, int length);

    [DllImport("__Internal")]
    private static extern void ListenWebVRData();

    // Shared array which we will load headset data in from webvr.jslib
    // Array stores  5 matrices, each 16 values, stored linearly.
    float[] sharedArray = new float[5 * 16];

    // show framerate UI
    private bool showPerf = false;

    private WebVRData webVRData;
    private Matrix4x4 sitStand = Matrix4x4.identity;

    // Data classes for WebVR data
    [System.Serializable]
    private class WebVRData
    {
        public float[] sitStand = null;
        public WebVRControllerData[] controllers = new WebVRControllerData[0];
        public static WebVRData CreateFromJSON(string jsonString)
        {
            return JsonUtility.FromJson<WebVRData> (jsonString);
        }
    }

    [System.Serializable]
    private class WebVRControllerData
    {
        public int index = 0;
        public string hand = null;
        public float[] orientation = null;
        public float[] position = null;
        public float[] axes = null;
        public WebVRControllerButton[] buttons = new WebVRControllerButton[0];
    }    

    void Start()
    {
        #if !UNITY_EDITOR && UNITY_WEBGL
        ConfigureToggleVRKeyName(toggleVRKeyName);
        InitSharedArray(sharedArray, sharedArray.Length);
        ListenWebVRData();
        #endif
        SetTrackingSpaceType();
    }

    float[] GetFromSharedArray(int index)
    {
        float[] newArray = new float[16];
        for (int i = 0; i < newArray.Length; i++) {
            newArray[i] = sharedArray[index * 16 + i];
        }
        return newArray;
    }

    void Update()
    {
        #if UNITY_EDITOR || !UNITY_WEBGL
        bool quickToggleEnabled = toggleVRKeyName != null && toggleVRKeyName != "";
        if (quickToggleEnabled && Input.GetKeyUp(toggleVRKeyName))
            toggleVrState();
        #endif

        if (OnHeadsetUpdate != null) {
            Matrix4x4 leftProjectionMatrix = numbersToMatrix(GetFromSharedArray(0));
            Matrix4x4 rightProjectionMatrix = numbersToMatrix(GetFromSharedArray(1));
            Matrix4x4 leftViewMatrix = numbersToMatrix(GetFromSharedArray(2));
            Matrix4x4 rightViewMatrix = numbersToMatrix(GetFromSharedArray(3));
            Matrix4x4 sitStandMatrix = numbersToMatrix(GetFromSharedArray(4));

            sitStand = sitStandMatrix;

            OnHeadsetUpdate(
                leftProjectionMatrix,
                rightProjectionMatrix,
                leftViewMatrix,
                rightViewMatrix,
                sitStand);
         }
    }

    // Utility functions
    private Matrix4x4 numbersToMatrix(float[] array)
    {
        var mat = new Matrix4x4 ();
        mat.m00 = array[0];
        mat.m01 = array[1];
        mat.m02 = array[2];
        mat.m03 = array[3];
        mat.m10 = array[4];
        mat.m11 = array[5];
        mat.m12 = array[6];
        mat.m13 = array[7];
        mat.m20 = array[8];
        mat.m21 = array[9];
        mat.m22 = array[10];
        mat.m23 = array[11];
        mat.m30 = array[12];
        mat.m31 = array[13];
        mat.m32 = array[14];
        mat.m33 = array[15];
        return mat;
    }
}
