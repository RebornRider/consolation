using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IngameConsole
{
    /// <summary>
    ///     A console to display Unity's debug logs in-game.
    /// </summary>
    internal class Console : MonoBehaviour
    {
        #region Inspector Settings

        /// <summary>
        ///     The hotkey to show and hide the console window.
        /// </summary>
        public KeyCode ToggleKey = KeyCode.F7;

        /// <summary>
        ///     Whether to open the window by shaking the device (mobile-only).
        /// </summary>
        public bool ShakeToOpen = true;

        /// <summary>
        ///     The (squared) acceleration above which the window should open.
        /// </summary>
        public float ShakeAcceleration = 3f;

        /// <summary>
        ///     Whether to only keep a certain number of logs.
        ///     Setting this can be helpful if memory usage is a concern.
        /// </summary>
        public bool RestrictLogCount;

        /// <summary>
        ///     Number of logs to keep before removing old ones.
        /// </summary>
        public int MaxLogs = 1000;

        #endregion

        private struct Log
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
        }


        private const string WindowTitle = "Console";
        private const int Margin = 20;
        private const Colors StackTraceColor = Colors.grey;

        // Visual elements:

        private static readonly Dictionary<LogType, Color> LogTypeColors = new Dictionary<LogType, Color>
        {
            {LogType.Assert, Color.white},
            {LogType.Error, Color.red},
            {LogType.Exception, Color.red},
            {LogType.Log, Color.white},
            {LogType.Warning, Color.yellow}
        };

        private static readonly GUIContent ClearLabel = new GUIContent("Clear", "Clear the contents of the console.");
        private static readonly GUIContent CollapseLabel = new GUIContent("Collapse", "Hide repeated messages.");
        private static readonly GUIContent StackTraceLabel = new GUIContent("Show Stack Trace", "Show log origin.");

        private readonly List<Log> _logs = new List<Log>();

        private readonly Rect _titleBarRect = new Rect(0, 0, 10000, 20);
        private bool _collapse;
        private bool _showStackTrace;
        private Vector2 _scrollPosition;
        private bool _visible;
        private Rect _windowRect = new Rect(Margin, Margin, Screen.width - Margin * 2, Screen.height - Margin * 2);

        private void OnEnable()
        {
#if UNITY_5
            Application.logMessageReceived += HandleLog;
#else
            Application.RegisterLogCallback(HandleLog);
#endif
        }

        private void OnDisable()
        {
#if UNITY_5
            Application.logMessageReceived -= HandleLog;
#else
            Application.RegisterLogCallback(null);
#endif
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                _visible = !_visible;
            }

            if (ShakeToOpen && Input.acceleration.sqrMagnitude > ShakeAcceleration)
            {
                _visible = true;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            _windowRect = GUILayout.Window(123456, _windowRect, DrawConsoleWindow, WindowTitle);
        }

        /// <summary>
        ///     Displays a window that lists the recorded logs.
        /// </summary>
        /// <param name="windowId">Window ID.</param>
        private void DrawConsoleWindow(int windowId)
        {
            DrawLogsList();
            DrawToolbar();

            // Allow the window to be dragged by its title bar.
            GUI.DragWindow(_titleBarRect);
        }

        /// <summary>
        ///     Displays a scrollable list of logs.
        /// </summary>
        private void DrawLogsList()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            // Used to determine height of accumulated log labels.
            GUILayout.BeginVertical();

            // Iterate through the recorded logs.
            for (var i = 0; i < _logs.Count; i++)
            {
                Log log = _logs[i];

                // Combine identical messages if collapse option is chosen.
                if (_collapse && i > 0)
                {
                    string previousMessage = _logs[i - 1].Message;

                    if (log.Message == previousMessage)
                    {
                        continue;
                    }
                }

                GUI.contentColor = LogTypeColors[log.Type];
                if (_showStackTrace)
                {
                    GUILayout.Label(log.Message + FormatStackTrace(log.StackTrace));
                }
                else
                {
                    GUILayout.Label(log.Message);
                }
            }

            GUILayout.EndVertical();
            Rect innerScrollRect = GUILayoutUtility.GetLastRect();
            GUILayout.EndScrollView();
            Rect outerScrollRect = GUILayoutUtility.GetLastRect();

            // If we're scrolled to bottom now, guarantee that it continues to be in next cycle.
            if (Event.current.type == EventType.Repaint && IsScrolledToBottom(innerScrollRect, outerScrollRect))
            {
                ScrollToBottom();
            }

            // Ensure GUI colour is reset before drawing other components.
            GUI.contentColor = Color.white;
        }

        private static string FormatStackTrace(string stackTrace)
        {
            return stackTrace.Trim()
                .Split('\n')
                .Last()
                .Insert(0, "\n --> ")
                .Italics()
                .Colored(StackTraceColor);
        }

        /// <summary>
        ///     Displays options for filtering and changing the logs list.
        /// </summary>
        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(ClearLabel))
            {
                _logs.Clear();
            }

            _collapse = GUILayout.Toggle(_collapse, CollapseLabel, GUILayout.ExpandWidth(false));
            _showStackTrace = GUILayout.Toggle(_showStackTrace, StackTraceLabel, GUILayout.ExpandWidth(false));

            GUILayout.EndHorizontal();
        }

        /// <summary>
        ///     Records a log from the stackTrace callback.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="stackTrace">Trace of where the message came from.</param>
        /// <param name="type">Type of message (error, exception, warning, assert).</param>
        private void HandleLog(string message, string stackTrace, LogType type)
        {
            _logs.Add(new Log
            {
                Message = message,
                StackTrace = stackTrace,
                Type = type
            });

            TrimExcessLogs();
        }

        /// <summary>
        ///     Determines whether the scroll view is scrolled to the bottom.
        /// </summary>
        /// <param name="innerScrollRect">Rect surrounding scroll view content.</param>
        /// <param name="outerScrollRect">Scroll view container.</param>
        /// <returns>Whether scroll view is scrolled to bottom.</returns>
        private bool IsScrolledToBottom(Rect innerScrollRect, Rect outerScrollRect)
        {
            float innerScrollHeight = innerScrollRect.height;

            // Take into account extra padding added to the scroll container.
            float outerScrollHeight = outerScrollRect.height - GUI.skin.box.padding.vertical;

            // If contents of scroll view haven't exceeded outer container, treat it as scrolled to bottom.
            if (outerScrollHeight > innerScrollHeight)
            {
                return true;
            }

            bool scrolledToBottom = Mathf.Approximately(innerScrollHeight, _scrollPosition.y + outerScrollHeight);
            return scrolledToBottom;
        }

        /// <summary>
        ///     Moves the scroll view down so that the last log is visible.
        /// </summary>
        private void ScrollToBottom()
        {
            _scrollPosition = new Vector2(0, int.MaxValue);
        }

        /// <summary>
        ///     Removes old logs that exceed the maximum number allowed.
        /// </summary>
        private void TrimExcessLogs()
        {
            if (!RestrictLogCount)
            {
                return;
            }

            int amountToRemove = Mathf.Max(_logs.Count - MaxLogs, 0);

            if (amountToRemove == 0)
            {
                return;
            }

            _logs.RemoveRange(0, amountToRemove);
        }
    }
}