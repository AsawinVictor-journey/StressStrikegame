using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Multiplayer
{
    /// <summary>
    /// Handles Unity Services init, anonymous auth, and hosting/joining a
    /// Netcode for GameObjects session over Unity Relay via Unity Transport.
    /// </summary>
    public class RelayManager : MonoBehaviour
    {
        [Header("UI")]
        public TMP_InputField joinCodeInput;
        public TMP_Text joinCodeText;
        public TMP_Text statusText;

        [Header("Relay")]
        [SerializeField] private int maxConnections = 4;
        [SerializeField] private string connectionType = "dtls";

        private bool _isBusy;
        private Task _signInTask;

        private UnityTransport Transport => NetworkManager.Singleton.GetComponent<UnityTransport>();

        private async void Awake()
        {
            await EnsureSignedInAsync();
        }

        private void Start()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
        }

        private void OnClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (clientId != nm.LocalClientId) return;

            SetStatus(nm.IsHost ? "Hosting - waiting for players" : "Connected!");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (clientId != nm.LocalClientId) return;

            SetStatus("Disconnected");
        }

        private void OnTransportFailure()
        {
            SetStatus("Transport failure - could not connect");
        }

        private Task EnsureSignedInAsync()
        {
            return _signInTask ??= SignInInternalAsync();
        }

        private async Task SignInInternalAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    SetStatus("Signing in...");
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                SetStatus("Signed in");
                Debug.Log($"[RelayManager] Signed in. Player ID: {AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception e)
            {
                SetStatus("Sign-in failed");
                Debug.LogException(e);
                throw;
            }
            finally
            {
                _signInTask = null;
            }
        }

        public async void HostGame()
        {
            if (_isBusy) return;
            _isBusy = true;

            try
            {
                await EnsureReadyAsync();

                SetStatus("Creating relay allocation...");
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

                SetStatus("Requesting join code...");
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                if (joinCodeText != null)
                    joinCodeText.text = joinCode;

                RelayServerData relayServerData = allocation.ToRelayServerData(connectionType);
                Transport.SetRelayServerData(relayServerData);

                bool started = NetworkManager.Singleton.StartHost();
                if (!started)
                    throw new InvalidOperationException("NetworkManager.StartHost() returned false.");

                SetStatus($"Hosting. Join code: {joinCode}");
                Debug.Log($"[RelayManager] Hosting with join code: {joinCode}");
            }
            catch (RelayServiceException e)
            {
                SetStatus("Host failed (relay error)");
                Debug.LogException(e);
            }
            catch (Exception e)
            {
                SetStatus("Host failed");
                Debug.LogException(e);
            }
            finally
            {
                _isBusy = false;
            }
        }

        public async void JoinGame()
        {
            if (_isBusy) return;

            string joinCode = joinCodeInput != null ? joinCodeInput.text.Trim() : null;
            if (string.IsNullOrEmpty(joinCode))
            {
                SetStatus("Enter a join code first");
                return;
            }

            _isBusy = true;

            try
            {
                await EnsureReadyAsync();

                SetStatus("Joining relay allocation...");
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                RelayServerData relayServerData = joinAllocation.ToRelayServerData(connectionType);
                Transport.SetRelayServerData(relayServerData);

                bool started = NetworkManager.Singleton.StartClient();
                if (!started)
                    throw new InvalidOperationException("NetworkManager.StartClient() returned false.");

                SetStatus("Connecting...");
            }
            catch (RelayServiceException e)
            {
                SetStatus("Join failed (relay error)");
                Debug.LogException(e);
            }
            catch (Exception e)
            {
                SetStatus("Join failed");
                Debug.LogException(e);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async Task EnsureReadyAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized ||
                !AuthenticationService.Instance.IsSignedIn)
            {
                await EnsureSignedInAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                throw new InvalidOperationException("Not signed in to Unity Services.");
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }
    }
}
