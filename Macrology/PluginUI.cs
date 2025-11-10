using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using static Dalamud.Bindings.ImGui.ImGui;
using static FFXIVClientStructs.FFXIV.Client.Graphics.Render.ModelRenderer;

namespace Macrology
{
    public class PluginUi
    {
        private Macrology Plugin { get; }
        private INode Dragged { get; set; }
        private Guid RunningChoice { get; set; } = Guid.Empty;
        private bool _showIdents;

        private bool _settingsVisible;

        public bool SettingsVisible
        {
            get => _settingsVisible;
            set => _settingsVisible = value;
        }

        public PluginUi(Macrology plugin)
        {
            Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Macrology cannot be null");
        }

        public void OpenSettings()
        {
            SettingsVisible = true;
        }

        public void Draw()
        {
            if (SettingsVisible)
                DrawSettings();
        }

        private bool InsertNode(ICollection<INode> list, INode toRemove)
        {
            return list.Remove(toRemove) || list.Any(node => node.Children.Count > 0 && RemoveNode(node.Children, toRemove));
        }

        private bool RemoveNode(ICollection<INode> list, INode toRemove)
        {
            return list.Remove(toRemove) || list.Any(node => node.Children.Count > 0 && RemoveNode(node.Children, toRemove));
        }

        private Dictionary<INode, INode> moveNode = new Dictionary<INode, INode>();

        private void DrawSettings()
        {
            // unset the cancel choice if no longer running
            if (RunningChoice != Guid.Empty && !Plugin.MacroHandler.IsRunning(RunningChoice))
                RunningChoice = Guid.Empty;

            if (!ImGui.Begin(Plugin.Name, ref _settingsVisible))
                return;

            ImGui.Columns(2);

            if (IconButton(FontAwesomeIcon.Plus))
            {
                Plugin.Config.Nodes.Add(new Macro("Untitled macro", ""));
                Plugin.Config.Save();
            }
            Tooltip("Add macro");

            ImGui.SameLine();

            if (IconButton(FontAwesomeIcon.FolderPlus))
            {
                Plugin.Config.Nodes.Add(new Folder("Untitled folder"));
                Plugin.Config.Save();
            }
            Tooltip("Add folder");

            var toRemove = new List<INode>();
            foreach (var node in Plugin.Config.Nodes)
                toRemove.AddRange(DrawNode(node));

            foreach (var node in toRemove)
                RemoveNode(Plugin.Config.Nodes, node);

            foreach (var mData in moveNode)
            {
                if (Plugin.Config.TryFindParent(mData.Value, out var draggedNodeParent))
                {
                    if (mData.Key is Folder targetFolderNode)
                    {
                        targetFolderNode.Children.Add(mData.Value);
                        draggedNodeParent.Children.Remove(mData.Value);
                    }
                    else
                    {
                        if (Plugin.Config.TryFindParent(mData.Key, out var targetNodeParent))
                        {
                            var targetNodeIndex = targetNodeParent.Children.IndexOf(mData.Key);
                            if (targetNodeParent == draggedNodeParent)
                            {
                                var draggedNodeIndex = targetNodeParent.Children.IndexOf(mData.Value);
                                if (draggedNodeIndex < targetNodeIndex)
                                {
                                    targetNodeIndex -= 1;
                                }
                                targetNodeParent.Children.Remove(mData.Value);
                                targetNodeParent.Children.Insert(targetNodeIndex, mData.Value);
                                Plugin.Config.Save();
                            }
                        }
                    }
                }
            }
            moveNode.Clear();

            if (toRemove.Count != 0)
                Plugin.Config.Save();

            ImGui.NextColumn();

            ImGui.Text("Running macros");
            ImGui.PushItemWidth(-1f);
            if (ImGui.BeginListBox("##running-macros"))
            {
                foreach (var (id, value) in Plugin.MacroHandler.Running)
                {
                    if (value == null)
                        continue;

                    var name = $"{value.Name}";
                    if (_showIdents)
                    {
                        var ident = id.ToString();
                        name += $" ({ident[^7..]})";
                    }

                    if (Plugin.MacroHandler.IsPaused(id))
                        name += " (paused)";

                    var cancelled = Plugin.MacroHandler.IsCancelled(id);
                    var flags = cancelled ? ImGuiSelectableFlags.Disabled : ImGuiSelectableFlags.None;
                    if (ImGui.Selectable($"{name}##{id}", RunningChoice == id, flags))
                        RunningChoice = id;
                }

                ImGui.EndListBox();
            }

            ImGui.PopItemWidth();

            if (ImGui.Button("Cancel") && RunningChoice != Guid.Empty)
                Plugin.MacroHandler.CancelMacro(RunningChoice);

            ImGui.SameLine();

            var paused = RunningChoice != Guid.Empty && Plugin.MacroHandler.IsPaused(RunningChoice);
            if (ImGui.Button(paused ? "Resume" : "Pause") && RunningChoice != Guid.Empty)
            {
                if (paused)
                    Plugin.MacroHandler.ResumeMacro(RunningChoice);
                else
                    Plugin.MacroHandler.PauseMacro(RunningChoice);
            }

            ImGui.SameLine();
            ImGui.Checkbox("Show unique identifiers", ref _showIdents);
            ImGui.Columns(1);
            ImGui.End();
        }

