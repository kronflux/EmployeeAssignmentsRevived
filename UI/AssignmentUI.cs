using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace EmployeeAssignmentsRevived.UI
{
    /// <summary>
    /// Displays the currently assigned objective on the HUD.
    /// Handles fade-in/out animation, text assignment, and distance display.
    /// </summary>
    public class AssignmentUI : MonoBehaviour
    {
        private enum State { Showing, Shown, Hiding, Hidden }

        private readonly Color NONE_TEXT_COLOR = new Color(1f, 0.8277f, 0.5236f, 0.3255f);
        private readonly Color BG_COLOR = new Color(1f, 0.6916f, 0.2594f, 1f);
        private readonly Color TITLE_COLOR = new Color(1f, 0.9356f, 0.8160f, 1f);
        private readonly Color TEXT_COLOR = new Color(0.3585f, 0.2703f, 0f, 1f);

        private const float SHOW_SPEED = 1f;
        private const float HIDE_SPEED = 2f;

        private readonly Vector2 SHOW_POSITION = new Vector2(-50f, -350f);
        private readonly Vector2 HIDE_POSITION = new Vector2(500f, -350f);

        private Canvas _canvas;
        private GameObject _noneText;
        private RectTransform _assignment;
        private Text _assignmentTitle;
        private Text _assignmentText;
        private Font _font;
        private QuickMenuManager _menuManager;
        private State _state;
        private float _animationProgress;
        public Assignment? _activeAssignment;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _state = State.Hidden;
            gameObject.layer = 5;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Setup Canvas
            _canvas = new GameObject("Canvas").AddComponent<Canvas>();
            _canvas.transform.SetParent(transform);
            _canvas.sortingOrder = -100;
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var canvasRect = _canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.zero;
            canvasRect.pivot = Vector2.zero;

            var scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // None active assignment fallback text
            _noneText = new GameObject("NoneText");
            var text = _noneText.AddComponent<Text>();
            text.fontSize = 20;
            text.font = _font;
            text.fontStyle = FontStyle.Bold;
            text.text = "NO ASSIGNMENT ACTIVE";
            text.color = NONE_TEXT_COLOR;
            text.alignment = TextAnchor.MiddleCenter;
            var noneTextRect = _noneText.GetComponent<RectTransform>();
            noneTextRect.SetParent(canvasRect);
            noneTextRect.pivot = Vector2.one;
            noneTextRect.anchorMin = Vector2.one;
            noneTextRect.anchorMax = Vector2.one;
            noneTextRect.anchoredPosition = new Vector2(-70f, -360f);
            noneTextRect.sizeDelta = new Vector2(310f, 50f);

            // Assignment panel
            var panel = new GameObject("AssignmentPanel").AddComponent<CanvasGroup>();
            panel.alpha = 0.5f;
            var image = panel.gameObject.AddComponent<Image>();
            image.color = BG_COLOR;
            _assignment = panel.GetComponent<RectTransform>();
            _assignment.SetParent(canvasRect);
            _assignment.pivot = Vector2.one;
            _assignment.anchorMin = Vector2.one;
            _assignment.anchorMax = Vector2.one;
            _assignment.anchoredPosition = SHOW_POSITION;
            _assignment.sizeDelta = new Vector2(350f, 80f);

            // Assignment title
            _assignmentTitle = new GameObject("Title").AddComponent<Text>();
            _assignmentTitle.font = _font;
            _assignmentTitle.fontSize = 20;
            _assignmentTitle.fontStyle = FontStyle.Bold;
            _assignmentTitle.text = "NO ASSIGNMENT ACTIVE";
            _assignmentTitle.color = TITLE_COLOR;
            _assignmentTitle.alignment = TextAnchor.UpperLeft;
            var titleRect = _assignmentTitle.GetComponent<RectTransform>();
            titleRect.SetParent(_assignment);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = Vector2.one;
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            titleRect.sizeDelta = new Vector2(-40f, 100f);

            // Assignment content text
            _assignmentText = new GameObject("Text").AddComponent<Text>();
            _assignmentText.font = _font;
            _assignmentText.fontSize = 16;
            _assignmentText.fontStyle = FontStyle.Bold;
            _assignmentText.text = "NO ASSIGNMENT ACTIVE";
            _assignmentText.color = TEXT_COLOR;
            _assignmentText.alignment = TextAnchor.LowerCenter;
            var textRect = _assignmentText.GetComponent<RectTransform>();
            textRect.SetParent(_assignment);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.anchoredPosition = new Vector2(0f, -10f);
            textRect.sizeDelta = new Vector2(-40f, -40f);
        }

        /// <summary>
        /// Updates the current assignment display.
        /// </summary>
        public void SetAssignment(Assignment assignment)
        {
            if (_state == State.Hidden || _state == State.Hiding)
            {
                ChangeState(State.Showing);
                _activeAssignment = assignment;
                _assignmentTitle.text = assignment.Name;
                _assignmentText.text = string.Format(assignment.ShortText, assignment.TargetText);
            }
        }

        /// <summary>
        /// Clears the assignment display with optional force.
        /// </summary>
        public void ClearAssignment(bool force = false)
        {
            if (force || _state == State.Shown || _state == State.Showing)
            {
                ChangeState(State.Hiding);
                _activeAssignment = null;
            }
        }

        private void ChangeState(State state)
        {
            _state = state;
            _animationProgress = 0f;
        }

        private void PanelAnimation()
        {
            if (_state == State.Shown || _state == State.Hidden) return;

            if (_animationProgress >= 1f)
            {
                _state = _state == State.Showing ? State.Shown : State.Hidden;
                return;
            }

            float speed = _state == State.Hiding ? HIDE_SPEED : SHOW_SPEED;
            _animationProgress += speed * Time.deltaTime;
            float eased = _state == State.Hiding ? 1f - _animationProgress : _animationProgress;
            _assignment.anchoredPosition = Vector2.Lerp(HIDE_POSITION, SHOW_POSITION, Utils.EaseInOutBack(eased));
        }

        private void Update()
        {
            PanelAnimation();

            bool show = GameNetworkManager.Instance?.localPlayerController != null &&
                        !GameNetworkManager.Instance.localPlayerController.isPlayerDead;

            if (_menuManager == null)
            {
                show = false;
                _menuManager = FindAnyObjectByType<QuickMenuManager>();
            }
            else
            {
                show &= !_menuManager.isMenuOpen;
            }

            if (_activeAssignment.HasValue && _activeAssignment.Value.FixedTargetPosition != Vector3.zero)
            {
                float dist = Vector3.Distance(_activeAssignment.Value.FixedTargetPosition,
                    GameNetworkManager.Instance.localPlayerController.transform.position);
                _assignmentText.text = string.Format(_activeAssignment.Value.ShortText, _activeAssignment.Value.TargetText) + $" {(int)dist}m";
            }

            _assignment.gameObject.SetActive(_state != State.Hidden);
            _noneText.SetActive(!_activeAssignment.HasValue);
            _canvas.enabled = show;
        }
    }
}