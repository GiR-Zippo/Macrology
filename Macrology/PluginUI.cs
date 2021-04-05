using Dalamud.Interface;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Macrology {
    public class PluginUi {
        private Macrology Plugin { get; }
        private INode? Dragged { get; set; }
        private Guid RunningChoice { get; set; } = Guid.Empty;
        private bool _showIdents;

        private bool _settingsVisible;

        public bool SettingsVisible {
            get => this._settingsVisible;
            set => this._settingsVisible = value;
        }

        public PluginUi(Macrology plugin) {
            this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Macrology cannot be null");
        }

        public void OpenSettings(object sender, EventArgs e) {
            this.SettingsVisible = true;
        }

        public void Draw() {
            if (this.SettingsVisible) {
                this.DrawSettings();
            }
        }

        private bool RemoveNode(ICollection<INode> list, INode toRemove) {
            return list.Remove(toRemove) || list.Any(node => node.Children.Count > 0 && this.RemoveNode(node.Children, toRemove));
        }

        private void DrawSettings() {
            // unset the cancel choice if no longer running
            if (this.RunningChoice != Guid.Empty && !this.Plugin.MacroHandler.IsRunning(this.RunningChoice)) {
                this.RunningChoice = Guid.Empty;
            }

            if (!ImGui.Begin(this.Plugin.Name, ref this._settingsVisible)) {
                return;
            }

            ImGui.Columns(2);

            if (IconButton(FontAwesomeIcon.Plus)) {
                this.Plugin.Config.Nodes.Add(new Macro("Untitled macro", ""));
                this.Plugin.Config.Save();
            }

            Tooltip("Add macro");

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.FolderPlus)) {
                this.Plugin.Config.Nodes.Add(new Folder("Untitled folder"));
                this.Plugin.Config.Save();
            }

            Tooltip("Add folder");

            var toRemove = new List<INode>();
            foreach (var node in this.Plugin.Config.Nodes) {
                toRemove.AddRange(this.DrawNode(node));
            }

            foreach (var node in toRemove) {
                this.RemoveNode(this.Plugin.Config.Nodes, node);
            }

            if (toRemove.Count != 0) {
                this.Plugin.Config.Save();
            }

            ImGui.NextColumn();

            ImGui.Text("Running macros");
            ImGui.PushItemWidth(-1f);
            if (ImGui.BeginListBox("##running-macros")) {
                foreach (var entry in this.Plugin.MacroHandler.Running) {
                    var name = $"{entry.Value.Name}";
                    if (this._showIdents) {
                        var ident = entry.Key.ToString();
                        name += $" ({ident.Substring(ident.Length - 7)})";
                    }

                    if (this.Plugin.MacroHandler.IsPaused(entry.Key)) {
                        name += " (paused)";
                    }

                    var cancelled = this.Plugin.MacroHandler.IsCancelled(entry.Key);
                    var flags = cancelled ? ImGuiSelectableFlags.Disabled : ImGuiSelectableFlags.None;
                    if (ImGui.Selectable($"{name}##{entry.Key}", this.RunningChoice == entry.Key, flags)) {
                        this.RunningChoice = entry.Key;
                    }
                }

                ImGui.EndListBox();
            }

            ImGui.PopItemWidth();

            if (ImGui.Button("Cancel") && this.RunningChoice != Guid.Empty) {
                this.Plugin.MacroHandler.CancelMacro(this.RunningChoice);
            }

            ImGui.SameLine();

            var paused = this.RunningChoice != Guid.Empty && this.Plugin.MacroHandler.IsPaused(this.RunningChoice);
            if (ImGui.Button(paused ? "Resume" : "Pause") && this.RunningChoice != Guid.Empty) {
                if (paused) {
                    this.Plugin.MacroHandler.ResumeMacro(this.RunningChoice);
                } else {
                    this.Plugin.MacroHandler.PauseMacro(this.RunningChoice);
                }
            }

            ImGui.SameLine();

            ImGui.Checkbox("Show unique identifiers", ref this._showIdents);

            ImGui.Columns(1);

            ImGui.End();
        }

        private IEnumerable<INode> DrawNode(INode node) {
            var toRemove = new List<INode>();
            ImGui.PushID($"{node.Id}");
            var open = ImGui.TreeNode($"{node.Id}", $"{node.Name}");

            if (ImGui.BeginPopupContextItem()) {
                var name = node.Name;
                if (ImGui.InputText($"##{node.Id}-rename", ref name, (uint) this.Plugin.Config.MaxLength, ImGuiInputTextFlags.AutoSelectAll)) {
                    node.Name = name;
                    this.Plugin.Config.Save();
                }

                if (ImGui.Button("Delete")) {
                    toRemove.Add(node);
                }

                ImGui.SameLine();

                if (ImGui.Button("Copy UUID")) {
                    ImGui.SetClipboardText($"{node.Id}");
                }

                if (node is Macro macro) {
                    ImGui.SameLine();

                    if (ImGui.Button("Run##context")) {
                        this.RunMacro(macro);
                    }
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginDragDropSource()) {
                ImGui.Text(node.Name);
                this.Dragged = node;
                ImGui.SetDragDropPayload("MACROLOGY-GUID", IntPtr.Zero, 0);
                ImGui.EndDragDropSource();
            }

            if (node is Folder dfolder && ImGui.BeginDragDropTarget()) {
                var payloadPtr = ImGui.AcceptDragDropPayload("MACROLOGY-GUID");
                bool nullPtr;
                unsafe {
                    nullPtr = payloadPtr.NativePtr == null;
                }

                if (!nullPtr && payloadPtr.IsDelivery() && this.Dragged != null) {
                    dfolder.Children.Add(this.Dragged.Duplicate());
                    this.Dragged.Id = Guid.NewGuid();
                    toRemove.Add(this.Dragged);

                    this.Dragged = null;
                }

                ImGui.EndDragDropTarget();
            }

            ImGui.PopID();

            if (open) {
                if (node is Macro macro) {
                    this.DrawMacro(macro);
                } else if (node is Folder folder) {
                    this.DrawFolder(folder);
                    foreach (var child in node.Children) {
                        toRemove.AddRange(this.DrawNode(child));
                    }
                }

                ImGui.TreePop();
            }

            return toRemove;
        }

        private void DrawMacro(Macro macro) {
            var contents = macro.Contents;
            ImGui.PushItemWidth(-1f);
            if (ImGui.InputTextMultiline($"##{macro.Id}-editor", ref contents, (uint) this.Plugin.Config.MaxLength, new Vector2(0, 250))) {
                macro.Contents = contents;
                this.Plugin.Config.Save();
            }

            ImGui.PopItemWidth();

            if (ImGui.Button("Run")) {
                this.RunMacro(macro);
            }
        }

        private void DrawFolder(Folder folder) {
        }

        private void RunMacro(Macro macro) {
            this.Plugin.MacroHandler.SpawnMacro(macro);
        }

        private static bool IconButton(FontAwesomeIcon icon) {
            ImGui.PushFont(UiBuilder.IconFont);
            var ret = ImGui.Button(icon.ToIconString());
            ImGui.PopFont();
            return ret;
        }

        private static void Tooltip(string text) {
            if (ImGui.IsItemHovered()) {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(text);
                ImGui.EndTooltip();
            }
        }
    }
}