        private IEnumerable<INode> DrawNode(INode node)
        {
            var toRemove = new List<INode>();
            ImGui.PushID($"{node.Id}");
            var open = ImGui.TreeNode($"{node.Name}");

            if (ImGui.BeginPopupContextItem(""))
            {
                var name = node.Name;
                if (ImGui.InputText($"##{node.Id}-rename", ref name, Plugin.Config.MaxLength, ImGuiInputTextFlags.AutoSelectAll))
                {
                    node.Name = name;
                    Plugin.Config.Save();
                }

                if (ImGui.Button("Delete"))
                    toRemove.Add(node);

                ImGui.SameLine();

                if (ImGui.Button("Copy UUID"))
                    ImGui.SetClipboardText($"{node.Id}");

                if (node is Macro macro)
                {
                    ImGui.SameLine();

                    if (ImGui.Button("Run##context"))
                        RunMacro(macro);
                }

                ImGui.EndPopup();
            }

            if (ImGui.BeginDragDropSource())
            {
                ImGui.Text(node.Name);
                Dragged = node;
                ImGui.SetDragDropPayload("MACROLOGY-GUID", new ReadOnlySpan<byte>(null), 0);
                ImGui.EndDragDropSource();
            }

            if (node is Folder dfolder && ImGui.BeginDragDropTarget())
            {
                var payloadPtr = ImGui.AcceptDragDropPayload("MACROLOGY-GUID");
                bool nullPtr;
                unsafe
                {
                    nullPtr = payloadPtr.IsNull;// == null;
                }

                if (!nullPtr && payloadPtr.IsDelivery() && Dragged != null)
                {
                    dfolder.Children.Add(Dragged.Duplicate());
                    Dragged.Id = Guid.NewGuid();
                    toRemove.Add(Dragged);

                    Dragged = null;
                }

                ImGui.EndDragDropTarget();
            }
            else if (node is Macro dmacro && ImGui.BeginDragDropTarget())
            {
                var payload = ImGui.AcceptDragDropPayload("MACROLOGY-GUID");
                bool nullPtr;
                unsafe { nullPtr = payload.IsNull; }

                var targetNode = node;
                if (!nullPtr && payload.IsDelivery() && Dragged != null)
                {
                    if (Plugin.Config.TryFindParent(Dragged, out var draggedNodeParent))
                    {
                        if (Plugin.Config.TryFindParent(targetNode, out var targetNodeParent))
                            moveNode.Add(targetNode, Dragged);
                        else
                            Plugin.PluginLog.Debug($"Could not find parent of node \"{targetNode.Name}\"");
                    }
                    else
                        Plugin.PluginLog.Debug($"Could not find parent of node \"{Dragged.Name}\"");

                    Dragged = null;
                }

                ImGui.EndDragDropTarget();
            }

            ImGui.PopID();

            if (open)
            {
                if (node is Macro macro)
                    DrawMacro(macro);
                else if (node is Folder folder)
                {
                    DrawFolder(folder);
                    foreach (var child in node.Children)
                        toRemove.AddRange(DrawNode(child));
                }

                ImGui.TreePop();
            }

            return toRemove;
        }

        private void DrawMacro(Macro macro)
        {
            string contents = macro.Contents;
            ImGui.PushItemWidth(-1f);
            if (ImGui.InputTextMultiline((ImU8String)$"##{macro.Id}-editor", ref contents, Plugin.Config.MaxLength, new Vector2(0, 250), ImGuiInputTextFlags.None))
            {
                macro.Contents = contents;
                Plugin.Config.Save();
            }

            ImGui.PopItemWidth();

            if (ImGui.Button("Run"))
                RunMacro(macro);
        }

        private void DrawFolder(Folder folder)
        {
        }

        private void RunMacro(Macro macro)
        {
            Plugin.MacroHandler.SpawnMacro(macro);
        }

        private static bool IconButton(FontAwesomeIcon icon)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var ret = ImGui.Button(icon.ToIconString());
            ImGui.PopFont();
            return ret;
        }

        private static void Tooltip(string text)
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(text);
                ImGui.EndTooltip();
            }
        }
    }
}
