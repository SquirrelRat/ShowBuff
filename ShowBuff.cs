using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ImGuiNET;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace ShowBuff_exileapi;

public class ShowBuff : BaseSettingsPlugin<ShowBuffSettings>
{
    private string T(string en, string ru) => Settings.UseEnglish ? en : ru;

    public override bool Initialise()
    {
        Name = "ShowBuff";
        return true;
    }

    public override void DrawSettings()
    {
        base.DrawSettings();

        ImGui.Separator();
        ImGui.Text(T("Buff Settings", "Настройки баффов"));

        ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1),
            T("Examples: unique_nearby_allies_are_lucky, player_aura_resists, ground_desecration",
              "Примеры: unique_nearby_allies_are_lucky, player_aura_resists, ground_desecration"));
        ImGui.TextColored(new System.Numerics.Vector4(1, 0.6f, 0.2f, 1),
            T("Auras: set Min Stacks = 0 and enable 'Hide Count' if no stacks",
              "Ауры: ставьте Min Stacks = 0 и включайте 'Hide Count' если стаков нет"));
        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1),
            T("Find names in DevTree -> Player -> Buffs",
              "Имена берите в DevTree -> Player -> Buffs"));

        for (int i = 0; i < Settings.BuffSettings.Count; i++)
        {
            var buff = Settings.BuffSettings[i];
            ImGui.PushID(i);

            bool show = buff.Show.Value;
            if (ImGui.Checkbox($"##Show{i}", ref show))
                buff.Show.Value = show;
            ImGui.SameLine();

            var buffName = buff.BuffName.Value;
            ImGui.SetNextItemWidth(150);
            if (ImGui.InputText(T("Buff Name", "Название баффа") + $"##{i}", ref buffName, 100))
                buff.BuffName.Value = buffName;
            ImGui.SameLine();

            var display = buff.DisplayName.Value;
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputText(T("Display", "Отображение") + $"##{i}", ref display, 64))
                buff.DisplayName.Value = display;
            ImGui.SameLine();

            if (ImGui.Button($"-##{i}"))
            {
                Settings.BuffSettings.RemoveAt(i);
                ImGui.PopID();
                i--;
                continue;
            }

            if (show)
            {
                ImGui.Indent();

                var c = buff.TextColor.Value;
                var vec = new System.Numerics.Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
                if (ImGui.ColorEdit4(T("Color", "Цвет") + $"##{i}", ref vec))
                {
                    buff.TextColor.Value = new Color(
                        (int)(vec.X * 255), (int)(vec.Y * 255), (int)(vec.Z * 255), (int)(vec.W * 255));
                }

                int minStacks = buff.MinStacks.Value;
                if (ImGui.SliderInt(T("Min Stacks", "Мин. стаков") + $"##{i}", ref minStacks, 0, 100))
                    buff.MinStacks.Value = minStacks;

                bool hideStacks = buff.HideStackCount.Value;
                if (ImGui.Checkbox(T("Hide Count (auras with 0)", "Скрыть количество (ауры с 0)") + $"##{i}", ref hideStacks))
                    buff.HideStackCount.Value = hideStacks;

                bool useHead = buff.UseHeadPosition.Value;
                if (ImGui.Checkbox(T("Above Head", "Над головой") + $"##{i}", ref useHead))
                    buff.UseHeadPosition.Value = useHead;

                int x = buff.PositionX.Value;
                int y = buff.PositionY.Value;
                if (!useHead)
                {
                    if (ImGui.SliderInt(T("Position X", "Позиция X") + $"##{i}", ref x, -1500, 1500))
                        buff.PositionX.Value = x;
                    if (ImGui.SliderInt(T("Position Y", "Позиция Y") + $"##{i}", ref y, -1500, 1500))
                        buff.PositionY.Value = y;
                }
                else
                {
                    if (ImGui.SliderInt(T("Offset X", "Смещение X") + $"##{i}", ref x, -1500, 1500))
                        buff.PositionX.Value = x;
                    if (ImGui.SliderInt(T("Offset Y", "Смещение Y") + $"##{i}", ref y, -1500, 1500))
                        buff.PositionY.Value = y;
                }

                ImGui.Unindent();
            }

            ImGui.PopID();
        }

        if (ImGui.Button("+ " + T("Add Buff", "Добавить бафф")))
            Settings.BuffSettings.Add(new ShowBuffSetting { DisplayName = new ExileCore.Shared.Nodes.TextNode($"Buff{Settings.BuffSettings.Count + 1}") });
    }

    public override void Render()
    {
        if (!Settings.Enable)
            return;

        if (!Settings.ShowInHideout && GameController?.Area?.CurrentArea?.IsHideout == true)
            return;

        var player = GameController.Player;
        if (player == null || !player.IsValid)
            return;

        var playerPos = player.Pos;
        if (playerPos.Equals(default(Vector3)))
            return;

        var screenPosSharp = GameController.IngameState.Camera.WorldToScreen(playerPos);
        if (screenPosSharp.Equals(default(SharpDX.Vector2)))
            return;

        var screenPos = new Vector2(screenPosSharp.X, screenPosSharp.Y);
        screenPos.Y -= Settings.HeightOffset;

        var active = GetActiveBuffs(player);
        DrawBuffs(screenPos, active);
    }

    private List<(string displayName, int count, Color color, bool useHeadPos, int posX, int posY, bool hideCount)> GetActiveBuffs(Entity player)
    {
        var result = new List<(string, int, Color, bool, int, int, bool)>();

        try
        {
            if (!player.TryGetComponent<Buffs>(out var buffComp))
                return result;

            var buffs = buffComp.BuffsList ?? new List<Buff>();

            foreach (var cfg in Settings.BuffSettings)
            {
                if (!cfg.Show.Value || string.IsNullOrWhiteSpace(cfg.BuffName.Value))
                    continue;

                int cnt = CountBuffsByName(buffs, cfg.BuffName.Value);
                if (cnt > cfg.MinStacks.Value)
                {
                    result.Add((
                        cfg.DisplayName.Value,
                        cnt,
                        cfg.TextColor.Value,
                        cfg.UseHeadPosition.Value,
                        cfg.PositionX.Value,
                        cfg.PositionY.Value,
                        cfg.HideStackCount.Value
                    ));
                }
            }
        }
        catch (Exception e)
        {
            LogError($"GetActiveBuffs error: {e.Message}", 5);
        }

        return result;
    }

    private static int CountBuffsByName(IEnumerable<Buff> buffs, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        var needle = name.ToLowerInvariant();
        int count = 0;
        foreach (var b in buffs)
        {
            var n = b?.Name?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(n) && n.Contains(needle))
                count++;
        }
        return count;
    }

    private void DrawBuffs(Vector2 headPos, List<(string displayName, int count, Color color, bool useHeadPos, int posX, int posY, bool hideCount)> items)
    {
        if (items.Count == 0) return;

        using (Graphics.SetTextScale(Settings.FontSize))
        {
            float lineHeight = 20f;
            float currentY = headPos.Y;

            var sorted = items.OrderBy(i => i.useHeadPos ? 0 : 1).ToList();
            foreach (var (display, count, color, useHead, x, y, hide) in sorted)
            {
                var text = hide || count == 0 ? display : $"{display}: {count}";

                Vector2 pos = useHead
                    ? new Vector2(headPos.X + x, currentY + y)
                    : new Vector2(x, y);

                var size = Graphics.MeasureText(text);
                var textPos = new Vector2(pos.X - size.X / 2, pos.Y - size.Y / 2);

                if (Settings.ShowBackground)
                {
                    var bgPos = new Vector2(textPos.X - 5, textPos.Y - 2);
                    var bgSize = new Vector2(size.X + 10, size.Y + 4);
                    Graphics.DrawBox(bgPos, bgPos + bgSize, Settings.BackgroundColor);
                }

                Graphics.DrawText(text, textPos, color);

                if (useHead)
                    currentY += lineHeight * Settings.FontSize;
            }
        }
    }
}


