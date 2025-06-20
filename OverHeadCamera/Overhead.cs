using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OverHeadCamera;

internal class Overhead
{
    // State variables
    private static bool initialized = false;
    private static bool overHeadCamToggle = false;
    
    // Components
    private static Player localPlayer;
    private static Camera playerCam;
    private static Camera overHeadCamera;
    private static GameObject overHeadCameraGameObject;
    private static bool keyWasPressed = false;

    // Set a unique prefix for your plugin
    private const string PluginPrefix = "OHC_";
    // Generate the instance ID with a prefix
    private static string instanceId = GenerateInstanceId();
    private static string GenerateInstanceId()
    {
        int randomNumber = UnityEngine.Random.Range(10000, 99999);
        return PluginPrefix + randomNumber.ToString();
    }

    // Initializes the custom camera for goalie view
    private static void InitializeOverheadCamera(PlayerTeam team)
    {
        try
        {
            Plugin.Log($"{instanceId} Initializing overhead camera for team: " + team.ToString());

            // Calculate camera position based on team
            float camRotation = team == PlayerTeam.Blue ? -180f : 0f;

            // Create camera if it doesn't exist
            EnsureOverHeadCameraExists();

            // Position and configure the camera
            overHeadCamera.transform.position = new Vector3(0, Plugin.modSettings.CameraHeight, 0);
            overHeadCamera.transform.rotation = Quaternion.Euler(Plugin.modSettings.CameraAngle, camRotation, 0);
            overHeadCamera.fieldOfView = Plugin.modSettings.CameraFOV;

            Plugin.Log($"{instanceId} Overhead camera initialized successfully");
            initialized = true;
        }
        catch (Exception e)
        {
            Plugin.LogError($"{instanceId} Failed to initialize overhead camera: " + e.Message);
            ResetCameraSystem();
        }
    }

    // Ensures the overhead camera exists, creating it if necessary
    private static void EnsureOverHeadCameraExists()
    {
        if (overHeadCamera == null || overHeadCameraGameObject == null)
        {
            Plugin.Log($"{instanceId} Creating new overhead camera");
            overHeadCameraGameObject = new GameObject("overHeadCamera");
            overHeadCamera = overHeadCameraGameObject.AddComponent<Camera>();
            overHeadCamera.enabled = overHeadCamToggle;
        }
    }

    // Initialize player reference if not already set
    private static void EnsurePlayerReference()
    {
        if (Plugin.playerManager == null)
        {
            Plugin.playerManager = NetworkBehaviourSingleton<PlayerManager>.Instance;
        }
        if (localPlayer == null && Plugin.playerManager != null)
        {
            localPlayer = Plugin.playerManager.GetLocalPlayer();
            Plugin.Log($"{instanceId} Local player reference established: {(localPlayer != null ? "success" : "failed")}");
            // Subscribe to team changed event if available
            if (localPlayer != null)
            {
                OverheadGameEvents.SubscribeToTeamChanged(localPlayer, HandleTeamChanged);
            }
        }
    }
    
    // Ensures the player camera reference is set
    private static void EnsurePlayerCameraExists()
    {
        if (playerCam == null && localPlayer != null)
        {
            Plugin.Log($"{instanceId} Getting camera from player object");
            playerCam = localPlayer.PlayerCamera.CameraComponent;
        }
    }

    // Handles input for camera toggling with improved detection
    private static void HandleCameraToggleInput()
    {
        // prevent triggering keybind while chat is open
        UIChat chat = NetworkBehaviourSingleton<UIChat>.Instance;
        if (chat.IsFocused) return;

        // Manage Keypress to trigger only once, when key is first pressed down (rising edge)
        bool isKeyPressed = Keyboard.current[Plugin.modSettings.CameraToggleKey].isPressed;
        if (isKeyPressed && !keyWasPressed)
        {
            // Toggle the camera state (true -> false, false -> true)
            overHeadCamToggle = !overHeadCamToggle;
        }
        if (Keyboard.current[Plugin.modSettings.CameraResetKey].wasPressedThisFrame) // Provide a manual camera reset button, camera breaks sometimes
        {
            ResetCameraSystem();
        }
        keyWasPressed = isKeyPressed;
    }

    // Updates camera enabled states based on toggle
    private static void UpdateCameraStates()
    {
        if (overHeadCamera != null && playerCam != null)
        {
            // Only update if the state actually changes
            if (overHeadCamera.enabled != overHeadCamToggle)
            {
                overHeadCamera.enabled = overHeadCamToggle; // Enable/Disable cameras based on overHeadCamToggle
                playerCam.enabled = !overHeadCamToggle;

                if (overHeadCamera.enabled) // Show/hide mesh renderers based on camera state
                {
                    localPlayer.PlayerBody.MeshRendererHider.ShowMeshRenderers(); // Show the player's mesh renderers (your body)
                }
                else if (playerCam.enabled)
                {
                    localPlayer.PlayerBody.MeshRendererHider.HideMeshRenderers(); // Hide the player's mesh renderers (your body)
                }
                else
                {
                    Plugin.Log("neither cam is active?");
                    ResetCameraSystem();
                }
                Plugin.Log($"{instanceId} Camera states updated: overHead={overHeadCamToggle}, Player={!overHeadCamToggle}");
            }
        }
    }

