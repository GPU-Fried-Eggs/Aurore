using Controller.Player;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Manager
{
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(GameSystem))]
    public partial class GameUISystem : SystemBase
    {
        private PlayerInputActions.MenuMapActions m_ActionsMap;

        private UIDocument m_MenuDocument;

        private MenuState m_LastKnownMenuState;

        private VisualElement m_RootPanel;
        private VisualElement m_ConnectionPanel;
        private VisualElement m_ConnectingPanel;
        private VisualElement m_GamePanel;
        private Button m_JoinButton;
        private Button m_HostButton;
        private Button m_DisconnectButton;
        private TextField m_NameTextField;
        private TextField m_JoinIPTextField;
        private TextField m_JoinPortTextField;
        private TextField m_HostPortTextField;

        private const string k_UIRootPanel = "RootPanel";
        private const string k_UIConnectionPanel = "ConnectionPanel";
        private const string k_UIConnectingPanel = "ConnectingPanel";
        private const string k_UIGamePanel = "GamePanel";
        private const string k_UIJoinButton = "JoinButton";
        private const string k_UIHostButton = "HostButton";
        private const string k_UIDisconnectButton = "DisconnectButton";
        private const string k_UINameTextField = "NameTextField";
        private const string k_UIJoinIPTextField = "JoinIPTextField";
        private const string k_UIJoinPortTextField = "JoinPortTextField";
        private const string k_UIHostPortTextField = "HostPortTextField";

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var inputActions = new PlayerInputActions();
            inputActions.Enable();
            inputActions.MenuMap.Enable();
            m_ActionsMap = inputActions.MenuMap;
        }

        protected override void OnUpdate()
        {
            var gameSingleton = SystemAPI.GetSingleton<GameSystem.Singleton>();

            // Check for state changes
            if (gameSingleton.MenuState != m_LastKnownMenuState)
            {
                SetState(gameSingleton.MenuState);
            
                if (gameSingleton.MenuState == MenuState.InGame)
                {
                    // Set invisible the first time we enter play
                    SetVisibleRecursive(m_RootPanel, false, gameSingleton.MenuState);
                }
                if (gameSingleton.MenuState == MenuState.InMenu)
                {
                    // Set visible when we enter menu
                    SetVisibleRecursive(m_RootPanel, true, gameSingleton.MenuState);
                }
            
                m_LastKnownMenuState = gameSingleton.MenuState;
            }

            // Toggle visibility
            if (m_ActionsMap.ToggleMenu.WasPressedThisFrame())
            {
                SetVisibleRecursive(m_RootPanel, !m_RootPanel.enabledSelf, gameSingleton.MenuState);
            }
        }

        public void SetUIReferences(GameUIAuthoring references)
        {
            m_MenuDocument = references.MenuDocument;

            #region Get element references

            m_RootPanel = m_MenuDocument.rootVisualElement.Q<VisualElement>(k_UIRootPanel);
            m_ConnectionPanel = m_MenuDocument.rootVisualElement.Q<VisualElement>(k_UIConnectionPanel);
            m_ConnectingPanel = m_MenuDocument.rootVisualElement.Q<VisualElement>(k_UIConnectingPanel);
            m_GamePanel = m_MenuDocument.rootVisualElement.Q<VisualElement>(k_UIGamePanel);
            m_JoinButton = m_MenuDocument.rootVisualElement.Q<Button>(k_UIJoinButton);
            m_HostButton = m_MenuDocument.rootVisualElement.Q<Button>(k_UIHostButton);
            m_DisconnectButton = m_MenuDocument.rootVisualElement.Q<Button>(k_UIDisconnectButton);
            m_NameTextField = m_MenuDocument.rootVisualElement.Q<TextField>(k_UINameTextField);
            m_JoinIPTextField = m_MenuDocument.rootVisualElement.Q<TextField>(k_UIJoinIPTextField);
            m_JoinPortTextField = m_MenuDocument.rootVisualElement.Q<TextField>(k_UIJoinPortTextField);
            m_HostPortTextField = m_MenuDocument.rootVisualElement.Q<TextField>(k_UIHostPortTextField);

            #endregion

            #region Subscribe events

            m_JoinButton.clicked += JoinButtonPressed;
            m_HostButton.clicked += HostButtonPressed;
            m_DisconnectButton.clicked += DisconnectButtonPressed;

            #endregion

            #region Initial state

            m_LastKnownMenuState = MenuState.InMenu;
            SetState(m_LastKnownMenuState);

            // When launched in server mode, auto-host
#if UNITY_SERVER
            HostButtonPressed();
#endif
            #endregion
        }

        private void SetVisibleRecursive(VisualElement root, bool visible, MenuState menuState)
        {
            root.visible = visible;
            root.SetEnabled(visible);
        
            if(visible) SetState(menuState);

            Cursor.visible = visible;
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        
            foreach (var child in root.Children())
            {
                SetVisibleRecursive(child, visible, menuState);
            }
        }

        private void SetState(MenuState state)
        {
            switch (state)
            {
                case MenuState.InMenu:
                    SetDisplay(m_ConnectionPanel, true);
                    SetDisplay(m_ConnectingPanel, false);
                    SetDisplay(m_GamePanel, false);
                    break;
                case MenuState.Connecting:
                    SetDisplay(m_ConnectionPanel, false);
                    SetDisplay(m_ConnectingPanel, true);
                    SetDisplay(m_GamePanel, false);
                    break;
                case MenuState.InGame:
                    SetDisplay(m_ConnectionPanel, false);
                    SetDisplay(m_ConnectingPanel, false);
                    SetDisplay(m_GamePanel, true);
                    break;
            }
        }

        private static void SetDisplay(VisualElement element, bool enabled) =>
            element.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;

        #region UI event handles

        private void JoinButtonPressed()
        {
            if (ushort.TryParse(m_JoinPortTextField.text, out var port) && NetworkEndpoint.TryParse(m_JoinIPTextField.text, port, out var newEndPoint))
            {
                var joinRequest = new GameSystem.JoinRequest
                {
                    LocalPlayerName = new FixedString128Bytes(m_NameTextField.text),
                    EndPoint = newEndPoint
                };
                var joinRequestEntity = World.EntityManager.CreateEntity();
                World.EntityManager.AddComponentData(joinRequestEntity, joinRequest);
            }
            else
            {
                Debug.LogError("Unable to parse Join IP or Port fields");
            }
        }

        private void HostButtonPressed()
        {
            if (ushort.TryParse(m_HostPortTextField.text, out var port) && NetworkEndpoint.TryParse(GameSystem.k_LocalHost, port, out var newLocalClientEndPoint))
            {
                var newServerEndPoint = NetworkEndpoint.AnyIpv4;
                newServerEndPoint.Port = port;
                var hostRequest = new GameSystem.HostRequest
                {
                    EndPoint = newServerEndPoint,
                };
                var hostRequestEntity = World.EntityManager.CreateEntity();
                World.EntityManager.AddComponentData(hostRequestEntity, hostRequest);

                // Only create local client if not in server mode
#if !UNITY_SERVER
                var joinRequest = new GameSystem.JoinRequest
                {
                    LocalPlayerName = new FixedString128Bytes(m_NameTextField.text),
                    EndPoint = newLocalClientEndPoint
                };
                var joinRequestEntity = World.EntityManager.CreateEntity();
                World.EntityManager.AddComponentData(joinRequestEntity, joinRequest);
#endif
            }
            else
            {
                Debug.LogError("Unable to parse Host Port field");
            }
        }

        private void DisconnectButtonPressed()
        {
            var disconnectRequestEntity = World.EntityManager.CreateEntity();
            World.EntityManager.AddComponentData(disconnectRequestEntity, new GameSystem.DisconnectRequest());
        }

        #endregion
    }
}
