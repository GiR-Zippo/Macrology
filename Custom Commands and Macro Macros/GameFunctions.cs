using Dalamud.Plugin;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CCMM {
    public class GameFunctions {
        private readonly CCMMPlugin plugin;

        private delegate IntPtr GetUIBaseDelegate();
        private delegate IntPtr GetUIModuleDelegate(IntPtr basePtr);
        private delegate void EasierProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);

        private readonly GetUIModuleDelegate GetUIModule;
        private readonly EasierProcessChatBoxDelegate _EasierProcessChatBox;

        private readonly IntPtr uiModulePtr;

        public GameFunctions(CCMMPlugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin cannot be null");

            IntPtr getUIModulePtr = this.plugin.Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 83 7F ?? 00 48 8B F0");
            IntPtr easierProcessChatBoxPtr = this.plugin.Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9");
            this.uiModulePtr = this.plugin.Interface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 54 24 ?? 48 83 C1 10 E8 ?? ?? ?? ??");

            if (getUIModulePtr == IntPtr.Zero || easierProcessChatBoxPtr == IntPtr.Zero || this.uiModulePtr == IntPtr.Zero) {
                PluginLog.Log($"getUIModulePtr: {getUIModulePtr.ToInt64():x}");
                PluginLog.Log($"easierProcessChatBoxPtr: {easierProcessChatBoxPtr.ToInt64():x}");
                PluginLog.Log($"this.uiModulePtr: {this.uiModulePtr.ToInt64():x}");
                throw new ApplicationException("Got null pointers for game signature(s)");
            }

            this.GetUIModule = Marshal.GetDelegateForFunctionPointer<GetUIModuleDelegate>(getUIModulePtr);
            this._EasierProcessChatBox = Marshal.GetDelegateForFunctionPointer<EasierProcessChatBoxDelegate>(easierProcessChatBoxPtr);
        }

        public void ProcessChatBox(string message) {
            IntPtr uiModule = this.GetUIModule(Marshal.ReadIntPtr(this.uiModulePtr));

            if (uiModule == IntPtr.Zero) {
                throw new ApplicationException("uiModule was null");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(message);

            IntPtr mem1 = Marshal.AllocHGlobal(400);
            IntPtr mem2 = Marshal.AllocHGlobal(bytes.Length + 30);

            Marshal.Copy(bytes, 0, mem2, bytes.Length);
            Marshal.WriteByte(mem2 + bytes.Length, 0);
            Marshal.WriteInt64(mem1, mem2.ToInt64());
            Marshal.WriteInt64(mem1 + 8, 64);
            Marshal.WriteInt64(mem1 + 8 + 8, bytes.Length + 1);
            Marshal.WriteInt64(mem1 + 8 + 8 + 8, 0);

            this._EasierProcessChatBox(uiModule, mem1, IntPtr.Zero, 0);

            Marshal.FreeHGlobal(mem1);
            Marshal.FreeHGlobal(mem2);
        }
    }
}
