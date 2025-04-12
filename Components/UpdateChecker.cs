using System;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace EmployeeAssignmentsRevived.Components
{
    /// <summary>
    /// Checks the Thunderstore API for the latest published version of the mod.
    /// Compares it to the installed PluginInfo.PLUGIN_VERSION and shows a warning if outdated.
    /// </summary>
    public class UpdateChecker : MonoBehaviour
    {
        private const string THUNDERSTORE_API_URL = "https://thunderstore.io/api/experimental/package/FluxTeam/EmployeeAssignmentsRevived/";
        private UnityWebRequestAsyncOperation _webRequest;
        public bool IsLatestVersion { get; private set; } = true;

        [Serializable]
        private class ThunderstoreModInfo
        {
            public LatestVersion latest;
        }

        [Serializable]
        private class LatestVersion
        {
            public string version_number;
        }

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex == 2) // Main Menu
            {
                var req = UnityWebRequest.Get(THUNDERSTORE_API_URL);
                _webRequest = req.SendWebRequest();
                _webRequest.completed += OnComplete;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnComplete(AsyncOperation op)
        {
            if (_webRequest.webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[UpdateChecker] Failed to contact Thunderstore: {_webRequest.webRequest.error}");
                return;
            }

            try
            {
                string json = _webRequest.webRequest.downloadHandler.text;
                var modInfo = JsonUtility.FromJson<ThunderstoreModInfo>(json);

                IsLatestVersion = string.Equals(modInfo.latest.version_number, PluginInfo.PLUGIN_VERSION, StringComparison.OrdinalIgnoreCase);

                if (!IsLatestVersion)
                {
                    var menu = FindObjectOfType<MenuManager>();
                    if (menu != null)
                    {
                        menu.menuNotificationText.text = PluginInfo.VERSION_TEXT;
                        menu.menuNotificationButtonText.text = "[ CLOSE ]";
                        menu.menuNotification.SetActive(true);
                    }

                    Debug.LogWarning($"[UpdateChecker] Mod is outdated. Current: {PluginInfo.PLUGIN_VERSION}, Latest: {modInfo.latest.version_number}");
                }
                else
                {
                    Debug.Log($"[UpdateChecker] Mod is up to date (v{PluginInfo.PLUGIN_VERSION})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UpdateChecker] Failed to parse Thunderstore response: {ex}");
            }

            _webRequest = null;
        }
    }
}