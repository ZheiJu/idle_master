using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IdleMaster
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
    }

    public class Win32Process
    {
        public struct PROCESS_QUERY_INFORMATION
        {
            public IntPtr BaseAdress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public uint RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }
        public struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            private ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }
        [DllImport("kernel32.dll")]
        public static extern int VirtualQueryEx(IntPtr handle, IntPtr adress, out Win32Process.PROCESS_QUERY_INFORMATION processQuery, uint length);
        [DllImport("kernel32.dll")]
        public static extern void GetSystemInfo(out Win32Process.SYSTEM_INFO input);
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(uint ProcessAcces, bool bInheritHandle, int processId);
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(IntPtr handle, IntPtr adress, [Out] byte[] buffer, uint size, out IntPtr numberofbytesread);
    }
}
