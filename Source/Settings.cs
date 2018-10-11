using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using System;
using System.Reflection;

namespace ConfigurableMaps.Settings
{
    public class Controller : Mod
    {
        public static Settings Settings;

        public Controller(ModContentPack content) : base(content)
        {
            Settings = base.GetSettings<Settings>();
        }

        public override string SettingsCategory()
        {
            return "ConsolidatedTraits".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }

    public class Settings : ModSettings
    {
        private readonly static List<TraitBackup> Backup = new List<TraitBackup>();

        private readonly Dictionary<string, TraitDef> TraitDefs = new Dictionary<string, TraitDef>();
        private TraitDef selected = null;
        private string[] comBuffer = { "", "", "", "", "", "", "", "", "", "", "" };
        private FieldInfo comFI = typeof(TraitDef).GetField("commonality", BindingFlags.Instance | BindingFlags.NonPublic);

        private List<TraitBackup> expose = null;

        public void DoWindowContents(Rect rect)
        {
            this.Init();

            Listing_Standard l = new Listing_Standard
            {
                ColumnWidth = rect.width / 2.0f
            };
            l.Begin(rect);

            l.Label("ConsolidatedTraits.EditTraits".Translate());

            string label = (this.selected == null) ? "ConsolidatedTraits.SelectTrait".Translate() : this.selected.defName;
            if (l.ButtonText(label))
            {
                this.DrawFloatingOptions();
            }

            if (this.selected != null)
            {
                l.Gap();
                l.Label(this.selected.defName);
                float commonality = (float)this.comFI.GetValue(this.selected);
                l.TextFieldNumericLabeled("ConsolidatedTraits.Commonality".Translate(), ref commonality, ref this.comBuffer[0], 0f, 10f);
                this.comFI.SetValue(this.selected, commonality);

                l.Gap();
                for (int i = 0; i < this.selected.degreeDatas.Count && i + 1 < this.comBuffer.Length; ++i)
                {
                    TraitDegreeData d = this.selected.degreeDatas[i];

                    l.Label("    " + d.label);
                    l.TextFieldNumericLabeled("    " + "ConsolidatedTraits.Commonality".Translate(), ref d.commonality, ref this.comBuffer[i + 1], 0f, 10f);
                }
                
                l.Gap(20);
                if (l.ButtonText("Reset"))
                {
                    this.ResetTrait(this.selected);
                }
            }

            l.End();


            if (Widgets.ButtonText(new Rect(rect.x + 10, rect.yMax - 32, 100, 32), "ConsolidatedTraits.ResetAll".Translate()))
            {
                foreach (TraitDef d in this.TraitDefs.Values)
                {
                    this.ResetTrait(d);
                }
            }
        }

        private void ResetTrait(TraitDef t)
        {
            foreach (TraitBackup b in Backup)
            {
                if (b.Label.Equals(t.defName))
                {
                    this.comFI.SetValue(t, b.Value);
                    if (b.DegreeData != null && t.degreeDatas != null)
                    {
                        foreach (TraitBackup bd in b.DegreeData)
                        {
                            foreach (TraitDegreeData data in t.degreeDatas)
                            {
                                if (data.label.Equals(bd.Label))
                                {
                                    data.commonality = bd.Value;
                                }
                            }
                        }
                    }
                }
            }
            if (t == this.selected)
                SetBuffer(this.selected);
        }

        private void SetBuffer(TraitDef t)
        {
            this.comBuffer[0] = ((float)this.comFI.GetValue(this.selected)).ToString("n4");
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
                expose = new List<TraitBackup>();
                foreach (TraitDef d in this.TraitDefs.Values)
                {
                    expose.Add(new TraitBackup(d.defName, (float)this.comFI.GetValue(d), d.degreeDatas));
                }
            }

            Scribe_Collections.Look(ref this.expose, "overrides");
        }

        private void DrawFloatingOptions()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (TraitDef d in this.TraitDefs.Values)
            {
                if (d != this.selected)
                {
                    options.Add(new FloatMenuOption(d.defName, delegate
                    {
                        this.selected = d;
                        this.SetBuffer(this.selected);
                    }, MenuOptionPriority.Default, null, null, 0f, null, null));
                }
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public void Init()
        {
            if (this.TraitDefs.Count == 0)
            {
                foreach(TraitDef d in DefDatabase<TraitDef>.AllDefs)
                    this.TraitDefs.Add(d.defName, d);
            }

            if (Backup.Count == 0)
            {
                foreach (TraitDef d in this.TraitDefs.Values)
                {
                    Backup.Add(new TraitBackup(d.defName, (float)this.comFI.GetValue(d), d.degreeDatas));
                }
            }

            if (this.expose != null)
            {
                foreach(TraitBackup t in this.expose)
                {
                    if (this.TraitDefs.TryGetValue(t.Label, out TraitDef d))
                    {
                        comFI.SetValue(d, t.Value);
                        if (d.degreeDatas != null && t.DegreeData != null)
                        {
                            foreach (TraitDegreeData data in d.degreeDatas)
                            {
                                foreach (TraitBackup b in t.DegreeData)
                                {
                                    if (b.Label.Equals(data.label))
                                    {
                                        data.commonality = b.Value;
                                    }
                                }
                            }
                        }
                    }
                }

                this.expose.Clear();
                this.expose = null;
            }
        }

        private class TraitBackup : IExposable
        {
            public string Label;
            public float Value;
            public List<TraitBackup> DegreeData;
            public TraitBackup() { }
            public TraitBackup(string label, float value, List<TraitDegreeData> degreeData = null)
            {
                this.Label = label;
                this.Value = value;
                this.DegreeData = null;
                if (degreeData != null && degreeData.Count > 0)
                {
                    this.DegreeData = new List<TraitBackup>(degreeData.Count);
                    foreach (TraitDegreeData d in degreeData)
                    {
                        this.DegreeData.Add(new TraitBackup(d.label, d.commonality));
                    }
                }
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref this.Label, "defName");
                Scribe_Values.Look(ref this.Value, "value");
                Scribe_Collections.Look(ref DegreeData, "data");
            }
        }
    }
}