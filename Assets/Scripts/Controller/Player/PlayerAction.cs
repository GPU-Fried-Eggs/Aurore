//------------------------------------------------------------------------------
// <auto-generated>
//     This code was auto-generated by com.unity.inputsystem:InputActionCodeGenerator
//     version 1.7.0
//     from Assets/Settings/Inputs/InputActions.inputactions
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Player
{
    public partial class @PlayerInputActions: IInputActionCollection2, IDisposable
    {
        public InputActionAsset asset { get; }
        public @PlayerInputActions()
        {
            asset = InputActionAsset.FromJson(@"{
    ""name"": ""InputActions"",
    ""maps"": [
        {
            ""name"": ""GameplayMap"",
            ""id"": ""101c6ad4-7d15-4b7a-ac81-d6b3ec336f50"",
            ""actions"": [
                {
                    ""name"": ""Move"",
                    ""type"": ""Value"",
                    ""id"": ""b8a40adb-767e-4278-a836-8e63a2e33e7d"",
                    ""expectedControlType"": """",
                    ""processors"": ""Clamp(max=1)"",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""LookDelta"",
                    ""type"": ""Value"",
                    ""id"": ""d1d46c89-8319-4def-971c-40bcf91dfd59"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""LookConst"",
                    ""type"": ""Value"",
                    ""id"": ""1f6d2c47-3049-40a4-9c4f-a892d3c1a579"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""CameraZoom"",
                    ""type"": ""Value"",
                    ""id"": ""70616633-6b7e-4129-bc71-744ce408cef7"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""Jump"",
                    ""type"": ""Button"",
                    ""id"": ""0394a962-f12c-4dc0-b829-31a1882f248e"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Crouch"",
                    ""type"": ""Button"",
                    ""id"": ""b9dbfacc-cee0-4337-96da-134f449a21c1"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Sprint"",
                    ""type"": ""Button"",
                    ""id"": ""85835435-e7f2-44da-ad49-bdea22fc556e"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""GodMode"",
                    ""type"": ""Button"",
                    ""id"": ""9ce57d0b-67e9-48cd-9608-0cc9b7920b52"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""Arrows"",
                    ""id"": ""2306c4b2-4cc0-419d-8612-be9e02cfa8b3"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""9c1eab46-7a65-486b-bf41-20e7f27d41da"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""725b118e-dbea-40e6-80cf-86e1cccfbe20"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""62850440-bc12-4abc-8641-f1efcef47a53"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""d36846bf-19ec-4eef-bb9e-1fb4a1219b77"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""5527946b-be43-4218-be81-a1810c3e4167"",
                    ""path"": ""<Gamepad>/leftStick"",
                    ""interactions"": """",
                    ""processors"": ""StickDeadzone"",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e2d544b6-c44a-4d7a-b0d4-f110d79077e4"",
                    ""path"": ""<Mouse>/delta"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""LookDelta"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""bac76e61-7524-4e1a-a9c3-bd4c4d89f796"",
                    ""path"": ""<Gamepad>/rightStick"",
                    ""interactions"": """",
                    ""processors"": ""StickDeadzone,ScaleVector2(x=70,y=70)"",
                    ""groups"": """",
                    ""action"": ""LookConst"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""55c0d206-a1f2-494c-837d-e52916f0e31c"",
                    ""path"": ""<Mouse>/scroll/y"",
                    ""interactions"": """",
                    ""processors"": ""Scale(factor=0.1),Invert"",
                    ""groups"": """",
                    ""action"": ""CameraZoom"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""45d113a7-6c6d-40a5-b081-4b2a2ef18a09"",
                    ""path"": ""<Gamepad>/dpad/y"",
                    ""interactions"": """",
                    ""processors"": ""Invert"",
                    ""groups"": """",
                    ""action"": ""CameraZoom"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e85458a2-da23-46ce-997d-b4380cad12a3"",
                    ""path"": ""<Keyboard>/space"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Jump"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""8ee0f35c-5700-49a7-90fb-a4b9e449148d"",
                    ""path"": ""<Gamepad>/buttonSouth"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Jump"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b8857ebe-a3d8-4ac2-8ecc-6d9ff086edbf"",
                    ""path"": ""<Keyboard>/leftCtrl"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Crouch"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""5d41cf27-098e-4f66-a120-1249cb9082f8"",
                    ""path"": ""<Gamepad>/buttonWest"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Crouch"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e3066a2d-6e6e-4508-b5ca-5df6c004d92e"",
                    ""path"": ""<Keyboard>/leftShift"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Sprint"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""db8f58c8-d84d-423b-8608-58639a692b4d"",
                    ""path"": ""<Gamepad>/rightTrigger"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Sprint"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""87e0a538-1625-4815-bfb4-2f2efa56acdb"",
                    ""path"": ""<Keyboard>/z"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""GodMode"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""bc3536d6-3354-4d1f-867c-1a5481e9a731"",
                    ""path"": ""<Gamepad>/select"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""GodMode"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        },
        {
            ""name"": ""MenuMap"",
            ""id"": ""08828243-0fa1-4364-8eef-e9e3bda16037"",
            ""actions"": [
                {
                    ""name"": ""ToggleMenu"",
                    ""type"": ""Button"",
                    ""id"": ""894eb2f0-bd71-4011-bca1-281b6cb48942"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""8743d8b3-15c3-4ffe-8f98-ee25a3bc88f2"",
                    ""path"": ""<Keyboard>/escape"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ToggleMenu"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""81d1c84d-43fd-4089-9526-c5a6569ebaad"",
                    ""path"": ""<Gamepad>/start"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""ToggleMenu"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
            // GameplayMap
            m_GameplayMap = asset.FindActionMap("GameplayMap", throwIfNotFound: true);
            m_GameplayMap_Move = m_GameplayMap.FindAction("Move", throwIfNotFound: true);
            m_GameplayMap_LookDelta = m_GameplayMap.FindAction("LookDelta", throwIfNotFound: true);
            m_GameplayMap_LookConst = m_GameplayMap.FindAction("LookConst", throwIfNotFound: true);
            m_GameplayMap_CameraZoom = m_GameplayMap.FindAction("CameraZoom", throwIfNotFound: true);
            m_GameplayMap_Jump = m_GameplayMap.FindAction("Jump", throwIfNotFound: true);
            m_GameplayMap_Crouch = m_GameplayMap.FindAction("Crouch", throwIfNotFound: true);
            m_GameplayMap_Sprint = m_GameplayMap.FindAction("Sprint", throwIfNotFound: true);
            m_GameplayMap_GodMode = m_GameplayMap.FindAction("GodMode", throwIfNotFound: true);
            // MenuMap
            m_MenuMap = asset.FindActionMap("MenuMap", throwIfNotFound: true);
            m_MenuMap_ToggleMenu = m_MenuMap.FindAction("ToggleMenu", throwIfNotFound: true);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(asset);
        }

        public InputBinding? bindingMask
        {
            get => asset.bindingMask;
            set => asset.bindingMask = value;
        }

        public ReadOnlyArray<InputDevice>? devices
        {
            get => asset.devices;
            set => asset.devices = value;
        }

        public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

        public bool Contains(InputAction action)
        {
            return asset.Contains(action);
        }

        public IEnumerator<InputAction> GetEnumerator()
        {
            return asset.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Enable()
        {
            asset.Enable();
        }

        public void Disable()
        {
            asset.Disable();
        }

        public IEnumerable<InputBinding> bindings => asset.bindings;

        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
        {
            return asset.FindAction(actionNameOrId, throwIfNotFound);
        }

        public int FindBinding(InputBinding bindingMask, out InputAction action)
        {
            return asset.FindBinding(bindingMask, out action);
        }

        // GameplayMap
        private readonly InputActionMap m_GameplayMap;
        private List<IGameplayMapActions> m_GameplayMapActionsCallbackInterfaces = new List<IGameplayMapActions>();
        private readonly InputAction m_GameplayMap_Move;
        private readonly InputAction m_GameplayMap_LookDelta;
        private readonly InputAction m_GameplayMap_LookConst;
        private readonly InputAction m_GameplayMap_CameraZoom;
        private readonly InputAction m_GameplayMap_Jump;
        private readonly InputAction m_GameplayMap_Crouch;
        private readonly InputAction m_GameplayMap_Sprint;
        private readonly InputAction m_GameplayMap_GodMode;
        public struct GameplayMapActions
        {
            private @PlayerInputActions m_Wrapper;
            public GameplayMapActions(@PlayerInputActions wrapper) { m_Wrapper = wrapper; }
            public InputAction @Move => m_Wrapper.m_GameplayMap_Move;
            public InputAction @LookDelta => m_Wrapper.m_GameplayMap_LookDelta;
            public InputAction @LookConst => m_Wrapper.m_GameplayMap_LookConst;
            public InputAction @CameraZoom => m_Wrapper.m_GameplayMap_CameraZoom;
            public InputAction @Jump => m_Wrapper.m_GameplayMap_Jump;
            public InputAction @Crouch => m_Wrapper.m_GameplayMap_Crouch;
            public InputAction @Sprint => m_Wrapper.m_GameplayMap_Sprint;
            public InputAction @GodMode => m_Wrapper.m_GameplayMap_GodMode;
            public InputActionMap Get() { return m_Wrapper.m_GameplayMap; }
            public void Enable() { Get().Enable(); }
            public void Disable() { Get().Disable(); }
            public bool enabled => Get().enabled;
            public static implicit operator InputActionMap(GameplayMapActions set) { return set.Get(); }
            public void AddCallbacks(IGameplayMapActions instance)
            {
                if (instance == null || m_Wrapper.m_GameplayMapActionsCallbackInterfaces.Contains(instance)) return;
                m_Wrapper.m_GameplayMapActionsCallbackInterfaces.Add(instance);
                @Move.started += instance.OnMove;
                @Move.performed += instance.OnMove;
                @Move.canceled += instance.OnMove;
                @LookDelta.started += instance.OnLookDelta;
                @LookDelta.performed += instance.OnLookDelta;
                @LookDelta.canceled += instance.OnLookDelta;
                @LookConst.started += instance.OnLookConst;
                @LookConst.performed += instance.OnLookConst;
                @LookConst.canceled += instance.OnLookConst;
                @CameraZoom.started += instance.OnCameraZoom;
                @CameraZoom.performed += instance.OnCameraZoom;
                @CameraZoom.canceled += instance.OnCameraZoom;
                @Jump.started += instance.OnJump;
                @Jump.performed += instance.OnJump;
                @Jump.canceled += instance.OnJump;
                @Crouch.started += instance.OnCrouch;
                @Crouch.performed += instance.OnCrouch;
                @Crouch.canceled += instance.OnCrouch;
                @Sprint.started += instance.OnSprint;
                @Sprint.performed += instance.OnSprint;
                @Sprint.canceled += instance.OnSprint;
                @GodMode.started += instance.OnGodMode;
                @GodMode.performed += instance.OnGodMode;
                @GodMode.canceled += instance.OnGodMode;
            }

            private void UnregisterCallbacks(IGameplayMapActions instance)
            {
                @Move.started -= instance.OnMove;
                @Move.performed -= instance.OnMove;
                @Move.canceled -= instance.OnMove;
                @LookDelta.started -= instance.OnLookDelta;
                @LookDelta.performed -= instance.OnLookDelta;
                @LookDelta.canceled -= instance.OnLookDelta;
                @LookConst.started -= instance.OnLookConst;
                @LookConst.performed -= instance.OnLookConst;
                @LookConst.canceled -= instance.OnLookConst;
                @CameraZoom.started -= instance.OnCameraZoom;
                @CameraZoom.performed -= instance.OnCameraZoom;
                @CameraZoom.canceled -= instance.OnCameraZoom;
                @Jump.started -= instance.OnJump;
                @Jump.performed -= instance.OnJump;
                @Jump.canceled -= instance.OnJump;
                @Crouch.started -= instance.OnCrouch;
                @Crouch.performed -= instance.OnCrouch;
                @Crouch.canceled -= instance.OnCrouch;
                @Sprint.started -= instance.OnSprint;
                @Sprint.performed -= instance.OnSprint;
                @Sprint.canceled -= instance.OnSprint;
                @GodMode.started -= instance.OnGodMode;
                @GodMode.performed -= instance.OnGodMode;
                @GodMode.canceled -= instance.OnGodMode;
            }

            public void RemoveCallbacks(IGameplayMapActions instance)
            {
                if (m_Wrapper.m_GameplayMapActionsCallbackInterfaces.Remove(instance))
                    UnregisterCallbacks(instance);
            }

            public void SetCallbacks(IGameplayMapActions instance)
            {
                foreach (var item in m_Wrapper.m_GameplayMapActionsCallbackInterfaces)
                    UnregisterCallbacks(item);
                m_Wrapper.m_GameplayMapActionsCallbackInterfaces.Clear();
                AddCallbacks(instance);
            }
        }
        public GameplayMapActions @GameplayMap => new GameplayMapActions(this);

        // MenuMap
        private readonly InputActionMap m_MenuMap;
        private List<IMenuMapActions> m_MenuMapActionsCallbackInterfaces = new List<IMenuMapActions>();
        private readonly InputAction m_MenuMap_ToggleMenu;
        public struct MenuMapActions
        {
            private @PlayerInputActions m_Wrapper;
            public MenuMapActions(@PlayerInputActions wrapper) { m_Wrapper = wrapper; }
            public InputAction @ToggleMenu => m_Wrapper.m_MenuMap_ToggleMenu;
            public InputActionMap Get() { return m_Wrapper.m_MenuMap; }
            public void Enable() { Get().Enable(); }
            public void Disable() { Get().Disable(); }
            public bool enabled => Get().enabled;
            public static implicit operator InputActionMap(MenuMapActions set) { return set.Get(); }
            public void AddCallbacks(IMenuMapActions instance)
            {
                if (instance == null || m_Wrapper.m_MenuMapActionsCallbackInterfaces.Contains(instance)) return;
                m_Wrapper.m_MenuMapActionsCallbackInterfaces.Add(instance);
                @ToggleMenu.started += instance.OnToggleMenu;
                @ToggleMenu.performed += instance.OnToggleMenu;
                @ToggleMenu.canceled += instance.OnToggleMenu;
            }

            private void UnregisterCallbacks(IMenuMapActions instance)
            {
                @ToggleMenu.started -= instance.OnToggleMenu;
                @ToggleMenu.performed -= instance.OnToggleMenu;
                @ToggleMenu.canceled -= instance.OnToggleMenu;
            }

            public void RemoveCallbacks(IMenuMapActions instance)
            {
                if (m_Wrapper.m_MenuMapActionsCallbackInterfaces.Remove(instance))
                    UnregisterCallbacks(instance);
            }

            public void SetCallbacks(IMenuMapActions instance)
            {
                foreach (var item in m_Wrapper.m_MenuMapActionsCallbackInterfaces)
                    UnregisterCallbacks(item);
                m_Wrapper.m_MenuMapActionsCallbackInterfaces.Clear();
                AddCallbacks(instance);
            }
        }
        public MenuMapActions @MenuMap => new MenuMapActions(this);
        public interface IGameplayMapActions
        {
            void OnMove(InputAction.CallbackContext context);
            void OnLookDelta(InputAction.CallbackContext context);
            void OnLookConst(InputAction.CallbackContext context);
            void OnCameraZoom(InputAction.CallbackContext context);
            void OnJump(InputAction.CallbackContext context);
            void OnCrouch(InputAction.CallbackContext context);
            void OnSprint(InputAction.CallbackContext context);
            void OnGodMode(InputAction.CallbackContext context);
        }
        public interface IMenuMapActions
        {
            void OnToggleMenu(InputAction.CallbackContext context);
        }
    }
}
