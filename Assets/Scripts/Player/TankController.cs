using Mirror;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NetTank
{
    /// <summary>
    /// Server-authoritative tank movement. Clients send input only; the server moves and syncs state.
    /// </summary>
    [RequireComponent(typeof(TankHealth))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TankController : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float boostMoveSpeed = 9f;
        [SerializeField] private float turnSpeed = 150f;
        [SerializeField] private float inputSendInterval = 0.033f;
        [SerializeField] private float inputTimeout = 0.35f;

        [Header("Remote Smoothing")]
        [SerializeField] private float positionLerp = 14f;
        [SerializeField] private float rotationLerp = 16f;
        [SerializeField] private float snapDistance = 4f;

        [SyncVar] private Vector3 serverPosition;
        [SyncVar] private Quaternion serverRotation;

        private Rigidbody cachedRigidbody;
        private TankHealth tankHealth;
        private float nextInputSendTime;
        private float serverMoveInput;
        private float serverTurnInput;
        private float lastInputTime;
        private bool serverBoostInput;

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            tankHealth = GetComponent<TankHealth>();

            cachedRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            cachedRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            cachedRigidbody.isKinematic = false;
            serverPosition = transform.position;
            serverRotation = transform.rotation;
            lastInputTime = Time.time;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isServer)
            {
                cachedRigidbody.isKinematic = true;
            }
        }

        private void Update()
        {
            if (!isLocalPlayer)
            {
                return;
            }

            float moveInput = 0f;
            float turnInput = 0f;
            bool boostInput = false;

            if (tankHealth == null || !tankHealth.IsDead)
            {
                moveInput = ReadMoveInput();
                turnInput = ReadTurnInput();
                boostInput = ReadBoostInput();
            }

            if (Time.time >= nextInputSendTime)
            {
                nextInputSendTime = Time.time + inputSendInterval;
                CmdSetMoveInput(moveInput, turnInput, boostInput);
            }
        }

        private void FixedUpdate()
        {
            if (isServer)
            {
                ServerApplyMovement(Time.fixedDeltaTime);
            }
        }

        private void LateUpdate()
        {
            if (!isServer)
            {
                SmoothToServerState();
            }
        }

        [Command]
        private void CmdSetMoveInput(float moveInput, float turnInput, bool boostInput)
        {
            // 客户端只上传输入意图，移动结果由服务端计算，避免客户端直接篡改位置。
            serverMoveInput = Mathf.Clamp(moveInput, -1f, 1f);
            serverTurnInput = Mathf.Clamp(turnInput, -1f, 1f);
            serverBoostInput = boostInput;
            lastInputTime = Time.time;
        }

        [Server]
        public void ServerTeleport(Vector3 position, Quaternion rotation)
        {
            serverMoveInput = 0f;
            serverTurnInput = 0f;
            serverBoostInput = false;
            lastInputTime = Time.time;

            cachedRigidbody.linearVelocity = Vector3.zero;
            cachedRigidbody.angularVelocity = Vector3.zero;
            cachedRigidbody.position = position;
            cachedRigidbody.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);

            serverPosition = position;
            serverRotation = rotation;

            if (netId != 0)
            {
                RpcSnapTo(position, rotation);
            }
        }

        [ClientRpc]
        private void RpcSnapTo(Vector3 position, Quaternion rotation)
        {
            if (cachedRigidbody == null)
            {
                cachedRigidbody = GetComponent<Rigidbody>();
            }

            transform.SetPositionAndRotation(position, rotation);
            cachedRigidbody.position = position;
            cachedRigidbody.rotation = rotation;
        }

        [Server]
        private void ServerApplyMovement(float deltaTime)
        {
            if (tankHealth != null && tankHealth.IsDead)
            {
                serverPosition = transform.position;
                serverRotation = transform.rotation;
                return;
            }

            if (Time.time - lastInputTime > inputTimeout)
            {
                serverMoveInput = 0f;
                serverTurnInput = 0f;
                serverBoostInput = false;
            }

            float yaw = serverTurnInput * turnSpeed * deltaTime;
            Quaternion nextRotation = cachedRigidbody.rotation * Quaternion.Euler(0f, yaw, 0f);
            float speed = serverBoostInput ? boostMoveSpeed : moveSpeed;
            Vector3 nextPosition = cachedRigidbody.position + nextRotation * Vector3.forward * (serverMoveInput * speed * deltaTime);

            cachedRigidbody.MoveRotation(nextRotation);
            cachedRigidbody.MovePosition(nextPosition);

            serverPosition = nextPosition;
            serverRotation = nextRotation;
        }

        private void SmoothToServerState()
        {
            if (serverPosition == Vector3.zero && transform.position != Vector3.zero)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, serverPosition);
            if (distance > snapDistance)
            {
                transform.SetPositionAndRotation(serverPosition, serverRotation);
                cachedRigidbody.position = serverPosition;
                cachedRigidbody.rotation = serverRotation;
                return;
            }

            transform.position = Vector3.Lerp(transform.position, serverPosition, Time.deltaTime * positionLerp);
            transform.rotation = Quaternion.Slerp(transform.rotation, serverRotation, Time.deltaTime * rotationLerp);
        }

        private float ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float value = 0f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    value += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    value -= 1f;
                }

                return Mathf.Clamp(value, -1f, 1f);
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Vertical");
#else
            return 0f;
#endif
        }

        private float ReadTurnInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float value = 0f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    value += 1f;
                }

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    value -= 1f;
                }

                return Mathf.Clamp(value, -1f, 1f);
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Horizontal");
#else
            return 0f;
#endif
        }

        private bool ReadBoostInput()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }
    }
}
