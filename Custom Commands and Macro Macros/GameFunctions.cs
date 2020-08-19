using Dalamud.Plugin;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CCMM {
    public class GameFunctions {
        private readonly CCMMPlugin plugin;

        private delegate IntPtr GetUIBaseDelegate();
        private delegate IntPtr GetUIModuleDelegate(IntPtr basePtr);
        private delegate void ProcessChatBoxDelegate(IntPtr raptureModule, IntPtr message, IntPtr uiModule);

        private readonly GetUIBaseDelegate GetUIBase;
        private readonly GetUIModuleDelegate GetUIModule;
        private readonly ProcessChatBoxDelegate _ProcessChatBox;

        public GameFunctions(CCMMPlugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "CCMMPlugin cannot be null");

            IntPtr getUIBasePtr = this.plugin.Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 41 b8 01 00 00 00 48 8d 15 ?? ?? ?? ?? 48 8b 48 20 e8 ?? ?? ?? ?? 48 8b cf");
            IntPtr getUIModulePtr = this.plugin.Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 83 7F ?? 00 48 8B F0");
            IntPtr processChatBoxPtr = this.plugin.Interface.TargetModuleScanner.ScanText("40 53 56 57 48 83 EC 70 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 48 8B 02");

            if (getUIBasePtr == IntPtr.Zero || getUIModulePtr == IntPtr.Zero || processChatBoxPtr == IntPtr.Zero) {
                PluginLog.Log($"getUIBasePtr: {getUIBasePtr.ToInt64():x}");
                PluginLog.Log($"getUIModulePtr: {getUIModulePtr.ToInt64():x}");
                PluginLog.Log($"processChatBoxPtr: {processChatBoxPtr.ToInt64():x}");
                throw new ApplicationException("Got null pointers for game signature(s)");
            }

            this.GetUIBase = Marshal.GetDelegateForFunctionPointer<GetUIBaseDelegate>(getUIBasePtr);
            this.GetUIModule = Marshal.GetDelegateForFunctionPointer<GetUIModuleDelegate>(getUIModulePtr);
            this._ProcessChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(processChatBoxPtr);
        }

        public void ProcessChatBox(string message) {
            IntPtr uiBase = this.GetUIBase();
            IntPtr uiModule = this.GetUIModule(Marshal.ReadIntPtr(this.plugin.Interface.TargetModuleScanner.Module.BaseAddress + 0x1ce80b8));

            if (uiBase == IntPtr.Zero || uiModule == IntPtr.Zero) {
                throw new ApplicationException("uiBase or uiModule was null");
            }

            IntPtr raptureModule = uiModule + 0xA4D00;
            
            byte[] bytes = Encoding.UTF8.GetBytes(message);

            IntPtr mem1 = Marshal.AllocHGlobal(400);
            IntPtr mem2 = Marshal.AllocHGlobal(bytes.Length + 30);

            Marshal.Copy(bytes, 0, mem2, bytes.Length);
            Marshal.WriteByte(mem2 + bytes.Length, 0);
            Marshal.WriteInt64(mem1, mem2.ToInt64());
            Marshal.WriteInt64(mem1 + 8, 64);
            Marshal.WriteInt64(mem1 + 8 + 8, bytes.Length + 1);
            Marshal.WriteInt64(mem1 + 8 + 8 + 8, 0);

            Marshal.WriteByte(uiModule + 675757, 1);
            Marshal.WriteInt16(uiModule + 169921, 0);

            this._ProcessChatBox(raptureModule, mem1, uiModule);

            Marshal.WriteByte(uiModule + 675757, 0);

            Marshal.FreeHGlobal(mem1);
            Marshal.FreeHGlobal(mem2);
        }
    }
}
