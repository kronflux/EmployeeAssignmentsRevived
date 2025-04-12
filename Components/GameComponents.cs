using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using TMPro;
using EmployeeAssignmentsRevived.Data;
using EmployeeAssignmentsRevived.Network;

namespace EmployeeAssignmentsRevived.Components
{
    /// <summary>
    /// Generates a markdown list of all killable enemies in the game and writes it to disk.
    /// This allows users to configure enemy assignments more easily.
    /// </summary>
    public class KillableEnemiesOutput : MonoBehaviour
    {
        private const string OUTPUT_PATH = "/../BepInEx/config2/KillableEnemies.md";

        public void Update()
        {
            if (StartOfRound.Instance == null) return;
            OutputList();
            Destroy(this);
        }

        private void OutputList()
        {
            var list = new HashSet<string>();
            var output = "# KILLABLE ENEMIES LIST" +
                         "Copy the names from this list into the enemy assignment whitelist config entry to allow them to spawn." +
                         "Note that some enemies would normally be spawnable on certain maps only.";

            foreach (var level in StartOfRound.Instance.levels)
            {
                foreach (var enemy in level.Enemies)
                {
                    if (enemy.enemyType.canDie && list.Add(enemy.enemyType.enemyName))
                    {
                        output += "- " + enemy.enemyType.enemyName + "";
                    }
                }
            }

            try
            {
                var path = Application.dataPath + OUTPUT_PATH;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, output);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to write killable enemies list: {ex}");
            }
        }
    }

    /// <summary>
    /// Tracks the game state and updates the shared GameContext accordingly.
    /// </summary>
    public class GameStateSync : MonoBehaviour
    {
        private GameContext _gameContext;
        private NetworkUtils _networkUtils;
        private bool _hasLanded;

        public void Inject(GameContext gameContext, NetworkUtils networkUtils)
        {
            _gameContext = gameContext;
            _networkUtils = networkUtils;
        }

        public void Update()
        {
            if (!_networkUtils.IsConnected || StartOfRound.Instance == null)
            {
                _gameContext.GameState = GameStateEnum.MainMenu;
            }
            else if (StartOfRound.Instance.inShipPhase && _hasLanded)
            {
                _hasLanded = false;
                _gameContext.GameState = GameStateEnum.Orbit;
            }
            else if (StartOfRound.Instance.shipHasLanded && !_hasLanded)
            {
                _hasLanded = true;
                string scene = RoundManager.Instance.currentLevel.sceneName;
                Debug.Log("Landed on moon: " + scene);
                _gameContext.GameState = scene == "CompanyBuilding" ? GameStateEnum.CompanyHQ : GameStateEnum.Level;
            }
        }
    }

    /// <summary>
    /// Checks for outdated mod version by pinging Thunderstore and showing a warning in the main menu if necessary.
    /// </summary>
    public class UpdateChecker : MonoBehaviour
    {
        private UnityWebRequestAsyncOperation _webRequest;
        public bool IsLatestVersion { get; private set; }

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex == 2) // Main menu
            {
                var req = new UnityWebRequest(PluginInfo.DOWNLOAD_URL);
                _webRequest = req.SendWebRequest();
                _webRequest.completed += OnComplete;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void OnComplete(AsyncOperation op)
        {
            IsLatestVersion = _webRequest.webRequest.responseCode == 404;

            if (!IsLatestVersion)
            {
                var menu = GameObject.FindObjectOfType<MenuManager>();
                if (menu != null)
                {
                    menu.menuNotificationText.text = PluginInfo.VERSION_TEXT;
                    menu.menuNotificationButtonText.text = "[ CLOSE ]";
                    menu.menuNotification.SetActive(true);
                }
            }

            _webRequest = null;
        }
    }
}