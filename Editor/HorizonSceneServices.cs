using UnityEngine;
using UnityEditor;
using VRC.SDK3.Components;
using BlackHorizon.HorizonGUI.Services;

namespace BlackHorizon.HorizonGUI.Editor
{
    /// <summary>
    /// Manages scene-level infrastructure requirements (EventSystem, Avatar Photobooth Service) for Horizon GUI.
    /// </summary>
    public static class HorizonSceneServices
    {
        /// <summary>
        /// Ensures an active EventSystem exists in the scene hierarchy, preferring InputSystemUIInputModule with Standalone fallback.
        /// </summary>
        /// <param name="parent">Parent GameObject to attach the EventSystem to if creation is required.</param>
        public static void EnsureEventSystemInside(GameObject parent)
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;

            GameObject esObj = new GameObject("System_Input");
            esObj.transform.SetParent(parent.transform);
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();

            System.Type newModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (newModuleType != null)
            {
                esObj.AddComponent(newModuleType);
                Debug.Log("<color=#33FF33>[HorizonSceneServices]</color> Created System_Input using modern <b>InputSystemUIInputModule</b>.");
            }
            else
            {
                Debug.LogWarning("<color=yellow>[HorizonSceneServices]</color> Modern Input System not found. Falling back to <b>StandaloneInputModule</b>.");
                var input = esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                input.horizontalAxis = "Horizontal";
                input.verticalAxis = "Vertical";
            }
        }

        /// <summary>
        /// Locates or procedurally instantiates the HorizonAvatarManager service and its dedicated capture camera.
        /// </summary>
        /// <param name="systemRoot">Root GameObject of the Horizon UI System.</param>
        /// <returns>The attached HorizonAvatarManager component.</returns>
        public static HorizonAvatarManager EnsureAvatarService(GameObject systemRoot)
        {
            const string serviceName = "Service_AvatarManager";
            Transform existingTr = systemRoot.transform.Find(serviceName);

            HorizonAvatarManager manager = null;
            GameObject serviceObj = null;

            if (existingTr != null)
            {
                serviceObj = existingTr.gameObject;
                manager = serviceObj.GetComponent<HorizonAvatarManager>();
                if (manager == null) manager = HorizonGUIFactory.AttachLogic<HorizonAvatarManager>(serviceObj);
            }
            else
            {
                serviceObj = new GameObject(serviceName);
                serviceObj.transform.SetParent(systemRoot.transform, false);
                manager = HorizonGUIFactory.AttachLogic<HorizonAvatarManager>(serviceObj);
                Undo.RegisterCreatedObjectUndo(serviceObj, "Create Avatar Service");
            }

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty camProp = so.FindProperty("photoCamera");

            Camera cam = null;
            if (camProp != null && camProp.objectReferenceValue != null)
            {
                cam = (Camera)camProp.objectReferenceValue;
            }

            if (cam == null)
            {
                Transform camTr = serviceObj.transform.Find("Avatar_Photobooth_Camera");
                if (camTr != null)
                {
                    cam = camTr.GetComponent<Camera>();
                    if (cam == null) cam = camTr.gameObject.AddComponent<Camera>();
                }
                else
                {
                    GameObject camObj = new GameObject("Avatar_Photobooth_Camera");
                    camObj.transform.SetParent(serviceObj.transform, false);
                    cam = camObj.AddComponent<Camera>();
                    Undo.RegisterCreatedObjectUndo(camObj, "Create Photobooth Camera");
                }

                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 50f;

                int avatarMask = 0;
                if (LayerMask.NameToLayer("Player") != -1) avatarMask |= (1 << LayerMask.NameToLayer("Player"));
                if (LayerMask.NameToLayer("PlayerLocal") != -1) avatarMask |= (1 << LayerMask.NameToLayer("PlayerLocal"));
                if (LayerMask.NameToLayer("MirrorReflection") != -1) avatarMask |= (1 << LayerMask.NameToLayer("MirrorReflection"));

                if (avatarMask == 0) avatarMask = (1 << 9) | (1 << 10) | (1 << 18);

                cam.cullingMask = avatarMask;
            }

            HorizonGUIFactory.ConfigureLogic<HorizonAvatarManager>(serviceObj, b =>
            {
                b.Bind("photoCamera", cam);
                b.BindVal("avatarOnlyLayers", cam.cullingMask);
                b.BindVal("fullEnvironmentLayers", -1);
                b.BindVal("poolSize", 16);
                b.BindVal("resolution", 256);
            });

            return manager;
        }
    }
}