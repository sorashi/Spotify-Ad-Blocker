using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace EZBlocker
{
    class SpotifyHook
    {
        private readonly Timer refreshTimer;
        private HashSet<int> children;
        private float lastPeak = 0f;
        private float peak = 0f;
        public SpotifyHook() {
            refreshTimer = new Timer((e) => {
                if (IsRunning()) {
                    WindowName = Spotify.MainWindowTitle;
                    Handle = Spotify.MainWindowHandle;
                    if (VolumeControl == null) {
                        VolumeControl = AudioUtils.GetVolumeControl(children);
                    }
                    else {
                        lastPeak = peak;
                        peak = AudioUtils.GetPeakVolume(VolumeControl.Control);
                    }
                }
                else {
                    ClearHooks();
                    HookSpotify();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }

        public IntPtr Handle { get; private set; }
        public Process Spotify { get; private set; }
        public AudioUtils.VolumeControl VolumeControl { get; private set; }
        public string WindowName { get; private set; }
        public string GetArtist() {
            if (IsPlaying()) {
                if (WindowName.Contains(" - "))
                    return WindowName.Split(new[] { " - " }, StringSplitOptions.None)[0];
                else
                    return WindowName;
            }

            return "";
        }

        public bool IsAdPlaying() {
            if (!WindowName.Equals("") && !WindowName.Equals("Drag") && IsPlaying()) {
                if (WindowName.Equals("Spotify")) // Prevent user pausing Spotify from being detected as ad (PeakVolume needs time to adjust)
                {
                    Debug.WriteLine("Ad1: " + lastPeak.ToString() + " " + peak.ToString());
                    return true;
                }
                else if (!WindowName.Contains(" - ")) {
                    Debug.WriteLine("Ad2: " + lastPeak.ToString() + " " + peak.ToString());
                    return true;
                }
            }
            return false;
        }

        public bool IsPlaying() {
            return peak > 0 && lastPeak > 0;
        }
        public bool IsRunning() {
            if (Spotify == null)
                return false;

            Spotify.Refresh();
            return !Spotify.HasExited;
        }
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        private void ClearHooks() {
            Spotify = null;
            WindowName = "";
            Handle = IntPtr.Zero;
            if (VolumeControl != null) Marshal.ReleaseComObject(VolumeControl.Control);
            VolumeControl = null;
        }

        private bool HookSpotify() {
            children = new HashSet<int>();

            // Try hooking through window title
            foreach (Process p in Process.GetProcessesByName("spotify")) {
                children.Add(p.Id);
                Spotify = p;
                if (p.MainWindowTitle.Length > 1) {
                    return true;
                }
            }

            // Try hooking through audio device
            VolumeControl = AudioUtils.GetVolumeControl(children);
            if (VolumeControl != null) {
                Spotify = Process.GetProcessById(VolumeControl.ProcessId);
                return true;
            }

            return false;
        }
    }
}
