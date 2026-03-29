using UnityEngine;
using VRC.SDKBase;
using UdonSharp;

namespace BlackHorizon.HorizonGUI.Services
{
    /// <summary>
    /// Service that renders players into RenderTextures on demand.
    /// </summary>
    public class HorizonAvatarManager : UdonSharpBehaviour
    {
        public Camera photoCamera;

        [Tooltip("Layers for transparent background. Usually 9 (Player) and 18 (MirrorReflection).")]
        public LayerMask avatarOnlyLayers;

        [Tooltip("Layers for full environment background.")]
        public LayerMask fullEnvironmentLayers;

        public int poolSize = 16;
        public int resolution = 256;

        [Tooltip("Seconds between renders for a single slot.")]
        public float updateInterval = 20f;

        [Header("Face Targeting")]
        [Tooltip("Vertical offset from Neck bone in meters. "
            + "Neck = base of skull. Face center is slightly above. "
            + "Positive = aim higher, negative = lower. Start at 0.03.")]
        [Range(-0.15f, 0.15f)]
        public float neckToFaceOffset = 0.03f; [Header("Camera")]
        [Tooltip("Distance multiplier relative to neck height. Lower = closer to face.")]
        [Range(0.25f, 0.80f)]
        public float cameraDistanceMult = 0.40f;

        [Tooltip("Camera field of view.")]
        [Range(35f, 75f)]
        public float cameraFOV = 60f;

        [Header("Debug")]
        public bool debugLog = false;

        private RenderTexture[] _texturePool;
        private int[] _slotOwnerIds;
        private bool[] _slotTransparent;
        private float[] _lastRenderTime;
        private bool _initialized = false;

        // --- Render State Machine ---
        private bool _isWaitingForRender = false;
        private int _renderWaitFrames = 0;

        public void OnShow() { }
        public void OnHide() { }
        public void OnHorizonBuild() { }

        private void Start() { EnsureInitialized(); }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            if (photoCamera != null)
                photoCamera.enabled = false;

            // Default layers: Player (9), PlayerLocal (10), MirrorReflection (18)
            if (avatarOnlyLayers.value == 0)
                avatarOnlyLayers = (1 << 9) | (1 << 10) | (1 << 18);

