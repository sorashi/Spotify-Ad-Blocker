﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EZBlocker
{
    class AudioUtils
    {
        private const int MEDIA_NEXTTRACK = 0xB0000;
        private const int MEDIA_PLAYPAUSE = 0xE0000;
        private const int WM_APPCOMMAND = 0x319;
        private enum EDataFlow
        {
            eRender,
            eCapture,
            eAll,
        }

        private enum ERole
        {
            eConsole,
            eMultimedia,
            eCommunications,
        }

        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ISimpleAudioVolume
        {
            [PreserveSig]
            int GetMasterVolume(out float pfLevel);

            [PreserveSig]
            int GetMute(out bool pbMute);

            [PreserveSig]
            int SetMasterVolume(float fLevel, Guid EventContext);
            [PreserveSig]
            int SetMute(bool bMute, Guid EventContext);
        }

        [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            float GetPeakValue(out float pfPeak);
        }

        [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            [PreserveSig]
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int GetGroupingParam(out Guid pRetVal);

            [PreserveSig]
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int GetProcessId(out int pRetVal);

            // IAudioSessionControl2
            [PreserveSig]
            int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);

            [PreserveSig]
            int IsSystemSoundsSession();

            // IAudioSessionControl
            [PreserveSig]
            int NotImpl0();
            [PreserveSig]
            int NotImpl1();

            [PreserveSig]
            int NotImpl2();

            [PreserveSig]
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);
            [PreserveSig]
            int SetDuckingPreference(bool optOut);

            [PreserveSig]
            int SetGroupingParam([MarshalAs(UnmanagedType.LPStruct)] Guid Override, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);

            [PreserveSig]
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, [MarshalAs(UnmanagedType.LPStruct)] Guid EventContext);
        }

        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            [PreserveSig]
            int GetCount(out int SessionCount);

            [PreserveSig]
            int GetSession(int SessionCount, out IAudioSessionControl2 Session);
        }

        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            [PreserveSig]
            int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);

            int NotImpl1();
            int NotImpl2();
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);

            int NotImpl1();
        }

        public static float GetPeakVolume(ISimpleAudioVolume v) {
            if (v == null)
                return 0f;

            IAudioMeterInformation meter = (IAudioMeterInformation)v;
            meter.GetPeakValue(out float peak);
            return peak * 100;
        }

        public static float? GetVolume(ISimpleAudioVolume v) {
            if (v == null)
                return null;

            v.GetMasterVolume(out float level);
            return level * 100;
        }

        public static VolumeControl GetVolumeControl(HashSet<int> p) {
            if (p == null) return null;

            ISimpleAudioVolume volumeControl = null;
            IMMDeviceEnumerator deviceEnumerator = null;
            IMMDevice speakers = null;
            IAudioSessionManager2 sm = null;
            IAudioSessionEnumerator sessionEnumerator = null;
            int ctlPid = 0;
            try {
                // Get default device
                deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out speakers);

                // Get session manager
                Guid IID_IAudioSessionManager2 = typeof(IAudioSessionManager2).GUID;
                speakers.Activate(ref IID_IAudioSessionManager2, 0, IntPtr.Zero, out object o);
                sm = (IAudioSessionManager2)o;

                // Get sessions
                sm.GetSessionEnumerator(out sessionEnumerator);
                sessionEnumerator.GetCount(out int count);

                // Get volume control
                for (int i = 0; i < count; i++) {
                    IAudioSessionControl2 ctl = null;
                    try {
                        sessionEnumerator.GetSession(i, out ctl);
                        if (ctl == null)
                            continue;

                        // Get and compare process id
                        ctl.GetProcessId(out ctlPid);
                        if (p.Contains(ctlPid)) {
                            volumeControl = ctl as ISimpleAudioVolume;
                            break;
                        }
                    }
                    finally {
                        if (volumeControl == null && ctl != null) Marshal.ReleaseComObject(ctl); // Only release if not target session
                    }
                }
            }
            finally {
                if (sessionEnumerator != null) Marshal.ReleaseComObject(sessionEnumerator);
                if (sm != null) Marshal.ReleaseComObject(sm);
                if (speakers != null) Marshal.ReleaseComObject(speakers);
                if (deviceEnumerator != null) Marshal.ReleaseComObject(deviceEnumerator);
            }

            if (volumeControl != null)
                return new VolumeControl(ctlPid, volumeControl);
            return null;
        }

        public static bool? IsMuted(ISimpleAudioVolume v) {
            if (v == null)
                return null;

            v.GetMute(out bool mute);
            return mute;
        }

        public static void SendNextTrack(IntPtr target) {
            SendMessage(target, WM_APPCOMMAND, IntPtr.Zero, (IntPtr)MEDIA_PLAYPAUSE);
        }

        public static void SendPlayPause(IntPtr target) {
            SendMessage(target, WM_APPCOMMAND, IntPtr.Zero, (IntPtr)MEDIA_PLAYPAUSE);
        }
        public static void SetMute(ISimpleAudioVolume v, bool mute) {
            if (v == null)
                return;

            v.SetMute(mute, Guid.Empty);
        }
        public static void SetVolume(ISimpleAudioVolume v, float level) {
            if (v == null)
                return;

            v.SetMasterVolume(level / 100, Guid.Empty);
        }
        // SendMessage for media controls
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        public class VolumeControl
        {
            public ISimpleAudioVolume Control;
            public int ProcessId;
            public VolumeControl(int pid, ISimpleAudioVolume control) {
                ProcessId = pid;
                Control = control;
            }
        }

        // Core Audio Imports
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator
        {
        }
    }
}
