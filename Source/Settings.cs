using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using System;
using System.Reflection;
using ConsolidatedTraits;

namespace ConfigurableMaps.Settings
{
    public class Controller : Mod
    {
        public static Settings Settings;

        public Controller(ModContentPack content) : base(content)
        { 
			if (!HarmonyPatches.UsingInGameDefEditor)
				Settings = base.GetSettings<Settings>();
        }

        public override string SettingsCategory()
		{
			return "ConsolidatedTraits".Translate();
        }

		public override void DoSettingsWindowContents(Rect inRect)
		{
			if (!HarmonyPatches.UsingInGameDefEditor)
				Settings.DoWindowContents(inRect);
			else
				Widgets.Label(new Rect(inRect.xMin, inRect.yMin, inRect.width, 200), "Please use \"In-Game Def Editor\" to modify traits.");
		}
    }

    public class Settings : ModSettings
    {
        private readonly static Dictionary<string, TraitStat> Backup = new Dictionary<string, TraitStat>();

        private readonly Dictionary<string, TraitDef> TraitDefs = new Dictionary<string, TraitDef>();
        private TraitDef selected = null;
        private string[] comBuffer = { "", "", "", "", "", "", "", "", "", "", "" };

        private List<TraitStat> expose = null;

        public void DoWindowContents(Rect rect)
		{
			this.Init();

			float x = rect.xMin, y = rect.yMin;
            Widgets.Label(new Rect(x, y, 200, 32), "ConsolidatedTraits.EditTraits".Translate());
            y += 40;

            x += 20;
            string label = (this.selected == null) ? "ConsolidatedTraits.SelectTrait".Translate().ToString() : this.selected.defName;
            if (Widgets.ButtonText(new Rect(x, y, 200, 32), label))
            {
                this.DrawFloatingOptions();
            }
            y += 60;

            if (this.selected != null)
            {
                x += 10;

                Widgets.Label(new Rect(x, y, 200, 32), this.selected.defName);
                y += 40;

                float commonality = (float)TraitStat.CommonalityFI.GetValue(this.selected);
                Widgets.TextFieldNumericLabeled(
                    new Rect(0, y, 300, 32), 
                    "ConsolidatedTraits.Commonality".Translate() + " ", ref commonality, ref this.comBuffer[0], 0f, 10f);
                TraitStat.CommonalityFI.SetValue(this.selected, commonality);
                y += 60;
                
                for (int i = 0; i < this.selected.degreeDatas.Count && i + 1 < this.comBuffer.Length; ++i)
                {
                    TraitDegreeData d = this.selected.degreeDatas[i];

                    Widgets.Label(new Rect(x, y, 200, 32), "    " + d.label);
                    y += 30;
                    Widgets.TextFieldNumericLabeled(
                        new Rect(x, y, 300, 32), 
                        "    " + "ConsolidatedTraits.Commonality".Translate() + " ", ref d.commonality, ref this.comBuffer[i + 1], 0f, 10f);
                    y += 40;
                }

                y += 60;
                if (Widgets.ButtonText(new Rect(x, y, 100, 32), "Reset"))
                {
                    this.ResetTrait(this.selected);
                }
            }


            if (Widgets.ButtonText(new Rect(rect.xMax - 132, rect.yMax - 32, 100, 32), "ConsolidatedTraits.ResetAll".Translate()))
            {
                foreach (TraitDef d in this.TraitDefs.Values)
                {
                    this.ResetTrait(d);
                }
            }
        }

        private void ResetTrait(TraitDef t)
        {
            foreach (TraitDef d in this.TraitDefs.Values)
            {
                if (Backup.TryGetValue(d.defName, out TraitStat bu))
                {
                    bu.ApplyStats(d);
                }
            }

            if (this.selected != null)
                SetBuffer(this.selected);
        }

        private void SetBuffer(TraitDef t)
        {
            this.comBuffer[0] = ((float)TraitStat.CommonalityFI.GetValue(this.selected)).ToString("n4");
            for (int i = 0; i < this.selected.degreeDatas.Count && i + 1 < this.comBuffer.Length; ++i)
            {
                this.comBuffer[i + 1] = this.selected.degreeDatas[i].commonality.ToString("n4");
            }
        }

        public override void ExposeData()
        {
			this.Init();

            base.ExposeData();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                expose = new List<TraitStat>();
                foreach (TraitDef d in this.TraitDefs.Values)
                {
                    TraitStat s = new TraitStat(d);
                    bool shouldExpose = true;
                    if (Backup.TryGetValue(d.defName, out TraitStat bu))
                    {
                        shouldExpose = !bu.Equals(s);
                    }

                    if (shouldExpose)
                        expose.Add(s);
                }
            }

