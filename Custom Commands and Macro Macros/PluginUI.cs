using Dalamud.Interface;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace CCMM {
    public class PluginUI {
        private readonly CCMMPlugin plugin;
        private INode dragged = null;
        private Guid runningChoice = Guid.Empty;
        private bool showIdents = false;

        private bool _settingsVisible = false;
        public bool SettingsVisible { get => this._settingsVisible; set => this._settingsVisible = value; }

        public PluginUI(CCMMPlugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "CCMMPlugin cannot be null");
        }

        public void OpenSettings(object sender, EventArgs e) {
            this.SettingsVisible = true;
        }

        public void Draw() {
            if (this.SettingsVisible) {
                this.DrawSettings();
            }
        }

        private bool RemoveNode(List<INode> list, INode toRemove) {
            if (list.Remove(toRemove)) {
                return true;
            }

            foreach (INode node in list) {
                if (node.Children.Count > 0 && this.RemoveNode(node.Children, toRemove)) {
                    return true;
                }
            }

            return false;
        }

        private void DrawSettings() {
            // unset the cancel choice if no longer running
            if (this.runningChoice != Guid.Empty && !this.plugin.MacroHandler.IsRunning(this.runningChoice)) {
                this.runningChoice = Guid.Empty;
            }

            if (ImGui.Begin(this.plugin.Name, ref this._settingsVisible)) {
                ImGui.Columns(2);

                if (IconButton(FontAwesomeIcon.Plus)) {
                    this.plugin.Config.Nodes.Add(new Macro("Untitled macro", ""));
                    this.plugin.Config.Save();
                }
                Tooltip("Add macro");

                ImGui.SameLine();
                
                if (IconButton(FontAwesomeIcon.FolderPlus)) {
                    this.plugin.Config.Nodes.Add(new Folder("Untitled folder"));
                    this.plugin.Config.Save();
                }
                Tooltip("Add folder");

                List<INode> toRemove = new List<INode>();
                foreach (INode node in this.plugin.Config.Nodes) {
                    toRemove.AddRange(this.DrawNode(node));
                }
                foreach (INode node in toRemove) {
                    this.RemoveNode(this.plugin.Config.Nodes, node);
                }
                if (toRemove.Count != 0) {
                    this.plugin.Config.Save();
                }

                ImGui.NextColumn();

                ImGui.Text("Running macros");
                ImGui.PushItemWidth(-1f);
                if (ImGui.ListBoxHeader("##running-macros", this.plugin.MacroHandler.Running.Count, 5)) {
                    foreach (KeyValuePair<Guid, Macro> entry in this.plugin.MacroHandler.Running) {
                        string name = $"{entry.Value.Name}";
                        if (this.showIdents) {
                            string ident = entry.Key.ToString();
                            name += $" ({ident.Substring(ident.Length - 7)})";
                        }
                        if (this.plugin.MacroHandler.IsPaused(entry.Key)) {
                            name += " (paused)";
                        }
                        bool cancalled = this.plugin.MacroHandler.IsCancelled(entry.Key);
                        ImGuiSelectableFlags flags = cancalled ? ImGuiSelectableFlags.Disabled : ImGuiSelectableFlags.None;
                        if (ImGui.Selectable($"{name}##{entry.Key}", this.runningChoice == entry.Key, flags)) {
                            this.runningChoice = entry.Key;
                        }
                    }

                    ImGui.ListBoxFooter();
                }
                ImGui.PopItemWidth();

                if (ImGui.Button("Cancel") && this.runningChoice != Guid.Empty) {
                    this.plugin.MacroHandler.CancelMacro(this.runningChoice);
                }

                ImGui.SameLine();

                bool paused = this.runningChoice != Guid.Empty && this.plugin.MacroHandler.IsPaused(this.runningChoice);
                if (ImGui.Button(paused ? "Resume" : "Pause") && this.runningChoice != Guid.Empty) {
                    if (paused) {
                        this.plugin.MacroHandler.ResumeMacro(this.runningChoice);
                    } else {
                        this.plugin.MacroHandler.PauseMacro(this.runningChoice);
                    }
                }

                ImGui.SameLine();

                ImGui.Checkbox("Show unique identifiers", ref this.showIdents);

                ImGui.Columns(1);

                ImGui.End();
            }
        }

        private List<INode> DrawNode(INode node) {
            List<INode> toRemove = new List<INode>();
            ImGui.PushID($"{node.Id}");
            bool open = ImGui.TreeNode($"{node.Id}", $"{node.Name}");

            if (ImGui.BeginPopupContextItem()) {
                string name = node.Name;
                if (ImGui.InputText($"##{node.Id}-rename", ref name, (uint)this.plugin.Config.MaxLength, ImGuiInputTextFlags.AutoSelectAll)) {
                    node.Name = name;
                    this.plugin.Config.Save();
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
                this.dragged = node;
                ImGui.SetDragDropPayload("CCMM-GUID", IntPtr.Zero, 0);
                ImGui.EndDragDropSource();
            }

            if (node is Folder dfolder && ImGui.BeginDragDropTarget()) {
                ImGuiPayloadPtr payloadPtr = ImGui.AcceptDragDropPayload("CCMM-GUID");
                bool nullPtr;
                unsafe {
                    nullPtr = payloadPtr.NativePtr == null;
                }
                if (!nullPtr && payloadPtr.IsDelivery() && this.dragged != null) {
                    dfolder.Children.Add(this.dragged.Duplicate());
                    this.dragged.Id = Guid.NewGuid();
                    toRemove.Add(this.dragged);

                    this.dragged = null;
                }

                ImGui.EndDragDropTarget();
            }

            ImGui.PopID();

            if (open) {
                if (node is Macro macro) {
                    this.DrawMacro(macro);
                } else if (node is Folder folder) {
                    this.DrawFolder(folder);
                    foreach (INode child in node.Children) {
                        toRemove.AddRange(this.DrawNode(child));
                    }
                }

                ImGui.TreePop();
            }

            return toRemove;
        }

        private void DrawMacro(Macro macro) {
            string contents = macro.Contents;
            ImGui.PushItemWidth(-1f);
            if (ImGui.InputTextMultiline($"##{macro.Id}-editor", ref contents, (uint)this.plugin.Config.MaxLength, new Vector2(0, 250))) {
                macro.Contents = contents;
                this.plugin.Config.Save();
            }
            ImGui.PopItemWidth();

            if (ImGui.Button("Run")) {
                this.RunMacro(macro);
            }
        }

        private void DrawFolder(Folder folder) {

        }

        private void RunMacro(Macro macro) {
            this.plugin.MacroHandler.SpawnMacro(macro);
        }

        private static bool IconButton(FontAwesomeIcon icon) {
            ImGui.PushFont(UiBuilder.IconFont);
            bool ret = ImGui.Button(icon.ToIconString());
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
