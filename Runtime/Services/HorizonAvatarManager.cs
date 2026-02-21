using UnityEngine;
using VRC.SDKBase;
using UdonSharp;

namespace BlackHorizon.HorizonGUI.Services
{
    /// <summary>
    /// Service that renders players into RenderTextures on demand.
    /// Implements a lazy-loading "Photobooth" pattern with time-slicing to guarantee zero FPS drops.
    /// </summary>
    public class HorizonAvatarManager : UdonSharpBehaviour
    {
        public Camera photoCamera;

        [Tooltip("Layers to render when background is transparent (usually Player, PlayerLocal, MirrorReflection).")]
        public LayerMask avatarOnlyLayers;

        [Tooltip("Layers to render when background is visible (usually Everything).")]
        public LayerMask fullEnvironmentLayers;

        public int poolSize = 16;
        public int resolution = 256;

        [Tooltip("Minimum time in seconds between renders for a single slot.")]
        public float updateInterval = 20f;

        // --- Runtime State ---
        private RenderTexture[] _texturePool;
        private int[] _slotOwnerIds;
        private bool[] _slotTransparent;
        private float[] _lastRenderTime;

        private void Start()
        {
            InitializePool();
        }

        /// <summary>
        /// Pre-allocates RenderTextures and prepares the tracking arrays.
        /// </summary>
        private void InitializePool()
        {
            if (photoCamera != null)
            {
                photoCamera.enabled = false;
            }

            if (avatarOnlyLayers.value == 0)
            {
                avatarOnlyLayers = (1 << 9) | (1 << 10) | (1 << 18);
            }
            if (fullEnvironmentLayers.value == 0)
            {
                fullEnvironmentLayers = -1;
            }

            _texturePool = new RenderTexture[poolSize];
            _slotOwnerIds = new int[poolSize];
            _slotTransparent = new bool[poolSize];
            _lastRenderTime = new float[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                _texturePool[i] = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32);
                _texturePool[i].Create();
                _slotOwnerIds[i] = -1;
                _lastRenderTime[i] = -updateInterval;
            }
        }

        private void OnDestroy()
        {
            if (_texturePool == null) return;

            for (int i = 0; i < _texturePool.Length; i++)
            {
                if (_texturePool[i] != null)
                {
                    _texturePool[i].Release();
                }
            }
        }

        /// <summary>
        /// Registers a slot to continuously track and render a specific player.
        /// </summary>
        /// <param name="slotIndex">The physical index in the texture pool.</param>
        /// <param name="playerId">The VRChat Player ID to render.</param>
        /// <param name="transparentBackground">If true, clears the background with zero alpha.</param>
        public void RegisterRequest(int slotIndex, int playerId, bool transparentBackground)
        {
            if (slotIndex < 0 || slotIndex >= poolSize) return;

            if (_slotOwnerIds[slotIndex] != playerId)
            {
                _lastRenderTime[slotIndex] = -updateInterval;
            }

            _slotOwnerIds[slotIndex] = playerId;
            _slotTransparent[slotIndex] = transparentBackground;
        }

        /// <summary>
        /// Frees the slot so it stops updating.
        /// </summary>
        public void ClearRequest(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < poolSize)
            {
                _slotOwnerIds[slotIndex] = -1;
            }
        }

        /// <summary>
        /// Returns the assigned RenderTexture for a specific slot.
        /// </summary>
        public RenderTexture GetTexture(int slotIndex)
        {
            if (_texturePool == null)
            {
                InitializePool();
            }

            if (slotIndex < 0 || slotIndex >= poolSize) return null;
            return _texturePool[slotIndex];
        }

        public override void PostLateUpdate()
        {
            if (photoCamera == null) return;

            for (int i = 0; i < poolSize; i++)
            {
                int targetId = _slotOwnerIds[i];
                if (targetId == -1) continue;

                if (Time.time - _lastRenderTime[i] < updateInterval) continue;

                VRCPlayerApi targetPlayer = VRCPlayerApi.GetPlayerById(targetId);

#if UNITY_EDITOR
                if (!Utilities.IsValid(targetPlayer))
                {
                    photoCamera.clearFlags = CameraClearFlags.SolidColor;
                    photoCamera.backgroundColor = new Color(0, 0, 0, 0); 
                    photoCamera.targetTexture = _texturePool[i];
                    photoCamera.Render();
                    photoCamera.targetTexture = null;
                    
                    _lastRenderTime[i] = Time.time;
                    return; 
                }
#endif

                if (!Utilities.IsValid(targetPlayer)) continue;

                RenderSlot(i, targetPlayer);
                _lastRenderTime[i] = Time.time;

                return;
            }
        }

        /// <summary>
        /// Positions the camera in front of the player's face and takes a snapshot.
        /// </summary>
        private void RenderSlot(int index, VRCPlayerApi player)
        {
            if (_slotTransparent[index])
            {
                photoCamera.clearFlags = CameraClearFlags.SolidColor;
                photoCamera.backgroundColor = new Color(0, 0, 0, 0);

                if (player.isLocal)
                {
                    photoCamera.cullingMask = 1 << 18;
                }
                else
                {
                    photoCamera.cullingMask = 1 << 9;
                }
            }
            else
            {
                photoCamera.clearFlags = CameraClearFlags.Skybox;
                int mask = fullEnvironmentLayers;

                if (player.isLocal)
                {
                    mask &= ~(1 << 10);
                    mask |= (1 << 18);
                }
                else
                {
                    mask &= ~(1 << 18);
                    mask |= (1 << 9);
                }

                photoCamera.cullingMask = mask;
            }

            VRCPlayerApi.TrackingData headData = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 viewPos = headData.position;
            Quaternion headRot = headData.rotation;

            Vector3 bonePos = player.GetBonePosition(HumanBodyBones.Head);
            Vector3 targetFacePos;

            if (bonePos == Vector3.zero)
            {
                targetFacePos = viewPos;
            }
            else
            {
                targetFacePos = Vector3.Lerp(bonePos, viewPos, 0.5f);
            }

            if (targetFacePos == Vector3.zero)
            {
                photoCamera.clearFlags = CameraClearFlags.SolidColor;
                photoCamera.backgroundColor = new Color(0, 0, 0, 0);
                photoCamera.targetTexture = _texturePool[index];

                if (photoCamera != null) photoCamera.Render();

                photoCamera.targetTexture = null;
                return;
            }

            float avatarHeight = Vector3.Distance(player.GetPosition(), viewPos);

            if (avatarHeight < 0.2f) avatarHeight = 1.6f;

            float cameraDistance = avatarHeight * 0.35f;

            Vector3 offset = headRot * Vector3.forward * cameraDistance;
            Vector3 cameraPos = targetFacePos + offset;

            photoCamera.transform.position = cameraPos;
            photoCamera.transform.LookAt(targetFacePos);
            photoCamera.nearClipPlane = 0.01f;

            photoCamera.targetTexture = _texturePool[index];
            if (photoCamera != null) photoCamera.Render();
            photoCamera.targetTexture = null;
        }
    }
}