            Scribe_Collections.Look(ref this.expose, "overrides");
        }

        private void DrawFloatingOptions()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (TraitDef d in this.TraitDefs.Values)
            {
                //if (d != this.selected)
                //{
                options.Add(new FloatMenuOption(d.defName, delegate
                {
                    this.selected = d;
                    this.SetBuffer(this.selected);
                }, MenuOptionPriority.Default, null, null, 0f, null, null));
                //}
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public void Init()
		{
			if (HarmonyPatches.UsingInGameDefEditor)
				return;

			if (this.TraitDefs.Count == 0)
            {
                foreach(TraitDef d in DefDatabase<TraitDef>.AllDefs)
                    this.TraitDefs.Add(d.defName, d);
            }

            if (Backup.Count == 0)
            {
                foreach (TraitDef d in this.TraitDefs.Values)
                {
                    Backup.Add(d.defName, new TraitStat(d));
                }
            }

            if (this.expose != null && this.TraitDefs != null && this.TraitDefs.Count > 0)
            {
                foreach(TraitStat from in this.expose)
                {
                    if (this.TraitDefs.TryGetValue(from.DefName, out TraitDef to))
                    {
                        from.ApplyStats(to);
                    }
                    else
                    {
                        Log.Warning("Failed to apply trait settings to: " + from.DefName);
                    }
                }

                this.expose.Clear();
                this.expose = null;
            }
        }

        private class TraitStat : IExposable
        {
            public readonly static FieldInfo CommonalityFI = typeof(TraitDef).GetField("commonality", BindingFlags.Instance | BindingFlags.NonPublic);

            public string DefName;
            public float Value;
            public List<TraitStat> DegreeData;
            public TraitStat() { }
            public TraitStat(TraitDef d)
            {
                this.DefName = d.defName;
                this.Value = (float)CommonalityFI.GetValue(d);
                this.DegreeData = null;
                if (d.degreeDatas != null && d.degreeDatas.Count > 0)
                {
                    this.DegreeData = new List<TraitStat>(d.degreeDatas.Count);
                    foreach (TraitDegreeData tdd in d.degreeDatas)
                    {
                        this.DegreeData.Add(new TraitStat(tdd));
                    }
                }
            }
            public TraitStat(TraitDegreeData d)
            {
                this.DefName = d.label;
                this.Value = d.commonality;
            }

            public bool ApplyStats(TraitDef d)
            {
                if (!d.defName.Equals(this.DefName))
                {
                    Log.Warning("Trying to apply stats [" + this.DefName + "] to wrong TraitDef: [" + d.defName + "]");
                    return false;
                }
                CommonalityFI.SetValue(d, this.Value);
                foreach (TraitStat from in this.DegreeData)
                {
                    bool found = false;
                    foreach (TraitDegreeData to in d.degreeDatas)
                    {
                        if (from.DefName.Equals(to.label))
                        {
                            to.commonality = from.Value;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        Log.Warning("Unable to apply degree stat [" + from.DefName + "]");
                    }
                }
                return true;
            }

            public bool ApplyStats(TraitDegreeData d)
            {
                if (!this.DefName.Equals(d.label))
                {
                    Log.Warning("Trying to apply stats [" + this.DefName + "] to wrong TraitDegreeData: [" + d.label + "]");
                    return false;
                }
                d.commonality = this.Value;
                return true;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref this.DefName, "defName");
                Scribe_Values.Look(ref this.Value, "value");
                Scribe_Collections.Look(ref DegreeData, "data");
            }

            public override string ToString()
            {
                return "TraitStat: DefName: [" + this.DefName + "] Value: [" + this.DefName + "]";
            }

            public override int GetHashCode()
            {
                return this.ToString().GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj == null)
                {
                    return false;
                }
                
                if (obj is TraitStat ts)
                {
                    if (!string.Equals(this.DefName, ts.DefName) ||
                        this.Value != ts.Value)
                    {
                        return false;
                    }

                    if (this.DegreeData == null && ts.DegreeData == null)
                    {
                        return true;
                    }

                    if (this.DegreeData == null && ts.DegreeData != null ||
                        this.DegreeData != null && ts.DegreeData == null ||
                        this.DegreeData.Count != ts.DegreeData.Count)
                    {
                        return false;
                    }

                    int found = 0;
                    foreach (TraitStat s in this.DegreeData)
                    {
                        foreach (TraitStat dd in ts.DegreeData)
                        {
                            if (s.DefName.Equals(dd.DefName))
                            {
                                if (!s.Equals(dd))
                                {
                                    return false;
                                }
                                ++found;
                                break;
                            }
                        }
                    }

                    if (found != this.DegreeData.Count)
                    {
                        return false;
                    }
                    return true;
                }
                return false;
            }
        }
    }
}