    // Resets the entire camera system
    private static void ResetCameraSystem()
    {
        playerCam = null;

        if (overHeadCameraGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(overHeadCameraGameObject);
        }
        overHeadCamera = null;
        overHeadCameraGameObject = null;
        overHeadCamToggle = false;
        initialized = false;
        Plugin.Log($"{instanceId} Camera system has been reset");
    }

    // Handles events when a player changes teams
    private static void HandleTeamChanged(Player player, PlayerTeam oldTeam, PlayerTeam newTeam)
    {
        // Check if this is our local player
        if (localPlayer != null && player == localPlayer)
        {
            Plugin.Log($"{instanceId} Local player team changed from {oldTeam} to {newTeam}");
            ResetCameraSystem();
        }
    }

    private static void TrackPuckZ(PlayerTeam team)
    {
        var puck = Plugin.puckManager.GetPuck(); // Get the puck instance
        if (puck != null)
        {
            float puckPositionZ = puck.transform.position.z; // Get Z of puck
            float cameraPositionZ = overHeadCamera.transform.position.z; // Get Z of camera
            
            if (team == PlayerTeam.Blue) // Calculations for blue team
            {
                float targetZ = cameraPositionZ - Plugin.modSettings.CameraOffset; // Target Z position for the camera
                float newZ;

                // Conditionals to determine if the puck is outside the buffer zone, if so, move the camera towards the puck based on the buffer zone size, clamped to the home/away max distance
                if (puckPositionZ < targetZ - Plugin.modSettings.BufferZoneSize)
                {
                    newZ = Mathf.Clamp(puckPositionZ + Plugin.modSettings.CameraOffset + Plugin.modSettings.BufferZoneSize, -Plugin.modSettings.AwayMaxDistance, Plugin.modSettings.HomeMaxDistance);
                }
                else if (puckPositionZ > targetZ + Plugin.modSettings.BufferZoneSize)
                {
                    newZ = Mathf.Clamp(puckPositionZ + Plugin.modSettings.CameraOffset - Plugin.modSettings.BufferZoneSize, -Plugin.modSettings.AwayMaxDistance, Plugin.modSettings.HomeMaxDistance);
                }
                else
                {
                    newZ = cameraPositionZ; // Puck is within the buffer, no change needed
                }
                // Calculate and set the new camera position
                var positionToSet = new Vector3(0, Plugin.modSettings.CameraHeight, newZ);
                overHeadCamera.transform.SetPositionAndRotation(positionToSet, overHeadCameraGameObject.transform.rotation);
            }
            else if (team == PlayerTeam.Red) // Calculations for red team
            {
                float targetZ = cameraPositionZ + Plugin.modSettings.CameraOffset; // Target Z position for the camera
                float newZ;

                // Conditionals to determine if the puck is outside the buffer zone, if so, move the camera towards the puck based on the buffer zone size, clamped to the home/away max distance
                if (puckPositionZ > targetZ + Plugin.modSettings.BufferZoneSize)
                {
                    newZ = Mathf.Clamp(puckPositionZ - Plugin.modSettings.CameraOffset - Plugin.modSettings.BufferZoneSize, -Plugin.modSettings.HomeMaxDistance, Plugin.modSettings.AwayMaxDistance);
                }
                else if (puckPositionZ < targetZ - Plugin.modSettings.BufferZoneSize)
                {
                    newZ = Mathf.Clamp(-Plugin.modSettings.HomeMaxDistance, puckPositionZ - Plugin.modSettings.CameraOffset + Plugin.modSettings.BufferZoneSize, Plugin.modSettings.AwayMaxDistance);
                }
                else
                {
                    newZ = cameraPositionZ; // Puck is within the buffer, no change needed
                }

                // Calculate and set the new camera position
                var positionToSet = new Vector3(0, Plugin.modSettings.CameraHeight, newZ);
                overHeadCamera.transform.SetPositionAndRotation(positionToSet, overHeadCameraGameObject.transform.rotation);
            }
           
        }
    }
    
    // Harmony patch to intercept camera tick
    [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.OnTick))]
    private class PatchPlayerCameraOnTick
    {
        private static void Postfix(PlayerCamera __instance)
        {
            try
            {
                // Ensure we have player and manager references
                EnsurePlayerReference();

                // Skip processing if we're not handling the local player
                if (localPlayer == null) return;

                // Additional check to make sure we're only working with local player's camera
                if (__instance != localPlayer.PlayerCamera) return;

                // Ensure cameras exist
                EnsurePlayerCameraExists();
                EnsureOverHeadCameraExists();

                // Check for input to toggle camera
                HandleCameraToggleInput();

                // Initialize camera if not already done
                if (!initialized)
                {
                    InitializeOverheadCamera(localPlayer.Team.Value);
                }

                try
                {
                    TrackPuckZ(localPlayer.Team.Value);
                }
                catch (Exception e)
                {
                    Plugin.LogError($"oopsies trackpuckZ no work{e.Message}");
                }
                // Update camera enabled states
                UpdateCameraStates();
            }
            catch (Exception e)
            {
                Plugin.Log($"{instanceId} Error in camera tick: " + e.Message);
                ResetCameraSystem();
            }
        }
    }
}
