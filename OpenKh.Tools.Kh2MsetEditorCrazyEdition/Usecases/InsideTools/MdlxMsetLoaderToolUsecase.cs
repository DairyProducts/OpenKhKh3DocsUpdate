using ImGuiNET;
using OpenKh.Kh2;
using OpenKh.Tools.Kh2MsetEditorCrazyEdition.Helpers;
using OpenKh.Tools.Kh2MsetEditorCrazyEdition.Interfaces;
using OpenKh.Tools.Kh2MsetEditorCrazyEdition.Models.Presets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static OpenKh.Tools.Common.CustomImGui.ImGuiEx;
using static OpenKh.Tools.Kh2MsetEditorCrazyEdition.ImGuiExHelpers;

namespace OpenKh.Tools.Kh2MsetEditorCrazyEdition.Usecases.InsideTools
{
    public class MdlxMsetLoaderToolUsecase : IToolRunnableProvider
    {
        private readonly GetMdlxMsetPresets _getMdlxMsetPresets;
        private readonly LayoutOnMultiColumnsUsecase _layoutOnMultiColumnsUsecase;
        private readonly LoadMotionDataUsecase _loadMotionDataUsecase;
        private readonly LoadMotionUsecase _loadMotionUsecase;
        private readonly ManageKingdomTextureUsecase _manageKingdomTextureUsecase;
        private readonly PrintActionResultUsecase _printActionResultUsecase;
        private readonly LoadModelUsecase _loadModelUsecase;
        private readonly LoadedModel _loadedModel;

        public MdlxMsetLoaderToolUsecase(
            LoadedModel loadedModel,
            LoadModelUsecase loadModelUsecase,
            PrintActionResultUsecase printActionResultUsecase,
            ManageKingdomTextureUsecase manageKingdomTextureUsecase,
            LoadMotionUsecase loadMotionUsecase,
            LoadMotionDataUsecase loadMotionDataUsecase,
            LayoutOnMultiColumnsUsecase layoutOnMultiColumnsUsecase,
            GetMdlxMsetPresets getMdlxMsetPresets
        )
        {
            _getMdlxMsetPresets = getMdlxMsetPresets;
            _layoutOnMultiColumnsUsecase = layoutOnMultiColumnsUsecase;
            _loadMotionDataUsecase = loadMotionDataUsecase;
            _loadMotionUsecase = loadMotionUsecase;
            _manageKingdomTextureUsecase = manageKingdomTextureUsecase;
            _printActionResultUsecase = printActionResultUsecase;
            _loadModelUsecase = loadModelUsecase;
            _loadedModel = loadedModel;
        }

        public Action CreateToolRunnable()
        {
            string? mdlxFile = "";
            string? msetFile = "";
            int presetSelectedIndex = -1;
            ActionResult mdlxResult = new ActionResult(ActionResultType.NotRun, "");
            var selectMotionVisible = false;
            var selectMotionCaption = "Select motion##motionSelector";
            ActionResult msetResult = new ActionResult(ActionResultType.NotRun, "");

            void LoadMotionAt(int index)
            {
                try
                {
                    _loadMotionDataUsecase.LoadAt(
                        index
                    );

                    _loadedModel.SelectedMotionIndex = index;

                    _loadedModel.OpenMotionPlayerOnce.TurnOn();

                    msetResult = ActionResult.Success;
                }
                catch (Exception ex)
                {
                    msetResult = new ActionResult(ActionResultType.Failure, ex.Message);
                }
            }

            return () =>
            {
                ForHeader("MdlxMsetLoader", () =>
                {
                    var autoLoadOnce = false;
                    string? defaultMotionOnce = null;

                    var presets = _getMdlxMsetPresets();

                    if (presets.Any())
                    {
                        if (ImGui.BeginCombo(
                            "presets",
                            presetSelectedIndex == -1
                            ? "..."
                            : presets
                                .Skip(presetSelectedIndex)
                                .Select(it => it.Label)
                                .First()
                            )
                        )
                        {
                            foreach (var (one, index) in presets.SelectWithIndex())
                            {
                                if (ImGui.Selectable(one.Label, index == presetSelectedIndex))
                                {
                                    presetSelectedIndex = index;
                                    mdlxFile = one.Mdlx;
                                    msetFile = one.Mset;

                                    if (one.AutoLoad)
                                    {
                                        autoLoadOnce = true;
                                        defaultMotionOnce = one.DefaultMotion;
                                    }
                                }
                            }
                            ImGui.EndCombo();
                        }
                    }

                    ImGui.InputText($"mdlxFile", ref mdlxFile, 256);
                    ImGui.InputText($"msetFile", ref msetFile, 256);

                    if (ImGui.Button("Load##loadMdlxMset") || autoLoadOnce)
                    {
                        try
                        {
                            _manageKingdomTextureUsecase.ClearCache();
                            _loadModelUsecase.OpenModel(mdlxFile!);
                            _loadMotionUsecase.OpenMotion(msetFile!);
                            mdlxResult = new ActionResult(ActionResultType.Success, "Success");
                        }
                        catch (Exception ex)
                        {
                            mdlxResult = new ActionResult(ActionResultType.Failure, ex.Message);
                        }
                    }

                    _printActionResultUsecase.Print(mdlxResult);


                    if (defaultMotionOnce != null)
                    {
                        if (int.TryParse(defaultMotionOnce, out int index))
                        {
                            LoadMotionAt(index);
                        }
                    }

                    if (_loadedModel.MotionList.Any())
                    {
                        var selectedMotionName = _loadedModel.SelectedMotionIndex == -1
                            ? "..."
                            : _loadedModel.MotionList[_loadedModel.SelectedMotionIndex].Label;

                        if (ImGui.Button($"{selectedMotionName}##selectMotion"))
                        {
                            ImGui.OpenPopup(selectMotionCaption);
                            selectMotionVisible = true;
                        }
                        ImGui.SameLine();
                        ImGui.Text("motion");

                        if (ImGui.BeginPopupModal(selectMotionCaption, ref selectMotionVisible,
                            ImGuiWindowFlags.Popup | ImGuiWindowFlags.Modal))
                        {
                            var list = _loadedModel.MotionList;

                            var layout = _layoutOnMultiColumnsUsecase.Layout(
                                ImGui.GetWindowSize(),
                                170,
                                ImGui.GetTextLineHeightWithSpacing(),
                                list.Count()
                            );

                            ImGui.Columns(layout.NumColumns);

                            foreach (var cell in layout.Cells)
                            {
                                var index = cell.Index;

                                var display = list[index];

                                if (!display.Valid)
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.3f, 0.3f, 0.3f, 1));
                                }

                                if (ImGui.Selectable($"{display.Label}##motion{index}", _loadedModel.SelectedMotionIndex == index))
                                {
                                    LoadMotionAt(index);
                                }

                                if (!display.Valid)
                                {
                                    ImGui.PopStyleColor();
                                }

                                ImGui.NextColumn();
                            }

                            ImGui.EndPopup();
                        }

                        _printActionResultUsecase.Print(msetResult);
                    }
                    else
                    {
                        ImGui.Text("(No motion loaded yet)");

                        msetResult = ActionResult.NotRun;
                    }
                },
                    openByDefault: true
                );
            };
        }
    }
}
