using Dalamud.Plugin;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Macrology {
    public class GameFunctions {
        private Macrology Plugin { get; }

        private delegate IntPtr GetUiModuleDelegate(IntPtr basePtr);

        private delegate void EasierProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);

        private readonly GetUiModuleDelegate _getUiModule;
        private readonly EasierProcessChatBoxDelegate _easierProcessChatBox;

        private readonly IntPtr _uiModulePtr;

        public GameFunctions(Macrology plugin) {
            this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin cannot be null");

            var getUiModulePtr = this.Plugin.Interface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 83 7F ?? 00 48 8B F0");
            var easierProcessChatBoxPtr = this.Plugin.Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9");
            this._uiModulePtr = this.Plugin.Interface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8D 54 24 ?? 48 83 C1 10 E8 ?? ?? ?? ??");

            if (getUiModulePtr == IntPtr.Zero || easierProcessChatBoxPtr == IntPtr.Zero || this._uiModulePtr == IntPtr.Zero) {
                PluginLog.Log($"getUIModulePtr: {getUiModulePtr.ToInt64():x}");
                PluginLog.Log($"easierProcessChatBoxPtr: {easierProcessChatBoxPtr.ToInt64():x}");
                PluginLog.Log($"this.uiModulePtr: {this._uiModulePtr.ToInt64():x}");
                throw new ApplicationException("Got null pointers for game signature(s)");
            }

            this._getUiModule = Marshal.GetDelegateForFunctionPointer<GetUiModuleDelegate>(getUiModulePtr);
            this._easierProcessChatBox = Marshal.GetDelegateForFunctionPointer<EasierProcessChatBoxDelegate>(easierProcessChatBoxPtr);
        }

        public void ProcessChatBox(string message) {
            var uiModule = this._getUiModule(Marshal.ReadIntPtr(this._uiModulePtr));

            if (uiModule == IntPtr.Zero) {
                throw new ApplicationException("uiModule was null");
            }

            using var payload = new ChatPayload(message);
            var mem1 = Marshal.AllocHGlobal(400);
            Marshal.StructureToPtr(payload, mem1, false);

            this._easierProcessChatBox(uiModule, mem1, IntPtr.Zero, 0);

            Marshal.FreeHGlobal(mem1);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
    internal readonly struct ChatPayload : IDisposable {
        [FieldOffset(0)]
        private readonly IntPtr textPtr;

        [FieldOffset(16)]
        private readonly ulong textLen;

        [FieldOffset(8)]
        private readonly ulong unk1;

        [FieldOffset(24)]
        private readonly ulong unk2;

        internal ChatPayload(string text) {
            var stringBytes = Encoding.UTF8.GetBytes(text);
            this.textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);
            Marshal.Copy(stringBytes, 0, this.textPtr, stringBytes.Length);
            Marshal.WriteByte(this.textPtr + stringBytes.Length, 0);

            this.textLen = (ulong) (stringBytes.Length + 1);

            this.unk1 = 64;
            this.unk2 = 0;
        }

        public void Dispose() {
            Marshal.FreeHGlobal(this.textPtr);
        }
    }
}
