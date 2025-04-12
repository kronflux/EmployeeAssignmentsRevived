using System;
using System.Text;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace EmployeeAssignmentsRevived.Network
{
    /// <summary>
    /// Container for mod-specific message serialization.
    /// </summary>
    [Serializable]
    public struct NetworkMessage
    {
        public string MessageTag;
        public ulong TargetId;
        public Hash128 Checksum;
        public byte[] Data;
    }

    /// <summary>
    /// Utility component for broadcasting and receiving raw network messages between clients.
    /// </summary>
    public class NetworkUtils : MonoBehaviour
    {
        public Action<string, byte[]> OnNetworkData;
        public Action OnDisconnect;
        public Action OnConnect;

        private bool _initialized;
        private bool _connected;

        /// <summary>
        /// Whether the client is connected to a host/server.
        /// </summary>
        public bool IsConnected => _connected;

        private void Initialize()
        {
            if (NetworkManager.Singleton?.CustomMessagingManager != null)
            {
                _initialized = true;
                Debug.Log("[NetworkUtils] Initialized messaging");
            }
        }

        private void UnInitialize()
        {
            if (_connected)
                Disconnected();

            _initialized = false;
        }

        private void Connected()
        {
            NetworkManager.Singleton.CustomMessagingManager.OnUnnamedMessage += OnMessageEvent;
            OnConnect?.Invoke();
            _connected = true;
            Debug.Log("[NetworkUtils] Connected");
        }

        private void Disconnected()
        {
            if (NetworkManager.Singleton?.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.OnUnnamedMessage -= OnMessageEvent;
            }

            OnDisconnect?.Invoke();
            _connected = false;
            Debug.Log("[NetworkUtils] Disconnected");
        }

        private void Update()
        {
            if (!_initialized)
            {
                Initialize();
            }
            else if (NetworkManager.Singleton == null)
            {
                UnInitialize();
            }
            else if (!_connected && NetworkManager.Singleton.IsConnectedClient)
            {
                Connected();
            }
            else if (_connected && !NetworkManager.Singleton.IsConnectedClient)
            {
                Disconnected();
            }
        }

        /// <summary>
        /// Called automatically when a client sends us an unnamed message.
        /// </summary>
        private void OnMessageEvent(ulong clientId, FastBufferReader reader)
        {
            Hash128 checksum = default;
            string json = "";
            NetworkMessage message = default;

            try
            {
                reader.ReadValueSafe(out json, false);
                message = JsonUtility.FromJson<NetworkMessage>(json);
                checksum = Hash128.Compute(message.Data);
                Debug.Log($"[NetworkUtils] Received {message.MessageTag} from {clientId}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            if (checksum != default && checksum.CompareTo(message.Checksum) == 0)
            {
                OnNetworkData?.Invoke(message.MessageTag, message.Data);
            }
        }

        /// <summary>
        /// Broadcasts a message to all connected clients.
        /// </summary>
        public void SendToAll(string tag, byte[] data)
        {
            if (!_initialized) return;

            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                SendTo(client.Value.ClientId, tag, data);
            }
        }

        /// <summary>
        /// Sends a message to a specific client.
        /// </summary>
        public void SendTo(ulong clientId, string tag, byte[] data)
        {
            if (!_initialized) return;

            NetworkMessage message = new NetworkMessage
            {
                MessageTag = tag,
                TargetId = clientId,
                Data = data,
                Checksum = Hash128.Compute(data)
            };

            string json = JsonUtility.ToJson(message);
            int writeSize = FastBufferWriter.GetWriteSize(json, false);

            using var writer = new FastBufferWriter(writeSize + 1, Allocator.Temp);
            writer.WriteValueSafe(json, false);

            Debug.Log($"[NetworkUtils] Sending {tag} to {clientId}");
            NetworkManager.Singleton.CustomMessagingManager.SendUnnamedMessage(clientId, writer, NetworkDelivery.Reliable);
        }
    }
}