            if (fullEnvironmentLayers.value == 0)
                fullEnvironmentLayers = -1;

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
                    _texturePool[i].Release();
            }
        }

        public void RegisterRequest(int slotIndex, int playerId, bool transparentBackground)
        {
            EnsureInitialized();
            if (slotIndex < 0 || slotIndex >= poolSize) return;

            if (_slotOwnerIds[slotIndex] != playerId)
                _lastRenderTime[slotIndex] = -updateInterval;

            _slotOwnerIds[slotIndex] = playerId;
            _slotTransparent[slotIndex] = transparentBackground;
        }

        public void ClearRequest(int slotIndex)
        {
            EnsureInitialized();
            if (slotIndex >= 0 && slotIndex < poolSize)
                _slotOwnerIds[slotIndex] = -1;
        }

        public RenderTexture GetTexture(int slotIndex)
        {
            EnsureInitialized();
            if (slotIndex < 0 || slotIndex >= poolSize) return null;
            return _texturePool[slotIndex];
        }

        // ==========================================================
        // MAIN LOOP
        // ==========================================================

        public override void PostLateUpdate()
        {
            if (photoCamera == null) return;
            EnsureInitialized();

            // 1. Await Unity's native render cycle.
            // Manual camera.Render() is bad :( 
            // VRChat requires the native OnPreCull event to update the IK of 
            // the Layer 18 clone. Without this delay, the clone is photographed 
            // in a lagged/default pose, causing the camera to miss the face.
            if (_isWaitingForRender)
            {
                _renderWaitFrames++;
                if (_renderWaitFrames > 1)
                {
                    photoCamera.enabled = false;
                    photoCamera.targetTexture = null;
                    _isWaitingForRender = false;
                }
                return;
            }

            // 2. Find the next avatar in the queue
            for (int i = 0; i < poolSize; i++)
            {
                int targetId = _slotOwnerIds[i];
                if (targetId == -1) continue;
                if (Time.time - _lastRenderTime[i] < updateInterval) continue;

                VRCPlayerApi player = VRCPlayerApi.GetPlayerById(targetId);

#if UNITY_EDITOR
                if (!Utilities.IsValid(player))
                {
                    SetupBlankCamera();
                    ExecuteRenderCycle(i);
                    return;
                }
#endif

                if (!Utilities.IsValid(player)) continue;

                // 3. Setup camera position and layers
                if (!SetupCameraForPlayer(i, player))
                {
                    SetupBlankCamera();
                }

                // 4. Trigger the native render cycle
                ExecuteRenderCycle(i);
                return;
            }
        }

        /// <summary>
        /// Activates the camera and delegates rendering to Unity's native pipeline.
        /// </summary>
        private void ExecuteRenderCycle(int slotIndex)
        {
            photoCamera.targetTexture = _texturePool[slotIndex];
            photoCamera.enabled = true;

            _isWaitingForRender = true;
            _renderWaitFrames = 0;
            _lastRenderTime[slotIndex] = Time.time;
        }

        // ==========================================================
        // CAMERA SETUP & POSITIONING
        // ==========================================================

        private bool SetupCameraForPlayer(int index, VRCPlayerApi player)
        {
            // --- 1. Layer Culling Setup ---
            if (_slotTransparent[index])
            {
                photoCamera.clearFlags = CameraClearFlags.SolidColor;
                photoCamera.backgroundColor = new Color(0, 0, 0, 0);
                photoCamera.cullingMask = player.isLocal ? (1 << 18) : (1 << 9);
            }
            else
            {
                photoCamera.clearFlags = CameraClearFlags.Skybox;
                int mask = fullEnvironmentLayers;
                mask &= ~(1 << 10);

                if (player.isLocal)
                {
                    mask &= ~(1 << 9);
                    mask |= (1 << 18);
                }
                else
                {
                    mask &= ~(1 << 18);
                    mask |= (1 << 9);
                }
                photoCamera.cullingMask = mask;
            }

            // --- 2. Positioning ---
            Vector3 facePos;
            float neckH;
            GetFaceTarget(player, out facePos, out neckH);

            if (facePos.sqrMagnitude < 0.01f) return false;

            Vector3 forward = GetFacingDirection(player);
            float dist = Mathf.Clamp(neckH * cameraDistanceMult, 0.12f, 0.60f);
            Vector3 camPos = facePos + forward * dist;

            photoCamera.transform.position = camPos;
            photoCamera.transform.LookAt(facePos);
            photoCamera.nearClipPlane = 0.01f;
            photoCamera.fieldOfView = cameraFOV;

            return true;
        }

        private void SetupBlankCamera()
        {
            photoCamera.clearFlags = CameraClearFlags.SolidColor;
            photoCamera.backgroundColor = new Color(0, 0, 0, 0);
        }

        // ==========================================================
        // FACE TARGETING
        // ==========================================================

        /// <summary>
        /// Calculates the ideal camera target based on the Neck bone.
        /// Head bone is avoided because it represents the top of the skull and is heavily 
        /// skewed by avatar accessories (hats, ears, hair). Neck provides a stable baseline.
        /// Just for now. Maybe in future need more testings.
        /// </summary>
        private void GetFaceTarget(VRCPlayerApi player, out Vector3 target, out float neckHeight)
        {
            Vector3 feet = player.GetPosition();

            // === PRIMARY: Neck bone ===
            Vector3 neck = player.GetBonePosition(HumanBodyBones.Neck);

            if (neck.sqrMagnitude > 0.01f && neck.y > feet.y + 0.05f)
            {
                neckHeight = neck.y - feet.y;
                target = new Vector3(neck.x, neck.y + neckToFaceOffset, neck.z);

                if (debugLog)
                {
                    Debug.Log($"[AvatarCam] NECK-TARGET name={player.displayName} isLocal={player.isLocal} "
                        + $"| neck.y={neck.y:F3} (+{neckHeight:F3}) | TARGET.y={target.y:F3}");
                }
                return;
            }

            // === FALLBACK: Chest ===
            Vector3 chest = player.GetBonePosition(HumanBodyBones.UpperChest);
            if (chest.sqrMagnitude < 0.01f) chest = player.GetBonePosition(HumanBodyBones.Chest);

            if (chest.sqrMagnitude > 0.01f && chest.y > feet.y + 0.05f)
            {
                float chestH = chest.y - feet.y;
                neckHeight = chestH * 1.12f;
                target = new Vector3(chest.x, chest.y + chestH * 0.12f + neckToFaceOffset, chest.z);
                return;
            }

            // === LAST RESORT ===
            neckHeight = 0.6f;
            target = feet + Vector3.up * 0.6f;
        }

        private Vector3 GetFacingDirection(VRCPlayerApi player)
        {
            Vector3 fwd = player.GetRotation() * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            return fwd.normalized;
        }
    }
}